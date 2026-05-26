using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Room;

public class RoomUpdatePayload
{
    public RoomDto Room { get; set; } = new();
    public List<RoomPlayerDto> Players { get; set; } = new();
}
