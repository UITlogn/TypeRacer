using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.State;
using TypeRacer.Shared;
using TypeRacer.Shared.Payloads.Room;
using TypeRacer.Shared.Payloads.System;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Services;

public class HeartbeatService
{
    private readonly ServerState _serverState;
    private readonly FileLogger _logger;

    public HeartbeatService(ServerState serverState, FileLogger logger)
    {
        _serverState = serverState;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.Info("Heartbeat monitor started");

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(10_000, ct); // Check every 10 seconds

            var now = DateTime.UtcNow;
            var timeoutThreshold = TimeSpan.FromMilliseconds(Constants.HeartbeatTimeoutMs);

            foreach (var kvp in _serverState.Clients)
            {
                var client = kvp.Value;
                if (client.HeartbeatGraceUntil > now)
                    continue;

                if (now - client.LastHeartbeat > timeoutThreshold)
                {
                    _logger.Warn($"Client {client.Username} heartbeat timeout, disconnecting");

                    try
                    {
                        var session = new ClientSession(client);
                        await session.SendAsync(MessageType.DISCONNECT,
                            new DisconnectPayload { Reason = "Heartbeat timeout" });
                    }
                    catch { }

                    try
                    {
                        client.TcpClient.Close();
                    }
                    catch { }

                    await CleanupTimedOutClientAsync(client);
                }
            }
        }
    }

    private async Task CleanupTimedOutClientAsync(ConnectedClient client)
    {
        if (!string.IsNullOrWhiteSpace(client.SessionToken))
            _serverState.RemoveClient(client.SessionToken);

        if (string.IsNullOrWhiteSpace(client.CurrentRoomCode))
            return;

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null)
            return;

        room.Players.TryRemove(client.UserId, out _);
        var leftPayload = new PlayerLeftPayload
        {
            UserId = client.UserId,
            Username = client.Username,
        };

        if (room.HostUserId == client.UserId)
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
            if (room.IsCommunityRoom)
            {
                room.ResetAfterLastHumanLeaves();
            }
            else
            {
                _serverState.RemoveRoom(room.RoomCode);
            }
            return;
        }

        foreach (var player in room.Players.Values.Where(p => !p.IsBot))
        {
            var playerClient = _serverState.GetClientByUserId(player.UserId);
            if (playerClient == null)
                continue;

            try
            {
                await new ClientSession(playerClient).SendAsync(MessageType.PLAYER_LEFT, leftPayload);
            }
            catch { }
        }
    }
}
