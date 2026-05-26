namespace TypeRacer.Shared.Payloads.Stats;

public class GetLeaderboardRequest
{
    public string SortBy { get; set; } = "rank_points"; // rank_points, avg_wpm, total_wins
    public int Top { get; set; } = 20;
}
