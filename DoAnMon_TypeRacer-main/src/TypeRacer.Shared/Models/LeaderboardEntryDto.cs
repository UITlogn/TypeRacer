namespace TypeRacer.Shared.Models;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal AvgWpm { get; set; }
    public decimal BestWpm { get; set; }
    public int TotalRaces { get; set; }
    public int TotalWins { get; set; }
}
