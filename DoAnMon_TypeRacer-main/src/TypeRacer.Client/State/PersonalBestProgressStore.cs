using System.Text.Json;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.State;

public sealed class PersonalBestProgressSnapshot
{
    public string Mode { get; init; } = string.Empty;
    public int TotalRaces { get; init; }
    public bool WasNewRace { get; init; }
    public decimal BestWpm { get; init; }
    public decimal PreviousBestWpm { get; init; }
    public decimal BestAccuracy { get; init; }
    public decimal PreviousBestAccuracy { get; init; }
    public decimal BestConsistency { get; init; }
    public decimal PreviousBestConsistency { get; init; }
    public int BestStreak { get; init; }
    public int PreviousBestStreak { get; init; }
    public bool IsNewBestWpm { get; init; }
    public bool IsNewBestAccuracy { get; init; }
    public bool IsNewBestConsistency { get; init; }
    public bool IsNewBestStreak { get; init; }
}

public static class PersonalBestProgressStore
{
    private const int MaxProcessedRaceIds = 160;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static PersonalBestProgressSnapshot RecordRace(
        int userId,
        string username,
        string mode,
        int raceId,
        RaceResultDto result)
    {
        var state = Load();
        var userKey = userId > 0 ? userId.ToString() : (username.Trim().ToLowerInvariant() is { Length: > 0 } key ? key : "guest");
        var modeKey = string.IsNullOrWhiteSpace(mode) ? "classic" : mode.Trim().ToLowerInvariant();
        var recordKey = $"{userKey}:{modeKey}";

        if (!state.Records.TryGetValue(recordKey, out var record))
        {
            record = new PersonalBestRecord { UserKey = userKey, Mode = modeKey };
            state.Records[recordKey] = record;
        }

        record.ProcessedRaceIds ??= new HashSet<string>(StringComparer.Ordinal);
        var raceKey = raceId > 0 ? raceId.ToString() : $"{result.Username}:{result.TimeTakenMs}:{result.Wpm:F1}:{result.Accuracy:F1}:{modeKey}";
        var wasNewRace = record.ProcessedRaceIds.Add(raceKey);

        var previousBestWpm = record.BestWpm;
        var previousBestAccuracy = record.BestAccuracy;
        var previousBestConsistency = record.BestConsistency;
        var previousBestStreak = record.BestStreak;
        var isNewWpm = false;
        var isNewAccuracy = false;
        var isNewConsistency = false;
        var isNewStreak = false;

        if (wasNewRace)
        {
            record.TotalRaces++;
            record.LastRaceAt = DateTime.Now;

            if (result.Wpm > record.BestWpm)
            {
                record.BestWpm = result.Wpm;
                isNewWpm = true;
            }
            if (result.Accuracy > record.BestAccuracy)
            {
                record.BestAccuracy = result.Accuracy;
                isNewAccuracy = true;
            }
            if (result.ConsistencyScore > record.BestConsistency)
            {
                record.BestConsistency = result.ConsistencyScore;
                isNewConsistency = true;
            }
            if (result.BestStreak > record.BestStreak)
            {
                record.BestStreak = result.BestStreak;
                isNewStreak = true;
            }

            record.ProcessedRaceIds = record.ProcessedRaceIds
                .Reverse()
                .Take(MaxProcessedRaceIds)
                .Reverse()
                .ToHashSet(StringComparer.Ordinal);

            Save(state);
        }

        return new PersonalBestProgressSnapshot
        {
            Mode = record.Mode,
            TotalRaces = record.TotalRaces,
            WasNewRace = wasNewRace,
            BestWpm = record.BestWpm,
            PreviousBestWpm = previousBestWpm,
            BestAccuracy = record.BestAccuracy,
            PreviousBestAccuracy = previousBestAccuracy,
            BestConsistency = record.BestConsistency,
            PreviousBestConsistency = previousBestConsistency,
            BestStreak = record.BestStreak,
            PreviousBestStreak = previousBestStreak,
            IsNewBestWpm = isNewWpm,
            IsNewBestAccuracy = isNewAccuracy,
            IsNewBestConsistency = isNewConsistency,
            IsNewBestStreak = isNewStreak,
        };
    }

    private static PersonalBestProgressFile Load()
    {
        try
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new PersonalBestProgressFile();

            var state = JsonSerializer.Deserialize<PersonalBestProgressFile>(File.ReadAllText(path), JsonOptions)
                ?? new PersonalBestProgressFile();
            state.Records ??= new Dictionary<string, PersonalBestRecord>(StringComparer.Ordinal);
            foreach (var record in state.Records.Values)
                record.ProcessedRaceIds ??= new HashSet<string>(StringComparer.Ordinal);
            return state;
        }
        catch
        {
            return new PersonalBestProgressFile();
        }
    }

    private static void Save(PersonalBestProgressFile state)
    {
        try
        {
            var path = GetStorePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Personal bests are local progress hints; race result display must stay available if disk I/O fails.
        }
    }

    private static string GetStorePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;
        return Path.Combine(root, "TypeRacer", "personal-bests.json");
    }

    private sealed class PersonalBestProgressFile
    {
        public Dictionary<string, PersonalBestRecord> Records { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class PersonalBestRecord
    {
        public string UserKey { get; set; } = string.Empty;
        public string Mode { get; set; } = "classic";
        public int TotalRaces { get; set; }
        public decimal BestWpm { get; set; }
        public decimal BestAccuracy { get; set; }
        public decimal BestConsistency { get; set; }
        public int BestStreak { get; set; }
        public DateTime LastRaceAt { get; set; }
        public HashSet<string>? ProcessedRaceIds { get; set; } = new(StringComparer.Ordinal);
    }
}
