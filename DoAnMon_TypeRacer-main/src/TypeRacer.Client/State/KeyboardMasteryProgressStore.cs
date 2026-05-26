using System.Globalization;
using System.Text;
using System.Text.Json;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.State;

public sealed class KeyboardMasteryProgressSnapshot
{
    public int TotalRaces { get; init; }
    public bool WasNewRace { get; init; }
    public int TrackedKeyCount { get; init; }
    public int MasteredKeyCount { get; init; }
    public int MasteryTargetKeyCount { get; init; }
    public decimal CoveragePercent { get; init; }
    public decimal AverageAccuracy { get; init; }
    public int NeedsReviewCount { get; init; }
    public IReadOnlyList<string> StrongestKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WeakestKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NeedsReviewKeys { get; init; } = Array.Empty<string>();
}

public static class KeyboardMasteryProgressStore
{
    private const int MaxProcessedRaceIds = 180;
    private const int MasteredAttemptThreshold = 6;
    private const decimal MasteredAccuracyThreshold = 96m;
    private const decimal ReviewAccuracyThreshold = 88m;
    private static readonly string[] MasteryTargetKeys =
    {
        "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
        "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        "space",
    };
    private static readonly HashSet<string> MasteryTargetSet = new(MasteryTargetKeys, StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static KeyboardMasteryProgressSnapshot RecordRace(
        int userId,
        string username,
        int raceId,
        RaceResultDto result)
    {
        var state = Load();
        var userKey = userId > 0 ? userId.ToString() : (username.Trim().ToLowerInvariant() is { Length: > 0 } key ? key : "guest");

        if (!state.Records.TryGetValue(userKey, out var record))
        {
            record = new KeyboardMasteryRecord { UserKey = userKey };
            state.Records[userKey] = record;
        }

        record.ProcessedRaceIds ??= new List<string>();
        record.Keys ??= new Dictionary<string, KeyboardKeyProgress>(StringComparer.Ordinal);

        var raceKey = raceId > 0
            ? raceId.ToString()
            : $"{result.Username}:{result.TimeTakenMs}:{result.Wpm:F1}:{result.Accuracy:F1}:keyboard";
        var wasNewRace = !record.ProcessedRaceIds.Contains(raceKey, StringComparer.Ordinal);

        if (wasNewRace)
        {
            record.ProcessedRaceIds.Add(raceKey);
            record.TotalRaces++;
            record.LastRaceAt = DateTime.Now;

            foreach (var item in CountTypedKeys(result.TypedText))
            {
                var progress = GetOrCreate(record.Keys, item.Key);
                progress.Attempts += item.Value;
            }

            foreach (var item in result.ObservedMistakeCharacters)
            {
                var label = NormalizeKeyLabel(item.Key);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var amount = Math.Clamp(item.Value, 0, 999);
                var progress = GetOrCreate(record.Keys, label);
                progress.Mistakes += amount;
                progress.Attempts = Math.Max(progress.Attempts, progress.Mistakes);
            }

            if (record.ProcessedRaceIds.Count > MaxProcessedRaceIds)
                record.ProcessedRaceIds = record.ProcessedRaceIds.TakeLast(MaxProcessedRaceIds).ToList();

            Save(state);
        }

        return BuildSnapshot(record, wasNewRace);
    }

    private static KeyboardMasteryProgressSnapshot BuildSnapshot(KeyboardMasteryRecord record, bool wasNewRace)
    {
        var scoredKeys = (record.Keys ?? new Dictionary<string, KeyboardKeyProgress>(StringComparer.Ordinal))
            .Where(x => x.Value.Attempts > 0 || x.Value.Mistakes > 0)
            .Select(x => BuildKeyScore(x.Key, x.Value))
            .OrderBy(x => x.Label, StringComparer.Ordinal)
            .ToList();

        var masteredKeys = scoredKeys
            .Where(x => MasteryTargetSet.Contains(x.Label) &&
                        x.Attempts >= MasteredAttemptThreshold &&
                        x.Accuracy >= MasteredAccuracyThreshold)
            .ToList();

        var reviewKeys = scoredKeys
            .Where(x => x.Mistakes >= 2 || x.Accuracy < ReviewAccuracyThreshold)
            .OrderBy(x => x.Accuracy)
            .ThenByDescending(x => x.Mistakes)
            .ThenByDescending(x => x.Attempts)
            .Take(6)
            .ToList();

        var denominator = scoredKeys.Sum(x => x.Attempts);
        var mistakes = scoredKeys.Sum(x => Math.Min(x.Mistakes, x.Attempts));
        var averageAccuracy = denominator > 0
            ? Math.Clamp((decimal)(denominator - mistakes) / denominator * 100m, 0m, 100m)
            : 0m;

        return new KeyboardMasteryProgressSnapshot
        {
            TotalRaces = record.TotalRaces,
            WasNewRace = wasNewRace,
            TrackedKeyCount = scoredKeys.Count,
            MasteredKeyCount = masteredKeys.Count,
            MasteryTargetKeyCount = MasteryTargetKeys.Length,
            CoveragePercent = Math.Clamp((decimal)masteredKeys.Count / MasteryTargetKeys.Length * 100m, 0m, 100m),
            AverageAccuracy = Math.Round(averageAccuracy, 1, MidpointRounding.AwayFromZero),
            NeedsReviewCount = reviewKeys.Count,
            StrongestKeys = scoredKeys
                .Where(x => x.Attempts >= MasteredAttemptThreshold)
                .OrderByDescending(x => x.Accuracy)
                .ThenByDescending(x => x.Attempts)
                .Take(6)
                .Select(FormatKeyScore)
                .ToList(),
            WeakestKeys = scoredKeys
                .OrderBy(x => x.Accuracy)
                .ThenByDescending(x => x.Mistakes)
                .ThenByDescending(x => x.Attempts)
                .Take(6)
                .Select(FormatKeyScore)
                .ToList(),
            NeedsReviewKeys = reviewKeys
                .Select(x => FormatKeyLabel(x.Label))
                .ToList(),
        };
    }

    private static Dictionary<string, int> CountTypedKeys(string typedText)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in typedText)
        {
            var label = NormalizeKeyLabel(value.ToString());
            if (string.IsNullOrWhiteSpace(label))
                continue;

            counts[label] = counts.GetValueOrDefault(label) + 1;
        }

