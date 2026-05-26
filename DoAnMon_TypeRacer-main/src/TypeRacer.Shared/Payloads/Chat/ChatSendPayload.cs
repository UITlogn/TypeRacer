namespace TypeRacer.Shared.Payloads.Chat;

public class ChatSendPayload
{
    public string RoomCode { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
