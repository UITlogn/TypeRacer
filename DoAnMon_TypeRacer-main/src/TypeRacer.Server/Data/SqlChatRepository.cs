using Microsoft.Data.SqlClient;

namespace TypeRacer.Server.Data;

public class SqlChatRepository : IChatRepository
{
    private readonly DatabaseManager _db;

    public SqlChatRepository(DatabaseManager db)
    {
        _db = db;
    }

    public async Task SaveAsync(string roomCode, int userId, string username, string content, DateTime sentAt)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        int? roomId = null;
        using (var roomCmd = new SqlCommand("SELECT TOP 1 id FROM rooms WHERE room_code = @code", conn))
        {
            roomCmd.Parameters.AddWithValue("@code", roomCode);
            var value = await roomCmd.ExecuteScalarAsync();
            if (value != null && value != DBNull.Value)
                roomId = Convert.ToInt32(value);
        }

        if (!roomId.HasValue)
            return;

        using var insertCmd = new SqlCommand(
            "INSERT INTO chat_messages (room_id, user_id, content, sent_at) VALUES (@roomId, @userId, @content, @sentAt)",
            conn);
        insertCmd.Parameters.AddWithValue("@roomId", roomId.Value);
        insertCmd.Parameters.AddWithValue("@userId", userId);
        insertCmd.Parameters.AddWithValue("@content", content);
        insertCmd.Parameters.AddWithValue("@sentAt", sentAt);
        await insertCmd.ExecuteNonQueryAsync();
    }
}