        return counts;
    }

    private static KeyboardKeyScore BuildKeyScore(string label, KeyboardKeyProgress progress)
    {
        var attempts = Math.Max(1, Math.Max(progress.Attempts, progress.Mistakes));
        var mistakes = Math.Clamp(progress.Mistakes, 0, attempts);
        var accuracy = Math.Clamp((decimal)(attempts - mistakes) / attempts * 100m, 0m, 100m);
        return new KeyboardKeyScore(label, attempts, mistakes, Math.Round(accuracy, 1, MidpointRounding.AwayFromZero));
    }

    private static string FormatKeyScore(KeyboardKeyScore score)
        => $"{FormatKeyLabel(score.Label)} {score.Accuracy:F0}%";

    private static string FormatKeyLabel(string label)
        => string.Equals(label, "space", StringComparison.Ordinal) ? "Space" : label.ToUpperInvariant();

    private static KeyboardKeyProgress GetOrCreate(Dictionary<string, KeyboardKeyProgress> keys, string label)
    {
        if (!keys.TryGetValue(label, out var progress))
        {
            progress = new KeyboardKeyProgress();
            keys[label] = progress;
        }

        return progress;
    }

    private static string NormalizeKeyLabel(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length == 0 && value.Any(char.IsWhiteSpace))
            return "space";

        if (string.Equals(trimmed, "space", StringComparison.OrdinalIgnoreCase))
            return "space";

        var first = trimmed.Length > 0 ? trimmed[0] : value[0];
        if (char.IsWhiteSpace(first))
            return "space";
        if (char.IsControl(first))
            return string.Empty;

        var normalized = first.ToString().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        var key = builder.Length > 0 ? builder[0] : first;
        return char.ToLowerInvariant(key).ToString();
    }

    private static KeyboardMasteryProgressFile Load()
    {
        try
        {
            var path = GetStorePath();
            if (!File.Exists(path))
                return new KeyboardMasteryProgressFile();

            var state = JsonSerializer.Deserialize<KeyboardMasteryProgressFile>(File.ReadAllText(path), JsonOptions)
                ?? new KeyboardMasteryProgressFile();
            state.Records ??= new Dictionary<string, KeyboardMasteryRecord>(StringComparer.Ordinal);
            foreach (var record in state.Records.Values)
            {
                record.ProcessedRaceIds ??= new List<string>();
                record.Keys ??= new Dictionary<string, KeyboardKeyProgress>(StringComparer.Ordinal);
            }

            return state;
        }
        catch
        {
            return new KeyboardMasteryProgressFile();
        }
    }

    private static void Save(KeyboardMasteryProgressFile state)
    {
        try
        {
            var path = GetStorePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Keyboard mastery is a local progress hint; result display must not depend on disk I/O.
        }
    }

    private static string GetStorePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = AppContext.BaseDirectory;
        return Path.Combine(root, "TypeRacer", "keyboard-mastery.json");
    }

    private sealed class KeyboardMasteryProgressFile
    {
        public Dictionary<string, KeyboardMasteryRecord> Records { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class KeyboardMasteryRecord
    {
        public string UserKey { get; set; } = string.Empty;
        public int TotalRaces { get; set; }
        public DateTime LastRaceAt { get; set; }
        public Dictionary<string, KeyboardKeyProgress>? Keys { get; set; } = new(StringComparer.Ordinal);
        public List<string>? ProcessedRaceIds { get; set; } = new();
    }

    private sealed class KeyboardKeyProgress
    {
        public int Attempts { get; set; }
        public int Mistakes { get; set; }
    }

    private sealed record KeyboardKeyScore(string Label, int Attempts, int Mistakes, decimal Accuracy);
}
