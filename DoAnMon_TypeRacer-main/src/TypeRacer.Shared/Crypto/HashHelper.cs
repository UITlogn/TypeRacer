using System.Security.Cryptography;
using System.Text;

namespace TypeRacer.Shared.Crypto;

public static class HashHelper
{
    private const string PasswordHashV2Prefix = "v2";
    private const int PasswordHashV2Iterations = 120_000;
    private const int PasswordHashV2KeySizeBytes = 32;

    /// <summary>Hash password đơn giản bằng SHA256 (không salt)</summary>
    public static string HashPasswordSimple(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Hash mật khẩu v2 (PBKDF2-HMACSHA256 + random salt), lưu ở dạng:
    /// v2$iterations$base64(salt)$base64(hash)
    /// </summary>
    public static string HashPasswordV2(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            PasswordHashV2Iterations,
            HashAlgorithmName.SHA256,
            PasswordHashV2KeySizeBytes);

        return $"{PasswordHashV2Prefix}${PasswordHashV2Iterations}${Convert.ToBase64String(saltBytes)}${Convert.ToBase64String(hashBytes)}";
    }

    /// <summary>
    /// Verify hash đã lưu trong DB.
    /// - Trả về true/false theo kết quả check mật khẩu
    /// - needsUpgrade=true khi hash cũ (SHA256) hợp lệ và nên migrate lên v2.
    /// </summary>
    public static bool VerifyStoredPassword(string password, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;

        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        if (storedHash.StartsWith($"{PasswordHashV2Prefix}$", StringComparison.Ordinal))
        {
            return VerifyV2(password, storedHash);
        }

        var legacyHash = HashPasswordSimple(password);
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legacyHash),
            Encoding.UTF8.GetBytes(storedHash));
        if (ok)
            needsUpgrade = true;

        return ok;
    }

    /// <summary>Hash password với salt (dùng khi bật feature crypto sau này)</summary>
    public static string GenerateSalt(int size = 32)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(size);
        return Convert.ToBase64String(saltBytes);
    }

    public static string HashPassword(string password, string salt)
    {
        var combined = Encoding.UTF8.GetBytes(password + salt);
        var hash = SHA256.HashData(combined);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string salt, string storedHash)
    {
        var computedHash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    /// <summary>Tạo session token ngẫu nhiên</summary>
    public static string GenerateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(Constants.SessionTokenLength);
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyV2(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 10_000 || iterations > 2_000_000)
            return false;

        try
        {
            var saltBytes = Convert.FromBase64String(parts[2]);
            var hashBytes = Convert.FromBase64String(parts[3]);
            var computed = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                iterations,
                HashAlgorithmName.SHA256,
                hashBytes.Length);
            return CryptographicOperations.FixedTimeEquals(computed, hashBytes);
        }
        catch
        {
            return false;
        }
    }
}
