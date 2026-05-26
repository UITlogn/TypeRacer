using System.Collections.Concurrent;
using TypeRacer.Shared.Enums;

namespace TypeRacer.Server.State;

public class GameRoom
{
    public string RoomCode { get; set; } = string.Empty;
    public int HostUserId { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public int DbRoomId { get; set; }  // ID trong database
    public string PassageLanguage { get; set; } = "en";
    public int RaceDurationSeconds { get; set; } = Shared.Constants.DefaultRaceDurationSeconds;
    public bool EnableAiMode { get; set; }
    public string GameMode { get; set; } = Shared.Constants.DefaultGameMode;
    public string AiPracticeDifficulty { get; set; } = Shared.Constants.DefaultAiPracticeDifficulty;
    public string CustomPassageText { get; set; } = string.Empty;
    public int LastPassageId { get; set; }
    public bool IsCommunityRoom { get; set; }
    public bool AllowJoinInProgress { get; set; }
    public int AutoStartIntervalSeconds { get; set; }
    public DateTime? NextAutoStartAtUtc { get; set; }

    public ConcurrentDictionary<int, PlayerState> Players { get; } = new();

    // Trạng thái trận đua
    public string? PassageText { get; set; }
    public int PassageId { get; set; }
    public string? LastPassageText { get; set; }
    public int? RaceId { get; set; }
    public DateTime? StartedAt { get; set; }
    public CancellationTokenSource? RaceTimeoutCts { get; set; }
    public CancellationTokenSource? AiBotCts { get; set; }

    public void SetPlayerTypedText(int userId, string typedText)
    {
        if (Players.TryGetValue(userId, out var player))
        {
            player.TypedText = typedText;
        }
    }

    /// <summary>Lock cho FinishRace — đảm bảo chỉ gọi 1 lần</summary>
    private int _finishLock;
    public bool TryClaimFinish() => Interlocked.CompareExchange(ref _finishLock, 1, 0) == 0;
    public void ResetFinishLock() => Interlocked.Exchange(ref _finishLock, 0);

    private readonly object _passageHistoryLock = new();
    private readonly HashSet<int> _usedPassageIds = new();

    public IReadOnlyCollection<int> GetUsedPassageIdsSnapshot()
    {
        lock (_passageHistoryLock)
        {
            return _usedPassageIds.ToList();
        }
    }

    public bool HasUsedPassage(int passageId)
    {
        if (passageId <= 0) return false;
        lock (_passageHistoryLock)
        {
            return _usedPassageIds.Contains(passageId);
        }
    }

    public bool HasAnyUsedPassages()
    {
        lock (_passageHistoryLock)
        {
            return _usedPassageIds.Count > 0;
        }
    }

    public void MarkPassageUsed(int passageId)
    {
        if (passageId <= 0) return;
        lock (_passageHistoryLock)
        {
            _usedPassageIds.Add(passageId);
        }
    }

    public void ResetPassageHistory()
    {
        lock (_passageHistoryLock)
        {
            _usedPassageIds.Clear();
        }
    }

    public void ResetAfterLastHumanLeaves()
    {
        foreach (var botUserId in Players.Values.Where(p => p.IsBot).Select(p => p.UserId).ToList())
            Players.TryRemove(botUserId, out _);

        RaceTimeoutCts?.Cancel();
        RaceTimeoutCts = null;
        AiBotCts?.Cancel();
        AiBotCts = null;
        HostUserId = 0;
        Status = RoomStatus.Waiting;
        PassageText = null;
        PassageId = 0;
        StartedAt = null;
        RaceId = null;
        ResetFinishLock();

        if (AutoStartIntervalSeconds > 0)
            NextAutoStartAtUtc ??= DateTime.UtcNow.AddSeconds(AutoStartIntervalSeconds);
    }
}

public class PlayerState
{
    private readonly object _mistakeLock = new();
    private readonly HashSet<string> _observedMistakeKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _observedMistakeCharacters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _observedMistakeWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _observedMistakeNgrams = new(StringComparer.OrdinalIgnoreCase);

    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public int CurrentPosition { get; set; }
    public int CorrectChars { get; set; }
    public int WrongChars { get; set; }
    public double CurrentWpm { get; set; }
    public double CurrentAccuracy { get; set; }
    public string TypedText { get; set; } = string.Empty;
    public int BackspaceCount { get; set; }
    public int BestStreak { get; set; }
    public decimal ConsistencyScore { get; set; }
    public bool IsDisqualified { get; set; }
    public List<string> Achievements { get; set; } = new();
    public bool IsBot { get; set; }
    public string AiPracticeDifficulty { get; set; } = Shared.Constants.DefaultAiPracticeDifficulty;
    public double AiTargetRpm { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFinished { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int TimeTakenMs { get; set; }

    public void ObserveMistakes(string passageText, string typedText)
    {
        if (string.IsNullOrWhiteSpace(passageText) || string.IsNullOrEmpty(typedText))
            return;

        lock (_mistakeLock)
        {
            var max = typedText.Length;
            for (var i = 0; i < max; i++)
            {
                var expected = i < passageText.Length ? passageText[i] : '\0';
                var actual = i < typedText.Length ? typedText[i] : '\0';
                if (expected == actual)
                    continue;

                var key = $"{i}:{expected}:{actual}";
                if (!_observedMistakeKeys.Add(key))
                    continue;

                AddMistakeCharacter(expected);
                AddMistakeCharacter(actual);

                var expectedWord = ExtractWordAt(passageText, i);
                if (!string.IsNullOrWhiteSpace(expectedWord))
                    Increment(_observedMistakeWords, expectedWord);

                foreach (var ngram in ExtractNgramsAt(passageText, i))
                    Increment(_observedMistakeNgrams, ngram);
            }
        }
    }

    public Dictionary<string, int> GetObservedMistakeCharactersSnapshot()
    {
        lock (_mistakeLock)
        {
            return _observedMistakeCharacters
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public Dictionary<string, int> GetObservedMistakeWordsSnapshot()
    {
        lock (_mistakeLock)
        {
            return _observedMistakeWords
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public Dictionary<string, int> GetObservedMistakeNgramsSnapshot()
    {
        lock (_mistakeLock)
        {
            return _observedMistakeNgrams
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void ClearObservedMistakes()
    {
        lock (_mistakeLock)
        {
            _observedMistakeKeys.Clear();
            _observedMistakeCharacters.Clear();
            _observedMistakeWords.Clear();
            _observedMistakeNgrams.Clear();
        }
    }

    private void AddMistakeCharacter(char value)
    {
        if (value == '\0' || char.IsControl(value) || char.IsWhiteSpace(value))
            return;

        Increment(_observedMistakeCharacters, value.ToString());
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private static string ExtractWordAt(string text, int index)
    {
        if (string.IsNullOrWhiteSpace(text) || index < 0 || index >= text.Length)
            return string.Empty;

        if (!IsWordChar(text[index]))
            return string.Empty;

        var start = index;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;

        var end = index;
        while (end + 1 < text.Length && IsWordChar(text[end + 1]))
            end++;

        return text[start..(end + 1)].Trim().ToLowerInvariant();
    }

    private static IEnumerable<string> ExtractNgramsAt(string text, int index)
    {
        var word = ExtractWordAt(text, index);
        if (word.Length < 2 || index < 0 || index >= text.Length || !IsWordChar(text[index]))
            return Enumerable.Empty<string>();

        var wordStart = index;
        while (wordStart > 0 && IsWordChar(text[wordStart - 1]))
            wordStart--;

        var localIndex = Math.Clamp(index - wordStart, 0, word.Length - 1);
        var ngrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var size = 2; size <= Math.Min(4, word.Length); size++)
        {
            var start = Math.Clamp(localIndex - size + 1, 0, word.Length - size);
            ngrams.Add(word.Substring(start, size));

            if (localIndex + size <= word.Length)
                ngrams.Add(word.Substring(localIndex, size));
        }

        return ngrams.Where(x => x.Length >= 2);
    }

    private static bool IsWordChar(char value)
        => char.IsLetterOrDigit(value) || value == '\'' || value == '-';
}
