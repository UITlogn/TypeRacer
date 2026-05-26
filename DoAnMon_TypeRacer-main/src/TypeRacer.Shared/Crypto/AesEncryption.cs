using System.Security.Cryptography;
using System.Text;

namespace TypeRacer.Shared.Crypto;

/// <summary>
/// AES-256-CBC encryption/decryption using a shared key.
/// Used to encrypt TCP message payloads on the wire.
/// </summary>
public static class AesEncryption
{
    private const int AesBlockSizeBytes = 16;
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes(Constants.SharedAesKey));

    public static byte[] Encrypt(byte[] plainData)
    {
        if (plainData.Length == 0)
            return Array.Empty<byte>();

        var iv = RandomNumberGenerator.GetBytes(AesBlockSizeBytes);
        var cipher = Transform(plainData, iv, encrypt: true);
        var output = new byte[iv.Length + cipher.Length];
        Buffer.BlockCopy(iv, 0, output, 0, iv.Length);
        Buffer.BlockCopy(cipher, 0, output, iv.Length, cipher.Length);
        return output;
    }

    public static byte[] Decrypt(byte[] cipherData)
    {
        if (cipherData.Length == 0)
            return Array.Empty<byte>();

        if (cipherData.Length > AesBlockSizeBytes &&
            (cipherData.Length - AesBlockSizeBytes) % AesBlockSizeBytes == 0)
        {
            try
            {
                var iv = cipherData[..AesBlockSizeBytes];
                var cipher = cipherData[AesBlockSizeBytes..];
                return Transform(cipher, iv, encrypt: false);
            }
            catch (CryptographicException)
            {
                // Fall through to legacy static-IV format for older captures.
            }
        }

        return Transform(cipherData, GetLegacyIv(), encrypt: false);
    }

    private static byte[] Transform(byte[] input, byte[] iv, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var transformer = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transformer.TransformFinalBlock(input, 0, input.Length);
    }

    private static byte[] GetLegacyIv()
    {
        var iv = Encoding.UTF8.GetBytes(Constants.SharedAesIV);
        if (iv.Length == AesBlockSizeBytes)
            return iv;

        return SHA256.HashData(iv)[..AesBlockSizeBytes];
    }

    public static string EncryptString(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = Encrypt(plainBytes);
        return Convert.ToBase64String(encrypted);
    }

    public static string DecryptString(string cipherBase64)
    {
        var cipherBytes = Convert.FromBase64String(cipherBase64);
        var decrypted = Decrypt(cipherBytes);
        return Encoding.UTF8.GetString(decrypted);
    }
}
