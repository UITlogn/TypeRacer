using System.Buffers.Binary;
using TypeRacer.Shared.Crypto;

namespace TypeRacer.Shared.Protocol;

/// <summary>
/// Reads NetworkMessage from a Stream, handling TCP partial reads.
/// </summary>
public static class MessageReader
{
    public static async Task<NetworkMessage?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        // Read header (8 bytes)
        var header = new byte[Constants.HeaderSize];
        if (!await ReadExactAsync(stream, header, ct))
            return null; // Connection closed

        var bodyLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
        var type = (MessageType)BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        var flags = (MessageFlags)BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6, 2));

        // Validate body length
        if (bodyLength < 0 || bodyLength > Constants.MaxMessageSize)
            throw new InvalidDataException($"Invalid message body length: {bodyLength}");

        // Read body
        var body = Array.Empty<byte>();
        if (bodyLength > 0)
        {
            body = new byte[bodyLength];
            if (!await ReadExactAsync(stream, body, ct))
                return null; // Connection closed mid-message

            // Decrypt if encrypted
            if (flags.HasFlag(MessageFlags.Encrypted))
            {
                body = AesEncryption.Decrypt(body);
            }
        }

        return new NetworkMessage
        {
            Type = type,
            Flags = flags,
            Body = body,
        };
    }

    /// <summary>
    /// Reads exactly buffer.Length bytes from stream, handling partial reads.
    /// Returns false if the connection was closed before all bytes were read.
    /// </summary>
    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);

            if (bytesRead == 0)
                return false; // Connection closed

            totalRead += bytesRead;
        }
        return true;
    }
}
