using Microsoft.Data.SqlClient;
using System.Text;

namespace TypeRacer.Server.Data;

public interface IPassageRepository
{
    Task<PassageRow?> GetRandomAsync(string language = "any", IReadOnlyCollection<int>? excludePassageIds = null);
    Task<PassageRow?> GetByIdAsync(int id);
    Task<int> GetOrCreateByContentAsync(string content, string language = "en");
}

public class SqlPassageRepository : IPassageRepository
{
    private readonly DatabaseManager _db;

    public SqlPassageRepository(DatabaseManager db)
    {
        _db = db;
    }

    /// <summary>Lấy ngẫu nhiên 1 đoạn văn (có thể lọc theo language và danh sách id cần loại trừ)</summary>
    public async Task<PassageRow?> GetRandomAsync(string language = "any", IReadOnlyCollection<int>? excludePassageIds = null)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var normalizedLanguage = NormalizeLanguage(language);
        var excludedIds = (excludePassageIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalizedLanguage == "vi")
        {
            var preferred = await TryGetRandomAsync(conn, normalizedLanguage, excludedIds, requireVietnameseDiacritics: true);
            if (preferred != null)
                return preferred;
        }

        return await TryGetRandomAsync(conn, normalizedLanguage, excludedIds, requireVietnameseDiacritics: false);
    }

    /// <summary>Lấy đoạn văn theo ID</summary>
    public async Task<PassageRow?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand("SELECT id, content, language FROM passages WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PassageRow
            {
                Id = reader.GetInt32(0),
                Content = reader.GetString(1),
                Language = reader.IsDBNull(2) ? "en" : NormalizeLanguage(reader.GetString(2)),
            };
        }
        return null;
    }

    public async Task<int> GetOrCreateByContentAsync(string content, string language = "en")
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var normalizedLanguage = NormalizeLanguage(language);

        using var selectCmd = new SqlCommand(
            "SELECT TOP 1 id FROM passages WHERE content = @content AND language = @language", conn);
        selectCmd.Parameters.AddWithValue("@content", content);
        selectCmd.Parameters.AddWithValue("@language", normalizedLanguage);
        var existingId = await selectCmd.ExecuteScalarAsync();
        if (existingId != null && existingId != DBNull.Value)
            return Convert.ToInt32(existingId);

        using var insertCmd = new SqlCommand(
            "INSERT INTO passages (content, language) OUTPUT INSERTED.id VALUES (@content, @language)", conn);
        insertCmd.Parameters.AddWithValue("@content", content);
        insertCmd.Parameters.AddWithValue("@language", normalizedLanguage);
        return Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
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

    private static async Task<PassageRow?> TryGetRandomAsync(
        SqlConnection conn,
        string normalizedLanguage,
        IReadOnlyCollection<int> excludedIds,
        bool requireVietnameseDiacritics)
    {
        var take = requireVietnameseDiacritics && normalizedLanguage == "vi" ? 200 : 1;
        var sql = new StringBuilder($"SELECT TOP {take} id, content, language FROM passages WHERE 1=1");
        using var cmd = new SqlCommand { Connection = conn };

        if (normalizedLanguage != "any")
        {
            sql.Append(" AND language = @language");
            cmd.Parameters.AddWithValue("@language", normalizedLanguage);
        }

        if (excludedIds.Count > 0)
        {
            var placeholders = new List<string>(excludedIds.Count);
            var i = 0;
            foreach (var id in excludedIds)
            {
                var paramName = $"@exclude{i++}";
                placeholders.Add(paramName);
                cmd.Parameters.AddWithValue(paramName, id);
            }
            sql.Append(" AND id NOT IN (");
            sql.Append(string.Join(", ", placeholders));
            sql.Append(')');
        }

        sql.Append(" ORDER BY NEWID()");
        cmd.CommandText = sql.ToString();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var content = reader.GetString(1);
            if (requireVietnameseDiacritics &&
                normalizedLanguage == "vi" &&
                !VietnameseTextHelper.ContainsVietnameseDiacritics(content))
            {
                continue;
            }

            return new PassageRow
            {
                Id = reader.GetInt32(0),
                Content = content,
                Language = reader.IsDBNull(2) ? "en" : NormalizeLanguage(reader.GetString(2)),
            };
        }

        return null;
    }
}

public class PassageRow
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
}
