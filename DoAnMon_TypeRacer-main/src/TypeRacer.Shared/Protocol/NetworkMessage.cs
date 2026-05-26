using System.Text.Json;

namespace TypeRacer.Shared.Protocol;

/// <summary>
/// Represents a message sent over the TCP connection.
/// Header: [Length:4][Type:2][Flags:2] + Body (JSON UTF-8)
/// </summary>
public class NetworkMessage
{
    public MessageType Type { get; set; }
    public MessageFlags Flags { get; set; }
    public byte[] Body { get; set; } = Array.Empty<byte>();

    public static NetworkMessage Create<T>(MessageType type, T payload, MessageFlags flags = MessageFlags.None)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions.Default);
        return new NetworkMessage
        {
            Type = type,
            Flags = flags,
            Body = json,
        };
    }

    public static NetworkMessage CreateEmpty(MessageType type, MessageFlags flags = MessageFlags.None)
    {
        return new NetworkMessage
        {
            Type = type,
            Flags = flags,
            Body = Array.Empty<byte>(),
        };
    }

    public T? GetPayload<T>()
    {
        if (Body.Length == 0)
            return default;
        return JsonSerializer.Deserialize<T>(Body, JsonOptions.Default);
    }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
