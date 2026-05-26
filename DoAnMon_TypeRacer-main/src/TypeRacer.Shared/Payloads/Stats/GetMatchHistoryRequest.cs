namespace TypeRacer.Shared.Payloads.Stats;

public class GetMatchHistoryRequest
{
    public int? UserId { get; set; } // null = self
    public int Limit { get; set; } = 20;
}
