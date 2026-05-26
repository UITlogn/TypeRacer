using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Game;

namespace TypeRacer.Shared.Payloads.Room;

public class JoinRoomResponse
{
    public bool Success { get; set; }
    public RoomDto? Room { get; set; }
    public List<RoomPlayerDto>? Players { get; set; }
    public bool RaceInProgress { get; set; }
    public RaceStartPayload? CurrentRace { get; set; }
    public string? ErrorMessage { get; set; }
}
