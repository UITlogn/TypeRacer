using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Chat;

public class ChatBroadcastPayload
{
    public string RoomCode { get; set; } = string.Empty;
    public ChatMessageDto Message { get; set; } = new();
}
