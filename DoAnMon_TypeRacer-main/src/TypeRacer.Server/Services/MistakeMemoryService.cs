using System.Collections.Concurrent;
using TypeRacer.Shared.Models;

namespace TypeRacer.Server.Services;

public sealed class MistakeMemoryService
{
    private const int MaxTypedTextLength = 1_200;
    private const int MaxPassageTextLength = 1_200;
    private const int MaxSamples = 2_000;
    private static readonly TimeSpan SampleTtl = TimeSpan.FromMinutes(60);
    private readonly ConcurrentDictionary<string, MistakeMemorySample> _samples = new();

    public void RecordRaceResults(
        string roomCode,
        int raceId,
        string passageText,
        string gameMode,
        IEnumerable<RaceResultDto> results,
        DateTime playedAt)
    {
        PurgeExpired();

        var normalizedRoom = NormalizeRoomCode(roomCode);
        if (string.IsNullOrWhiteSpace(normalizedRoom) || raceId <= 0)
            return;

        var safePassage = Truncate(passageText, MaxPassageTextLength);
        foreach (var result in results.Where(r => !r.IsAiBot && r.UserId > 0))
        {
            var typedText = Truncate(result.TypedText, MaxTypedTextLength);
            var observedChars = TrimCounts(result.ObservedMistakeCharacters, 12);
            var observedWords = TrimCounts(result.ObservedMistakeWords, 12);
            var observedNgrams = TrimCounts(result.ObservedMistakeNgrams, 16);
            if (string.IsNullOrWhiteSpace(typedText) &&
                observedChars.Count == 0 &&
                observedWords.Count == 0 &&
                observedNgrams.Count == 0)
            {
                continue;
            }

            var key = BuildKey(normalizedRoom, raceId, result.UserId);
            _samples[key] = new MistakeMemorySample
            {
                RaceId = raceId,
                RoomCode = normalizedRoom,
                UserId = result.UserId,
                Username = result.Username,
                PassageText = safePassage,
                TypedText = typedText,
                GameMode = gameMode,
                PlayedAt = playedAt,
                ObservedMistakeCharacters = observedChars,
                ObservedMistakeWords = observedWords,
                ObservedMistakeNgrams = observedNgrams,
            };
        }

        TrimIfNeeded();
    }

    public IReadOnlyList<MistakeMemorySample> GetSamples(int userId, string? roomCode, int raceId, int limit)
    {
        PurgeExpired();

        var normalizedRoom = NormalizeRoomCode(roomCode);
        var cappedLimit = Math.Clamp(limit, 1, 24);

        return _samples.Values
            .Select(sample => new
            {
                Sample = sample,
                Score =
                    (raceId > 0 && sample.RaceId == raceId ? 4 : 0) +
                    (!string.IsNullOrWhiteSpace(normalizedRoom) && sample.RoomCode == normalizedRoom ? 3 : 0) +
                    (sample.UserId == userId ? 2 : 0),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Sample.PlayedAt)
            .Select(x => x.Sample)
            .DistinctBy(x => $"{x.RoomCode}:{x.RaceId}:{x.UserId}")
            .Take(cappedLimit)
            .ToList();
    }

    public void ClearUserRoom(int userId, string? roomCode)
    {
        var normalizedRoom = NormalizeRoomCode(roomCode);
        foreach (var item in _samples)
        {
            if (item.Value.UserId == userId &&
                (string.IsNullOrWhiteSpace(normalizedRoom) || item.Value.RoomCode == normalizedRoom))
            {
                _samples.TryRemove(item.Key, out _);
            }
        }
    }

    public void ClearRoom(string? roomCode)
    {
        var normalizedRoom = NormalizeRoomCode(roomCode);
        if (string.IsNullOrWhiteSpace(normalizedRoom))
            return;

        foreach (var item in _samples)
        {
            if (item.Value.RoomCode == normalizedRoom)
                _samples.TryRemove(item.Key, out _);
        }
    }

    private void PurgeExpired()
    {
        var cutoff = DateTime.UtcNow - SampleTtl;
        foreach (var item in _samples)
        {
            if (item.Value.PlayedAt < cutoff)
                _samples.TryRemove(item.Key, out _);
        }
    }

    private void TrimIfNeeded()
    {
        if (_samples.Count <= MaxSamples)
            return;

        foreach (var item in _samples
            .OrderBy(x => x.Value.PlayedAt)
            .Take(Math.Max(1, _samples.Count - MaxSamples)))
        {
            _samples.TryRemove(item.Key, out _);
        }
    }

    private static string BuildKey(string roomCode, int raceId, int userId)
        => $"{roomCode}:{raceId}:{userId}";

    private static string NormalizeRoomCode(string? roomCode)
        => (roomCode ?? string.Empty).Trim().ToUpperInvariant();

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var compact = value.Trim();
        return compact.Length <= maxLength ? compact : compact[..maxLength];
    }

    private static Dictionary<string, int> TrimCounts(IReadOnlyDictionary<string, int>? counts, int limit)
    {
        if (counts == null || counts.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return counts
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(Math.Clamp(limit, 1, 50))
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class MistakeMemorySample
{
    public int RaceId { get; init; }
    public string RoomCode { get; init; } = string.Empty;
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PassageText { get; init; } = string.Empty;
    public string TypedText { get; init; } = string.Empty;
    public string GameMode { get; init; } = string.Empty;
    public DateTime PlayedAt { get; init; }
    public Dictionary<string, int> ObservedMistakeCharacters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ObservedMistakeWords { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ObservedMistakeNgrams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
