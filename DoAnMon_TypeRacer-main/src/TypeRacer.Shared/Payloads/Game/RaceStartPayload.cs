using TypeRacer.Shared;

namespace TypeRacer.Shared.Payloads.Game;

public class RaceStartPayload
{
    public string PassageText { get; set; } = string.Empty;
    public int PassageId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string PassageLanguage { get; set; } = "en";
    public int RaceDurationSeconds { get; set; }
    public int RaceElapsedSeconds { get; set; }
    public string GameMode { get; set; } = Constants.DefaultGameMode;
    public string AiPracticeDifficulty { get; set; } = Constants.DefaultAiPracticeDifficulty;
}
