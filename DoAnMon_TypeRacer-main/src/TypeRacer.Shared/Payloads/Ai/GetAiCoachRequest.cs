namespace TypeRacer.Shared.Payloads.Ai;

public class GetAiCoachRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public int RaceId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Position { get; set; }
    public int TotalPlayers { get; set; }
    public decimal Wpm { get; set; }
    public decimal Accuracy { get; set; }
    public int CharsCorrect { get; set; }
    public int CharsWrong { get; set; }
    public int TimeTakenMs { get; set; }
    public bool IsCompleted { get; set; }
    public string Language { get; set; } = "vi";
    public string PassageText { get; set; } = string.Empty;
    public string TypedText { get; set; } = string.Empty;

    // Context 6-8 trận gần nhất để AI coach bớt generic.
    public int RecentRaceCount { get; set; }
    public int RecentCompletedCount { get; set; }
    public decimal RecentAvgWpm { get; set; }
    public decimal RecentAvgAccuracy { get; set; }
    public decimal RecentWpmTrend { get; set; }
    public decimal RecentAccuracyTrend { get; set; }
}
