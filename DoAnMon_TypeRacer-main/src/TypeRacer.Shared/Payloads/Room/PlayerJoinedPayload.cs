using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Room;

public class PlayerJoinedPayload
{
    public RoomPlayerDto Player { get; set; } = new();
}
