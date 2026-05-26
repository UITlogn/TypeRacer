using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Stats;

public class LeaderboardResponse
{
    public List<LeaderboardEntryDto> Entries { get; set; } = new();
}
