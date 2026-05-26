using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Room;

public class RoomListResponse
{
    public List<RoomDto> Rooms { get; set; } = new();
}
