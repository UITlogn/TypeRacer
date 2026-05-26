using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared.Payloads.Auth;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public class AuthHandler : IMessageHandler
{
    private readonly AuthService _authService;
    private readonly ServerState _serverState;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;

    public AuthHandler(AuthService authService, ServerState serverState, MistakeMemoryService mistakeMemory, FileLogger logger)
    {
        _authService = authService;
        _serverState = serverState;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case MessageType.LOGIN_REQUEST:
                await HandleLoginAsync(client, session, message, ct);
                break;

            case MessageType.REGISTER_REQUEST:
                await HandleRegisterAsync(client, session, message, ct);
                break;

            case MessageType.LOGOUT:
                await HandleLogoutAsync(client, session, ct);
                break;

            case MessageType.HEARTBEAT_PING:
                await session.SendAsync(NetworkMessage.CreateEmpty(MessageType.HEARTBEAT_PONG), ct);
                break;

            case MessageType.DISCONNECT:
                client.TcpClient.Close();
                break;
        }
    }

    private async Task HandleLoginAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<LoginRequest>();
        if (request == null)
        {
            await session.SendErrorAsync(Shared.Enums.ErrorCode.InvalidMessage, "Yêu cầu đăng nhập không hợp lệ");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            await session.SendAsync(MessageType.LOGIN_RESPONSE, new LoginResponse
            {
                Success = false,
                ErrorMessage = "Sai tên đăng nhập hoặc mật khẩu.",
            }, ct);
            return;
        }

        _logger.Info($"Đăng nhập: {request.Username}");

        var result = await _authService.LoginAsync(request.Username, request.Password);

        if (result.Success)
        {
            // Đá session cũ nếu có ai đang login cùng tài khoản
            var oldClient = _serverState.GetClientByUserId(result.User!.Id);
            if (oldClient != null && oldClient != client)
            {
                _logger.Info($"Đá session cũ của {result.User.Username}");
                _mistakeMemory.ClearUserRoom(oldClient.UserId, oldClient.CurrentRoomCode);
                try
                {
                    var oldSession = new ClientSession(oldClient);
                    await oldSession.SendErrorAsync(
                        Shared.Enums.ErrorCode.InvalidSession,
                        "Tài khoản đã đăng nhập ở nơi khác.");
                }
                catch { }

                // Xoá session cũ
                if (!string.IsNullOrEmpty(oldClient.SessionToken))
                    _serverState.RemoveClient(oldClient.SessionToken);

                try { oldClient.TcpClient.Close(); } catch { }
            }

            client.UserId = result.User.Id;
            client.Username = result.User.Username;
            client.SessionToken = result.SessionToken!;

            _serverState.AddClient(result.SessionToken!, client);
            _logger.Info($"Đăng nhập thành công: {request.Username} (ID: {result.User.Id})");
        }
        else
        {
            _logger.Warn($"Đăng nhập thất bại: {request.Username} - {result.ErrorMessage}");
        }

        await session.SendAsync(MessageType.LOGIN_RESPONSE, result, ct);
    }

    private async Task HandleRegisterAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<RegisterRequest>();
        if (request == null)
        {
            await session.SendErrorAsync(Shared.Enums.ErrorCode.InvalidMessage, "Yêu cầu đăng ký không hợp lệ");
            return;
        }

        _logger.Info($"Đăng ký: {request.Username}");

        var result = await _authService.RegisterAsync(
            request.Username, request.Password);

        if (result.Success)
            _logger.Info($"Đăng ký thành công: {request.Username}");
        else
            _logger.Warn($"Đăng ký thất bại: {request.Username} - {result.ErrorMessage}");

        await session.SendAsync(MessageType.REGISTER_RESPONSE, result, ct);
    }

    private async Task HandleLogoutAsync(ConnectedClient client, ClientSession session, CancellationToken ct)
    {
        if (client.IsAuthenticated)
        {
            // Nếu đang ở phòng → auto leave
            if (client.CurrentRoomCode != null)
            {
                var room = _serverState.GetRoom(client.CurrentRoomCode);
                if (room != null)
                {
                    room.Players.TryRemove(client.UserId, out _);
                    _mistakeMemory.ClearUserRoom(client.UserId, room.RoomCode);

                    var leftPayload = new Shared.Payloads.Room.PlayerLeftPayload
                    {
                        UserId = client.UserId,
                        Username = client.Username,
                    };

                    if (room.HostUserId == client.UserId && room.Players.Values.Any(p => !p.IsBot))
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
                        // Broadcast to remaining players
                        foreach (var player in room.Players.Values)
                        {
                            var playerClient = _serverState.GetClientByUserId(player.UserId);
                            if (playerClient != null)
                            {
                                try
                                {
                                    var playerSession = new ClientSession(playerClient);
                                    await playerSession.SendAsync(
                                        NetworkMessage.Create(MessageType.PLAYER_LEFT, leftPayload));
                                }
                                catch { }
                            }
                        }
                    }
                }
                client.CurrentRoomCode = null;
            }

            await _authService.LogoutAsync(client.UserId);
            _serverState.RemoveClient(client.SessionToken);
            _logger.Info($"Đăng xuất: {client.Username}");

            client.UserId = 0;
            client.SessionToken = string.Empty;
        }
    }
}
