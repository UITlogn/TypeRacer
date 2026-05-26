using Microsoft.Data.SqlClient;

namespace TypeRacer.Server.Data;

public class UserRepository : IUserRepository
{
    private readonly DatabaseManager _db;

    public UserRepository(DatabaseManager db)
    {
        _db = db;
    }

    public async Task<UserRow?> GetByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT id, username, password_hash FROM users WHERE username = @u", conn);
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new UserRow { Id = reader.GetInt32(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2) };
        return null;
    }

    public async Task<UserRow?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT id, username, password_hash FROM users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new UserRow { Id = reader.GetInt32(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2) };
        return null;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT COUNT(1) FROM users WHERE username = @u", conn);
        cmd.Parameters.AddWithValue("@u", username);
        var count = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(count ?? 0) > 0;
    }

    public async Task<int> CreateAsync(string username, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("INSERT INTO users (username, password_hash) OUTPUT INSERTED.id VALUES (@u, @h)", conn);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@h", passwordHash);

        try
        {
            var insertedId = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(insertedId ?? throw new InvalidOperationException("Failed to create user."));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateUsernameException(username, ex);
        }
    }

    public async Task UpdatePasswordHashAsync(int userId, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE users SET password_hash = @h WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@h", passwordHash);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Lấy leaderboard bằng cách tính từ race_results</summary>
    public async Task<List<LeaderboardRow>> GetLeaderboardAsync(int top, string sortBy)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        var orderBy = string.Equals(sortBy, "total_wins", StringComparison.OrdinalIgnoreCase)
            ? "total_wins DESC, avg_wpm DESC, best_wpm DESC"
            : "avg_wpm DESC, best_wpm DESC, total_wins DESC";
        const string sql = @"
            SELECT TOP (@top) u.id, u.username,
                   COUNT(rr.id) AS total_races,
                   SUM(CASE WHEN rr.position = 1 THEN 1 ELSE 0 END) AS total_wins,
                   AVG(rr.wpm) AS avg_wpm,
                   MAX(rr.wpm) AS best_wpm
            FROM users u
            INNER JOIN race_results rr ON u.id = rr.user_id
            WHERE rr.is_completed = 1
            GROUP BY u.id, u.username
            ORDER BY __ORDER_BY__";
        using var cmd = new SqlCommand(sql.Replace("__ORDER_BY__", orderBy), conn);
        cmd.Parameters.AddWithValue("@top", top);
        var results = new List<LeaderboardRow>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new LeaderboardRow
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                TotalRaces = reader.GetInt32(2),
                TotalWins = reader.GetInt32(3),
                AvgWpm = reader.GetDecimal(4),
                BestWpm = reader.GetDecimal(5),
            });
        }
        return results;
    }
}
