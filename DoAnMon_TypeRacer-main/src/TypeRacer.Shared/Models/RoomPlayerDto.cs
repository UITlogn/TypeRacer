namespace TypeRacer.Shared.Models;

public class RoomPlayerDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public bool IsHost { get; set; }
}
