using TypeRacer.Server.State;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Network;

/// <summary>
/// Thread-safe message sending for a connected client.
/// Uses SemaphoreSlim to prevent concurrent writes to the same stream.
/// </summary>
public class ClientSession
{
    private readonly ConnectedClient _client;

    public ClientSession(ConnectedClient client)
    {
        _client = client;
    }

    public async Task SendAsync(NetworkMessage message, CancellationToken ct = default)
    {
        await _client.SendLock.WaitAsync(ct);
        try
        {
            if (_client.TcpClient.Connected)
            {
                var outbound = message;
                if (_client.UseEncryptedMessages && message.Body.Length > 0)
                {
                    outbound = new NetworkMessage
                    {
                        Type = message.Type,
                        Flags = message.Flags | MessageFlags.Encrypted,
                        Body = message.Body,
                    };
                }

                await MessageSerializer.WriteAsync(_client.Stream, outbound, ct);
            }
        }
        finally
        {
            _client.SendLock.Release();
        }
    }

    public async Task SendAsync<T>(MessageType type, T payload, CancellationToken ct = default)
    {
        var message = NetworkMessage.Create(type, payload);
        await SendAsync(message, ct);
    }

    public async Task SendErrorAsync(Shared.Enums.ErrorCode code, string errorMessage, CancellationToken ct = default)
    {
        var payload = new Shared.Payloads.System.ErrorPayload
        {
            Code = code,
            Message = errorMessage,
        };
        await SendAsync(MessageType.ERROR, payload, ct);
    }
}
