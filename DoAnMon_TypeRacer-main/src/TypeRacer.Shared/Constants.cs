namespace TypeRacer.Shared;

public static class Constants
{
    // Network
    public const int DefaultServerPort = 5000;
    public const int DefaultLoadBalancerPort = 4999;
    public const int HeaderSize = 8; // 4 (length) + 2 (type) + 2 (flags)
    public const int MaxMessageSize = 1024 * 1024; // 1MB

    // Heartbeat
    public const int HeartbeatIntervalMs = 30_000;  // 30 seconds
    public const int HeartbeatTimeoutMs = 90_000;    // 90 seconds

    // Game
    public const int CountdownSeconds = 3;
    public const int DefaultRaceDurationSeconds = 5 * 60; // 5 minutes
    public const int MinRaceDurationSeconds = 30;       // 30 seconds
    public const int MaxRaceDurationSeconds = 20 * 60;  // 20 minutes

    [Obsolete("Use DefaultRaceDurationSeconds and compute by duration instead of fixed milliseconds.")]
    public const int RaceTimeoutMs = DefaultRaceDurationSeconds * 1000;
    public const int ProgressBroadcastIntervalMs = 300; // 300ms throttle
    public const int MaxPlayersPerRoom = 5;
    public const int RoomCodeLength = 6;

    // Community quick play
    public const string CommunityRoomCode = "QUICK";
    public const string CommunityRoomName = "Quick Play Cộng đồng";
    public const int CommunityQuickPlayIntervalSeconds = 3 * 60;
    public const int CommunityQuickPlayMaxPlayers = 50;

    // Room game modes
    public const string GameModeClassic = "classic";
    public const string GameModeAccuracy = "accuracy";
    public const string GameModeNoBackspace = "no_backspace";
    public const string GameModeSuddenDeath = "sudden_death";
    public const string GameModeAiPractice = "ai_practice";
    public const string DefaultGameMode = GameModeClassic;

    public static string NormalizeGameMode(string? value)
    {
        var mode = (value ?? DefaultGameMode).Trim().ToLowerInvariant();
        return mode switch
        {
            GameModeAccuracy => GameModeAccuracy,
            GameModeNoBackspace => GameModeNoBackspace,
            GameModeSuddenDeath => GameModeSuddenDeath,
            GameModeAiPractice => GameModeAiPractice,
            _ => GameModeClassic,
        };
    }

    // AI practice bot difficulty. RPM here is the target typing pace used by the AI opponent.
    public const string AiPracticeEasy = "easy";
    public const string AiPracticeMedium = "medium";
    public const string AiPracticeHard = "hard";
    public const string AiPracticeNightmare = "nightmare";
    public const string DefaultAiPracticeDifficulty = AiPracticeEasy;

    public const int AiPracticeEasyRpm = 25;
    public const int AiPracticeMediumRpm = 45;
    public const int AiPracticeHardRpm = 65;
    public const int AiPracticeNightmareRpm = 90;

    public static string NormalizeAiPracticeDifficulty(string? value)
    {
        var difficulty = (value ?? DefaultAiPracticeDifficulty).Trim().ToLowerInvariant();
        return difficulty switch
        {
            AiPracticeMedium => AiPracticeMedium,
            AiPracticeHard => AiPracticeHard,
            AiPracticeNightmare => AiPracticeNightmare,
            _ => AiPracticeEasy,
        };
    }

    public static int GetAiPracticeTargetRpm(string? difficulty)
    {
        return NormalizeAiPracticeDifficulty(difficulty) switch
        {
            AiPracticeMedium => AiPracticeMediumRpm,
            AiPracticeHard => AiPracticeHardRpm,
            AiPracticeNightmare => AiPracticeNightmareRpm,
            _ => AiPracticeEasyRpm,
        };
    }

    public static double GetAiPracticeAccuracy(string? difficulty)
    {
        return NormalizeAiPracticeDifficulty(difficulty) switch
        {
            AiPracticeMedium => 98.0,
            AiPracticeHard => 98.7,
            AiPracticeNightmare => 99.4,
            _ => 96.5,
        };
    }

    // Crypto - shared secret is SHA-256-derived into an AES-256 key.
    public const string SharedAesKey = "TypeRacer2026NT106UIT!SecretKey32";
    public const string SharedAesIV = "TR_IV_16BytesOK!"; // legacy static-IV compatibility only

    // Session
    public const int SessionTokenLength = 32;
}
