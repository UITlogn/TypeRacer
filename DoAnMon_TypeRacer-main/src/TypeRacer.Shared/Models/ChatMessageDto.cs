namespace TypeRacer.Shared.Models;

public class ChatMessageDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}
