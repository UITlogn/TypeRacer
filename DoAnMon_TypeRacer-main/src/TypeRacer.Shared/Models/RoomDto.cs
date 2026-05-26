using TypeRacer.Shared;

namespace TypeRacer.Shared.Models;

public class RoomDto
{
    public string RoomCode { get; set; } = string.Empty;
    public int HostUserId { get; set; }
    public string HostUsername { get; set; } = string.Empty;
    public int CurrentPlayers { get; set; }
    public string PassageLanguage { get; set; } = "en";
    public int RaceDurationSeconds { get; set; } = Constants.DefaultRaceDurationSeconds;
    public bool EnableAiMode { get; set; }
    public string GameMode { get; set; } = Constants.DefaultGameMode;
    public string AiPracticeDifficulty { get; set; } = Constants.DefaultAiPracticeDifficulty;
    public bool HasCustomPassage { get; set; }
    public bool IsCommunityRoom { get; set; }
    public bool IsJoinableInProgress { get; set; }
    public string Status { get; set; } = "waiting";
    public int SecondsUntilNextStart { get; set; }
}
