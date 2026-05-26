using TypeRacer.Shared.Models;

namespace TypeRacer.Server.Data;

public sealed class InMemoryDataStore
{
    private readonly object _lock = new();
    private int _nextUserId = 1;
    private int _nextRoomId = 1;
    private int _nextRaceId = 1;
    private int _nextPassageId = 1;

    public List<UserRow> Users { get; } = new();
    public Dictionary<string, int> Rooms { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, RaceRecord> Races { get; } = new();
    public List<RaceResultRecord> Results { get; } = new();
    public List<PassageRow> Passages { get; } = new();

    public InMemoryDataStore()
    {
        SeedPassages();
    }

    public object SyncRoot => _lock;

    public int NextUserId() => _nextUserId++;
    public int NextRoomId() => _nextRoomId++;
    public int NextRaceId() => _nextRaceId++;
    public int NextPassageId() => _nextPassageId++;

    private void SeedPassages()
    {
        var seeds = new (string Text, string Lang)[]
        {
            ("Lập trình mạng giúp các ứng dụng trao đổi dữ liệu qua socket, xử lý nhiều client và phản hồi theo thời gian thực.", "vi"),
            ("Người chơi giữ nhịp gõ ổn định, quan sát trước vài ký tự và hạn chế sửa lỗi quá muộn trong cuộc đua.", "vi"),
            ("Một hệ thống tốt cần có giao thức rõ ràng, kiểm thử biên, xử lý timeout và lưu lại kết quả đáng tin cậy.", "vi"),
            ("Quick play community rooms make it easy for players to join a shared race without creating a private lobby first.", "en"),
            ("Reliable network games validate every client update, broadcast progress often, and recover gracefully from disconnects.", "en"),
            ("Typing practice improves fastest when each mistake becomes a focused drill for the next short training session.", "en"),
        };

        foreach (var seed in seeds)
        {
            Passages.Add(new PassageRow
            {
                Id = NextPassageId(),
                Content = seed.Text,
                Language = seed.Lang,
            });
        }
    }
}

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryUserRepository(InMemoryDataStore store) => _store = store;

    public Task<UserRow?> GetByUsernameAsync(string username)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(_store.Users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<UserRow?> GetByIdAsync(int id)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(_store.Users.FirstOrDefault(u => u.Id == id));
        }
    }

    public Task<bool> UsernameExistsAsync(string username)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(_store.Users.Any(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<int> CreateAsync(string username, string passwordHash)
    {
        lock (_store.SyncRoot)
        {
            if (_store.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new DuplicateUsernameException(username);

            var id = _store.NextUserId();
            _store.Users.Add(new UserRow { Id = id, Username = username, PasswordHash = passwordHash });
            return Task.FromResult(id);
        }
    }

    public Task UpdatePasswordHashAsync(int userId, string passwordHash)
    {
        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
                user.PasswordHash = passwordHash;
        }

        return Task.CompletedTask;
    }

    public Task<List<LeaderboardRow>> GetLeaderboardAsync(int top, string sortBy)
    {
        lock (_store.SyncRoot)
        {
            var rows = _store.Results
                .Where(r => !r.IsAiBot && r.UserId > 0)
                .GroupBy(r => r.UserId)
                .Select(g =>
                {
                    var user = _store.Users.FirstOrDefault(u => u.Id == g.Key);
                    return new LeaderboardRow
                    {
                        UserId = g.Key,
                        Username = user?.Username ?? $"user_{g.Key}",
                        TotalRaces = g.Count(),
                        TotalWins = g.Count(r => r.Position == 1),
                        AvgWpm = decimal.Round(g.Average(r => r.Wpm), 2),
                        BestWpm = g.Max(r => r.Wpm),
                    };
                });

            rows = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "wins" => rows.OrderByDescending(r => r.TotalWins).ThenByDescending(r => r.BestWpm),
                "avg" or "average" => rows.OrderByDescending(r => r.AvgWpm).ThenByDescending(r => r.BestWpm),
                _ => rows.OrderByDescending(r => r.BestWpm).ThenByDescending(r => r.AvgWpm),
            };

            return Task.FromResult(rows.Take(Math.Clamp(top, 1, 100)).ToList());
        }
    }
}

