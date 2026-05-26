using Microsoft.Data.SqlClient;
using TypeRacer.Shared.Models;

namespace TypeRacer.Server.Data;

public class RaceRepository : IRaceRepository
{
    private readonly DatabaseManager _db;

    public RaceRepository(DatabaseManager db)
    {
        _db = db;
    }

    public async Task<int> GetOrCreateRoomAsync(string roomCode, int hostId)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Kiểm tra room đã tồn tại chưa
        using var selectCmd = new SqlCommand("SELECT id FROM rooms WHERE room_code = @code", conn);
        selectCmd.Parameters.AddWithValue("@code", roomCode);
        var existing = await selectCmd.ExecuteScalarAsync();
        if (existing != null)
            return (int)existing;

        // Tạo mới
        using var insertCmd = new SqlCommand(
            "INSERT INTO rooms (room_code, host_id) OUTPUT INSERTED.id VALUES (@code, @hostId)", conn);
        insertCmd.Parameters.AddWithValue("@code", roomCode);
        insertCmd.Parameters.AddWithValue("@hostId", hostId);
        var insertedId = await insertCmd.ExecuteScalarAsync();
        return Convert.ToInt32(insertedId ?? throw new InvalidOperationException("Failed to create room."));
    }

    public async Task UpdateRoomHostAsync(string roomCode, int hostId)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand("UPDATE rooms SET host_id = @hostId WHERE room_code = @code", conn);
        cmd.Parameters.AddWithValue("@hostId", hostId);
        cmd.Parameters.AddWithValue("@code", roomCode);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CreateRaceAsync(int roomId, string roomCode, int passageId, DateTime startedAt)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO races (room_id, passage_id, started_at)
            OUTPUT INSERTED.id
            VALUES (@roomId, @passageId, @startedAt)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@roomId", roomId);
        cmd.Parameters.AddWithValue("@passageId", passageId);
        cmd.Parameters.AddWithValue("@startedAt", startedAt);

        var raceId = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(raceId ?? throw new InvalidOperationException("Failed to create race."));
    }

    public async Task EndRaceAsync(int raceId, DateTime endedAt)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand("UPDATE races SET ended_at = @endedAt WHERE id = @raceId", conn);
        cmd.Parameters.AddWithValue("@endedAt", endedAt);
        cmd.Parameters.AddWithValue("@raceId", raceId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertResultAsync(int raceId, string roomCode, RaceResultDto result, int totalPlayers, DateTime playedAt)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // DB schema uses DECIMAL(6,2) for wpm and DECIMAL(5,2) for accuracy.
        // Clamp values to avoid overflow if a client finishes unusually fast.
        var safeWpm = Math.Clamp(result.Wpm, 0m, 9999.99m);
        var safeAccuracy = Math.Clamp(result.Accuracy, 0m, 100m);

        const string sql = @"
            INSERT INTO race_results (race_id, user_id, wpm, accuracy, position,
                                       chars_correct, chars_wrong, time_taken_ms, is_completed)
            VALUES (@raceId, @userId, @wpm, @accuracy, @position,
                    @charsCorrect, @charsWrong, @timeTakenMs, @isCompleted)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@raceId", raceId);
        cmd.Parameters.AddWithValue("@userId", result.UserId);
        cmd.Parameters.AddWithValue("@wpm", safeWpm);
        cmd.Parameters.AddWithValue("@accuracy", safeAccuracy);
        cmd.Parameters.AddWithValue("@position", result.Position);
        cmd.Parameters.AddWithValue("@charsCorrect", result.CharsCorrect);
        cmd.Parameters.AddWithValue("@charsWrong", result.CharsWrong);
        cmd.Parameters.AddWithValue("@timeTakenMs", result.TimeTakenMs);
        cmd.Parameters.AddWithValue("@isCompleted", result.IsCompleted);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<MatchHistoryRow>> GetMatchHistoryAsync(int userId, int limit)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            SELECT TOP (@limit)
                r.id AS race_id, rm.room_code, rr.position,
                (SELECT COUNT(*) FROM race_results WHERE race_id = r.id) AS total_players,
                rr.wpm, rr.accuracy, rr.time_taken_ms, rr.is_completed, r.started_at
            FROM race_results rr
            INNER JOIN races r ON rr.race_id = r.id
            INNER JOIN rooms rm ON r.room_id = rm.id
            WHERE rr.user_id = @userId
            ORDER BY r.started_at DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<MatchHistoryRow>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new MatchHistoryRow
            {
                RaceId = reader.GetInt32(0),
                RoomCode = reader.GetString(1),
                Position = reader.GetByte(2),
                TotalPlayers = reader.GetInt32(3),
                Wpm = reader.GetDecimal(4),
                Accuracy = reader.GetDecimal(5),
                TimeTakenMs = reader.GetInt32(6),
                IsCompleted = reader.GetBoolean(7),
                PlayedAt = reader.GetDateTime(8),
            });
        }
        return results;
    }
}
