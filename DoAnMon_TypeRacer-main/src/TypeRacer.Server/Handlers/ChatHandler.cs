using TypeRacer.Server.Data;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.State;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Chat;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public class ChatHandler : IMessageHandler
{
    private readonly ServerState _serverState;
    private readonly IChatRepository _chatRepo;
    private readonly FileLogger _logger;

    public ChatHandler(ServerState serverState, IChatRepository chatRepo, FileLogger logger)
    {
        _serverState = serverState;
        _chatRepo = chatRepo;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        if (message.Type != MessageType.CHAT_SEND) return;

        var payload = message.GetPayload<ChatSendPayload>();
        if (payload == null || string.IsNullOrWhiteSpace(payload.Content)) return;

        if (client.CurrentRoomCode == null)
        {
            await session.SendErrorAsync(Shared.Enums.ErrorCode.NotInRoom, "You are not in a room.");
            return;
        }

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null) return;

        // Truncate message
        var content = payload.Content.Length > 500 ? payload.Content[..500] : payload.Content;

        _logger.Info($"Chat [{room.RoomCode}] {client.Username}: {content}");

        var broadcast = new ChatBroadcastPayload
        {
            RoomCode = room.RoomCode,
            Message = new ChatMessageDto
            {
                UserId = client.UserId,
                Username = client.Username,
                Content = content,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
        };

        try
        {
            var sentAt = DateTimeOffset.FromUnixTimeMilliseconds(broadcast.Message.Timestamp).UtcDateTime;
            await _chatRepo.SaveAsync(room.RoomCode, client.UserId, client.Username, content, sentAt);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to persist chat message for room {room.RoomCode}", ex);
        }

        var msg = NetworkMessage.Create(MessageType.CHAT_BROADCAST, broadcast);

        foreach (var player in room.Players.Values)
        {
            var playerClient = _serverState.GetClientByUserId(player.UserId);
            if (playerClient != null)
            {
                try
                {
                    var playerSession = new ClientSession(playerClient);
                    await playerSession.SendAsync(msg);
                }
                catch { }
            }
        }
    }
}
