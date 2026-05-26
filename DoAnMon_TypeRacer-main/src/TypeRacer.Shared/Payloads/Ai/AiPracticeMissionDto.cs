namespace TypeRacer.Shared.Payloads.Ai;

public class AiPracticeMissionDto
{
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string GameMode { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public decimal TargetWpm { get; set; }
    public decimal TargetAccuracy { get; set; }
    public int TargetRpm { get; set; }
    public string Passage { get; set; } = string.Empty;
    public string RewardBadge { get; set; } = string.Empty;
    public string SourceWeakspot { get; set; } = string.Empty;
}
