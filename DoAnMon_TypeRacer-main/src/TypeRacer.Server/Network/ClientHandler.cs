using TypeRacer.Server.Handlers;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Network;

/// <summary>
/// Handles a single client connection: reads messages in a loop and dispatches to handlers.
/// Runs on its own Task (thread from ThreadPool).
/// </summary>
public class ClientHandler
{
    private readonly ConnectedClient _client;
    private readonly ClientSession _session;
    private readonly IMessageHandler _authHandler;
    private readonly IMessageHandler _roomHandler;
    private readonly GameHandler _gameHandler;
    private readonly IMessageHandler _chatHandler;
    private readonly IMessageHandler _statsHandler;
    private readonly ServerState _serverState;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;

    public ClientHandler(
        ConnectedClient client,
        IMessageHandler authHandler,
        IMessageHandler roomHandler,
        GameHandler gameHandler,
        IMessageHandler chatHandler,
        IMessageHandler statsHandler,
        ServerState serverState,
        MistakeMemoryService mistakeMemory,
        FileLogger logger)
    {
        _client = client;
        _session = new ClientSession(client);
        _authHandler = authHandler;
        _roomHandler = roomHandler;
        _gameHandler = gameHandler;
        _chatHandler = chatHandler;
        _statsHandler = statsHandler;
        _serverState = serverState;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    public async Task HandleAsync(CancellationToken ct)
    {
        var remoteEndpoint = _client.TcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.Info($"Client connected: {remoteEndpoint}");

        try
        {
            while (!ct.IsCancellationRequested && _client.TcpClient.Connected)
            {
                var message = await MessageReader.ReadAsync(_client.Stream, ct);
                if (message == null)
                {
                    _logger.Info($"Client disconnected (stream closed): {remoteEndpoint}");
                    break;
                }

                _client.LastHeartbeat = DateTime.UtcNow;
                if (message.Flags.HasFlag(MessageFlags.Encrypted))
                    _client.UseEncryptedMessages = true;

                await DispatchMessageAsync(message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutting down
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.Info($"Client connection lost: {remoteEndpoint}");
        }
        catch (InvalidDataException ex)
        {
            _logger.Warn($"Invalid protocol from {remoteEndpoint}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling client {remoteEndpoint}", ex);
        }
        finally
        {
            await CleanupClientAsync();
        }
    }

    private async Task DispatchMessageAsync(NetworkMessage message, CancellationToken ct)
    {
        var type = (int)message.Type;

        // Route to appropriate handler based on message type range
        IMessageHandler handler = type switch
        {
            >= 100 and < 200 => _authHandler,
            >= 200 and < 300 => _roomHandler,
            >= 300 and < 400 => _gameHandler,
            >= 400 and < 500 => _chatHandler,
            >= 500 and < 600 => _statsHandler,
            >= 900 => _authHandler, // Heartbeat + system messages
            _ => _authHandler,
        };

        // Require auth for Room/Game/Chat/Stats ranges only.
        // System messages (900+) like heartbeat should stay available pre-login.
        if (type is >= 200 and < 600 && !_client.IsAuthenticated)
        {
            await _session.SendErrorAsync(
                Shared.Enums.ErrorCode.InvalidSession,
                "You must be logged in to perform this action.");
            return;
        }

        await handler.HandleAsync(_client, _session, message, ct);
    }

    private async Task CleanupClientAsync()
    {
        try
        {
            // Remove from room if in one
            if (_client.CurrentRoomCode != null)
            {
                var room = _serverState.GetRoom(_client.CurrentRoomCode);
                if (room != null)
                {
                    room.Players.TryRemove(_client.UserId, out _);
                    _mistakeMemory.ClearUserRoom(_client.UserId, room.RoomCode);

                    // Notify other players
                    var leftPayload = new Shared.Payloads.Room.PlayerLeftPayload
                    {
                        UserId = _client.UserId,
                        Username = _client.Username,
                    };

                    // Transfer host if needed
                    if (room.HostUserId == _client.UserId && room.Players.Values.Any(p => !p.IsBot))
                    {
                        var newHost = room.Players.Values.FirstOrDefault(p => !p.IsBot);
                        if (newHost != null)
                        {
                            room.HostUserId = newHost.UserId;
                            leftPayload.NewHostUserId = newHost.UserId;
                        }
                    }

                    if (!room.Players.Values.Any(p => !p.IsBot))
                    {
                        // Hủy race timeout nếu đang chạy
                        room.RaceTimeoutCts?.Cancel();
                        room.AiBotCts?.Cancel();
                        _mistakeMemory.ClearRoom(room.RoomCode);
                        if (room.IsCommunityRoom)
                        {
                            room.ResetAfterLastHumanLeaves();
                        }
                        else
                        {
                            _serverState.RemoveRoom(room.RoomCode);
                        }
                    }
                    else
                    {
                        // Broadcast to room
                        foreach (var player in room.Players.Values)
                        {
                            var playerClient = _serverState.GetClientByUserId(player.UserId);
                            if (playerClient != null)
                            {
                                try
                                {
                                    var session = new ClientSession(playerClient);
                                    await session.SendAsync(MessageType.PLAYER_LEFT, leftPayload);
                                }
                                catch { }
                            }
                        }

                        // Nếu đang Racing và tất cả người còn lại đều finished → kết thúc race
                        if (room.Status == Shared.Enums.RoomStatus.Racing &&
                            room.Players.Values.Where(p => !p.IsBot).All(p => p.IsFinished))
                        {
                            await _gameHandler.FinishRaceAsync(room);
                        }
                    }
                }
            }

            // Remove from connected clients
            if (!string.IsNullOrEmpty(_client.SessionToken))
            {
                _serverState.RemoveClient(_client.SessionToken);
            }

            _client.TcpClient.Close();
        }
        catch (Exception ex)
        {
            _logger.Error("Error during client cleanup", ex);
        }
    }
}
