using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Room;

public class CreateRoomResponse
{
    public bool Success { get; set; }
    public string? RoomCode { get; set; }
    public RoomDto? Room { get; set; }
    public string? ErrorMessage { get; set; }
}
