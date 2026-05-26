using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Game;

public class ProgressBroadcastPayload
{
    public string RoomCode { get; set; } = string.Empty;
    public List<PlayerProgressDto> Players { get; set; } = new();
}
