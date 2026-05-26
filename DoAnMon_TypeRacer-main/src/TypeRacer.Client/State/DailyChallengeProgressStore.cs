using System.Text.Json;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.State;

public sealed class DailyChallengeProgressSnapshot
{
    public DateOnly Date { get; init; }
    public int RacesToday { get; init; }
    public int AccuracyRacesToday { get; init; }
    public int PodiumsToday { get; init; }
    public bool WasNewRace { get; init; }
    public string Badge { get; init; } = string.Empty;
    public int RaceTarget => 3;
    public int AccuracyTarget => 2;
    public int PodiumTarget => 1;
    public int CompletedChallengeCount =>
        (RacesToday >= RaceTarget ? 1 : 0) +
        (AccuracyRacesToday >= AccuracyTarget ? 1 : 0) +
        (PodiumsToday >= PodiumTarget ? 1 : 0);
}

public static class DailyChallengeProgressStore
{
    private const int MaxProcessedRaceIds = 80;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static DailyChallengeProgressSnapshot RecordRace(
        int userId,
        string username,
        int raceId,
        RaceResultDto result,
        int totalPlayers)
    {
        var state = Load();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var userKey = userId > 0 ? userId.ToString() : (username.Trim().ToLowerInvariant() is { Length: > 0 } key ? key : "guest");

        if (state.Date != today || !string.Equals(state.UserKey, userKey, StringComparison.Ordinal))
            state = new DailyChallengeProgressFile { Date = today, UserKey = userKey };

        var raceKey = raceId > 0 ? raceId.ToString() : $"{result.Username}:{result.TimeTakenMs}:{result.Wpm:F1}:{result.Accuracy:F1}";
        state.ProcessedRaceIds ??= new HashSet<string>(StringComparer.Ordinal);
        var wasNewRace = state.ProcessedRaceIds.Add(raceKey);
        if (wasNewRace)
        {
            state.RacesToday++;
            if (result.Accuracy >= 95m)
                state.AccuracyRacesToday++;
            if (result.Position <= Math.Min(3, Math.Max(1, totalPlayers)))
                state.PodiumsToday++;
        }

        state.ProcessedRaceIds = state.ProcessedRaceIds
            .Reverse()
            .Take(MaxProcessedRaceIds)
            .Reverse()
            .ToHashSet(StringComparer.Ordinal);

        Save(state);
        return BuildSnapshot(state, wasNewRace);
    }

    private static DailyChallengeProgressSnapshot BuildSnapshot(DailyChallengeProgressFile state, bool wasNewRace)
    {
        var completed =
            (state.RacesToday >= 3 ? 1 : 0) +
            (state.AccuracyRacesToday >= 2 ? 1 : 0) +
            (state.PodiumsToday >= 1 ? 1 : 0);
        var badge = completed switch
        {
            >= 3 => "Daily Diamond",
            2 => "Daily Gold",
            1 => "Daily Starter",
            _ => "Daily Warmup",
        };

        return new DailyChallengeProgressSnapshot
        {
            Date = state.Date,
            RacesToday = state.RacesToday,
            AccuracyRacesToday = state.AccuracyRacesToday,
            PodiumsToday = state.PodiumsToday,
            WasNewRace = wasNewRace,
            Badge = badge,
        };
    }

    private static DailyChallengeProgressFile Load()
    {
        try
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new DailyChallengeProgressFile();

            var state = JsonSerializer.Deserialize<DailyChallengeProgressFile>(File.ReadAllText(path), JsonOptions);
            state ??= new DailyChallengeProgressFile();
            state.ProcessedRaceIds ??= new HashSet<string>(StringComparer.Ordinal);
            return state;
        }
        catch
        {
            return new DailyChallengeProgressFile();
        }
    }

    private static void Save(DailyChallengeProgressFile state)
    {
        try
        {
            var path = GetStorePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Daily challenge progress is a local UX bonus; gameplay must not fail if disk I/O fails.
        }
    }

    private static string GetStorePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;
        return Path.Combine(root, "TypeRacer", "daily-challenges.json");
    }

    private sealed class DailyChallengeProgressFile
    {
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public string UserKey { get; set; } = string.Empty;
        public int RacesToday { get; set; }
        public int AccuracyRacesToday { get; set; }
        public int PodiumsToday { get; set; }
        public HashSet<string>? ProcessedRaceIds { get; set; } = new(StringComparer.Ordinal);
    }
}
