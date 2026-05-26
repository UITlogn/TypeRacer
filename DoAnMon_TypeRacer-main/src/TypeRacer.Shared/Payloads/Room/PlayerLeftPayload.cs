namespace TypeRacer.Shared.Payloads.Room;

public class PlayerLeftPayload
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? NewHostUserId { get; set; }
}
