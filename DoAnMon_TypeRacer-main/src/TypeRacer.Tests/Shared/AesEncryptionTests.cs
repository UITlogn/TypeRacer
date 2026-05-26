using System.Security.Cryptography;
using System.Text;
using TypeRacer.Shared.Crypto;

namespace TypeRacer.Tests.Shared;

public class AesEncryptionTests
{
    [Fact]
    public void EncryptString_RoundTripsUnicodePayload()
    {
        var plain = "Xin chào TypeRacer AI - luyện lỗi dấu tiếng Việt 123!";

        var cipher = AesEncryption.EncryptString(plain);
        var decrypted = AesEncryption.DecryptString(cipher);

        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Encrypt_UsesRandomIvForSamePlaintext()
    {
        var plain = Encoding.UTF8.GetBytes("same payload should not reuse IV");

        var first = AesEncryption.Encrypt(plain);
        var second = AesEncryption.Encrypt(plain);

        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
        Assert.Equal(plain, AesEncryption.Decrypt(first));
        Assert.Equal(plain, AesEncryption.Decrypt(second));
    }

    [Fact]
    public void Decrypt_RejectsTamperedCiphertext()
    {
        var cipher = AesEncryption.Encrypt(Encoding.UTF8.GetBytes("protected payload"));
        cipher[^1] ^= 0x7F;

        Assert.Throws<CryptographicException>(() => AesEncryption.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_EmptyPayloadStaysEmpty()
    {
        Assert.Empty(AesEncryption.Encrypt(Array.Empty<byte>()));
        Assert.Empty(AesEncryption.Decrypt(Array.Empty<byte>()));
    }
}