public sealed class InMemoryRaceRepository : IRaceRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryRaceRepository(InMemoryDataStore store) => _store = store;

    public Task<int> GetOrCreateRoomAsync(string roomCode, int hostId)
    {
        lock (_store.SyncRoot)
        {
            if (_store.Rooms.TryGetValue(roomCode, out var existing))
                return Task.FromResult(existing);

            var id = _store.NextRoomId();
            _store.Rooms[roomCode] = id;
            return Task.FromResult(id);
        }
    }

    public Task UpdateRoomHostAsync(string roomCode, int hostId) => Task.CompletedTask;

    public Task<int> CreateRaceAsync(int roomId, string roomCode, int passageId, DateTime startedAt)
    {
        lock (_store.SyncRoot)
        {
            var id = _store.NextRaceId();
            _store.Races[id] = new RaceRecord
            {
                RaceId = id,
                RoomCode = roomCode,
                PassageId = passageId,
                StartedAt = startedAt,
            };
            return Task.FromResult(id);
        }
    }

    public Task EndRaceAsync(int raceId, DateTime endedAt)
    {
        lock (_store.SyncRoot)
        {
            if (_store.Races.TryGetValue(raceId, out var race))
                race.EndedAt = endedAt;
        }

        return Task.CompletedTask;
    }

    public Task InsertResultAsync(int raceId, string roomCode, RaceResultDto result, int totalPlayers, DateTime playedAt)
    {
        lock (_store.SyncRoot)
        {
            _store.Results.RemoveAll(r => r.RaceId == raceId && r.UserId == result.UserId);
            _store.Results.Add(new RaceResultRecord
            {
                RaceId = raceId,
                RoomCode = roomCode,
                UserId = result.UserId,
                Position = result.Position,
                TotalPlayers = totalPlayers,
                Wpm = result.Wpm,
                Accuracy = result.Accuracy,
                TimeTakenMs = result.TimeTakenMs,
                IsCompleted = result.IsCompleted,
                IsAiBot = result.IsAiBot,
                PlayedAt = playedAt,
            });
        }

        return Task.CompletedTask;
    }

    public Task<List<MatchHistoryRow>> GetMatchHistoryAsync(int userId, int limit)
    {
        lock (_store.SyncRoot)
        {
            var rows = _store.Results
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.PlayedAt)
                .Take(Math.Clamp(limit, 1, 100))
                .Select(r => new MatchHistoryRow
                {
                    RaceId = r.RaceId,
                    RoomCode = r.RoomCode,
                    Position = r.Position,
                    TotalPlayers = r.TotalPlayers,
                    Wpm = r.Wpm,
                    Accuracy = r.Accuracy,
                    TimeTakenMs = r.TimeTakenMs,
                    IsCompleted = r.IsCompleted,
                    PlayedAt = r.PlayedAt,
                })
                .ToList();

            return Task.FromResult(rows);
        }
    }
}

public sealed class InMemoryPassageRepository : IPassageRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryPassageRepository(InMemoryDataStore store) => _store = store;

    public Task<PassageRow?> GetRandomAsync(string language = "any", IReadOnlyCollection<int>? excludePassageIds = null)
    {
        lock (_store.SyncRoot)
        {
            var normalized = NormalizeLanguage(language);
            var excluded = (excludePassageIds ?? Array.Empty<int>()).ToHashSet();
            var query = _store.Passages
                .Where(p => !excluded.Contains(p.Id))
                .Where(p => normalized == "any" || p.Language == normalized)
                .ToList();

            if (query.Count == 0)
                query = _store.Passages.Where(p => normalized == "any" || p.Language == normalized).ToList();
            if (query.Count == 0)
                query = _store.Passages.ToList();

            return Task.FromResult(query.Count == 0 ? null : query[Random.Shared.Next(query.Count)]);
        }
    }

    public Task<PassageRow?> GetByIdAsync(int id)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(_store.Passages.FirstOrDefault(p => p.Id == id));
        }
    }

    public Task<int> GetOrCreateByContentAsync(string content, string language = "en")
    {
        lock (_store.SyncRoot)
        {
            var normalized = NormalizeLanguage(language);
            var existing = _store.Passages.FirstOrDefault(p =>
                string.Equals(p.Content, content, StringComparison.Ordinal) &&
                p.Language == normalized);
            if (existing != null)
                return Task.FromResult(existing.Id);

            var id = _store.NextPassageId();
            _store.Passages.Add(new PassageRow { Id = id, Content = content, Language = normalized });
            return Task.FromResult(id);
        }
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            "any" => "any",
            _ => "en",
        };
    }
}

public sealed class InMemoryChatRepository : IChatRepository
{
    public Task SaveAsync(string roomCode, int userId, string username, string content, DateTime sentAt)
        => Task.CompletedTask;
}

public sealed class RaceRecord
{
    public int RaceId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int PassageId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class RaceResultRecord
{
    public int RaceId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int Position { get; set; }
    public int TotalPlayers { get; set; }
    public decimal Wpm { get; set; }
    public decimal Accuracy { get; set; }
    public int TimeTakenMs { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsAiBot { get; set; }
    public DateTime PlayedAt { get; set; }
}
