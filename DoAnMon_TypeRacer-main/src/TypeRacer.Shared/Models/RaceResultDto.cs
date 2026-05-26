using TypeRacer.Shared;

namespace TypeRacer.Shared.Models;

public class RaceResultDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Position { get; set; }
    public decimal Wpm { get; set; }
    public decimal Accuracy { get; set; }
    public int CharsCorrect { get; set; }
    public int CharsWrong { get; set; }
    public int TimeTakenMs { get; set; }
    public bool IsCompleted { get; set; }
    public string TypedText { get; set; } = string.Empty;
    public string GameMode { get; set; } = Constants.DefaultGameMode;
    public int BackspaceCount { get; set; }
    public int BestStreak { get; set; }
    public decimal ConsistencyScore { get; set; }
    public bool IsDisqualified { get; set; }
    public List<string> Achievements { get; set; } = new();
    public bool IsAiBot { get; set; }
    public Dictionary<string, int> ObservedMistakeCharacters { get; set; } = new();
    public Dictionary<string, int> ObservedMistakeWords { get; set; } = new();
    public Dictionary<string, int> ObservedMistakeNgrams { get; set; } = new();
}
