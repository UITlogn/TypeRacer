using System.Buffers.Binary;
using TypeRacer.Shared.Crypto;

namespace TypeRacer.Shared.Protocol;

/// <summary>
/// Writes a NetworkMessage to a Stream with the binary header protocol.
/// Thread-safe when used with external synchronization (e.g., SemaphoreSlim).
/// </summary>
public static class MessageSerializer
{
    public static async Task WriteAsync(Stream stream, NetworkMessage message, CancellationToken ct = default)
    {
        var body = message.Body;
        var flags = message.Flags;

        // Encrypt body if flag is set
        if (flags.HasFlag(MessageFlags.Encrypted) && body.Length > 0)
        {
            body = AesEncryption.Encrypt(body);
        }

        // Build header: [BodyLength:4][Type:2][Flags:2]
        var header = new byte[Constants.HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), body.Length);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), (ushort)message.Type);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6, 2), (ushort)flags);

        // Write header + body
        await stream.WriteAsync(header, ct);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, ct);
        }
        await stream.FlushAsync(ct);
    }
}
