using TypeRacer.Shared.Models;

namespace TypeRacer.Server.Data;

public interface IUserRepository
{
    Task<UserRow?> GetByUsernameAsync(string username);
    Task<UserRow?> GetByIdAsync(int id);
    Task<bool> UsernameExistsAsync(string username);
    Task<int> CreateAsync(string username, string passwordHash);
    Task UpdatePasswordHashAsync(int userId, string passwordHash);
    Task<List<LeaderboardRow>> GetLeaderboardAsync(int top, string sortBy);
}

public interface IRaceRepository
{
    Task<int> GetOrCreateRoomAsync(string roomCode, int hostId);
    Task UpdateRoomHostAsync(string roomCode, int hostId);
    Task<int> CreateRaceAsync(int roomId, string roomCode, int passageId, DateTime startedAt);
    Task EndRaceAsync(int raceId, DateTime endedAt);
    Task InsertResultAsync(int raceId, string roomCode, RaceResultDto result, int totalPlayers, DateTime playedAt);
    Task<List<MatchHistoryRow>> GetMatchHistoryAsync(int userId, int limit);
}

public interface IChatRepository
{
    Task SaveAsync(string roomCode, int userId, string username, string content, DateTime sentAt);
}

public class UserRow
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class LeaderboardRow
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int TotalRaces { get; set; }
    public int TotalWins { get; set; }
    public decimal AvgWpm { get; set; }
    public decimal BestWpm { get; set; }
}

public class MatchHistoryRow
{
    public int RaceId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int Position { get; set; }
    public int TotalPlayers { get; set; }
    public decimal Wpm { get; set; }
    public decimal Accuracy { get; set; }
    public int TimeTakenMs { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime PlayedAt { get; set; }
}
