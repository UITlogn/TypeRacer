using TypeRacer.Shared.Models;

namespace TypeRacer.Shared.Payloads.Stats;

public class MatchHistoryResponse
{
    public List<MatchHistoryEntryDto> Matches { get; set; } = new();
}
