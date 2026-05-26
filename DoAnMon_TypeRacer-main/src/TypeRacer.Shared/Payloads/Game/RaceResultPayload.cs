using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Game;

public class RaceResultPayload
{
    public string RoomCode { get; set; } = string.Empty;
    public int RaceId { get; set; }
    public List<RaceResultDto> Results { get; set; } = new();
}
