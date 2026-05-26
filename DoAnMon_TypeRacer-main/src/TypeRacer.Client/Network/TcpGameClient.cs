using System.Net.Sockets;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Network;

/// <summary>
/// Kết nối TCP tới server, gửi/nhận message.
/// Chạy vòng lặp nhận message trên thread riêng.
/// </summary>
public class TcpGameClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsConnected => _tcpClient?.Connected == true;
    public bool EncryptPayloads { get; set; } = true;

    /// <summary>Sự kiện khi nhận được message từ server</summary>
    public event Action<NetworkMessage>? MessageReceived;

    /// <summary>Sự kiện khi mất kết nối</summary>
    public event Action<string>? Disconnected;

    /// <summary>Kết nối tới server</summary>
    public async Task ConnectAsync(string host, int port)
    {
        // Dọn dẹp kết nối cũ nếu còn
        Disconnect();

        _tcpClient = new TcpClient();

        // Timeout 10 giây cho kết nối
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _tcpClient.ConnectAsync(host, port, timeoutCts.Token);
        _stream = _tcpClient.GetStream();

        // Bắt đầu vòng lặp nhận message
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    /// <summary>Gửi message tới server (thread-safe)</summary>
    public async Task SendAsync(NetworkMessage message)
    {
        if (_stream == null) throw new InvalidOperationException("Chưa kết nối tới server.");

        await _sendLock.WaitAsync();
        try
        {
            var outbound = EncryptPayloads && message.Body.Length > 0
                ? new NetworkMessage
                {
                    Type = message.Type,
                    Flags = message.Flags | MessageFlags.Encrypted,
                    Body = message.Body,
                }
                : message;

            await MessageSerializer.WriteAsync(_stream, outbound);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Gửi message có payload</summary>
    public async Task SendAsync<T>(MessageType type, T payload)
    {
        var message = NetworkMessage.Create(type, payload);
        await SendAsync(message);
    }

    /// <summary>Gửi message không có body</summary>
    public async Task SendEmptyAsync(MessageType type)
    {
        var message = NetworkMessage.CreateEmpty(type);
        await SendAsync(message);
    }

    /// <summary>Vòng lặp nhận message liên tục từ server</summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                var message = await MessageReader.ReadAsync(_stream, ct);
                if (message == null)
                {
                    Disconnected?.Invoke("Server đã đóng kết nối.");
                    break;
                }
                MessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)
        {
            if (!ct.IsCancellationRequested)
                Disconnected?.Invoke("Mất kết nối tới server.");
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                Disconnected?.Invoke($"Lỗi kết nối: {ex.Message}");
        }
    }

    /// <summary>Ngắt kết nối</summary>
    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        try { _stream?.Close(); } catch { }
        _stream = null;
        try { _tcpClient?.Close(); } catch { }
        try { _tcpClient?.Dispose(); } catch { }
        _tcpClient = null;
    }

    public void Dispose()
    {
        Disconnect();
        _sendLock.Dispose();
    }
}
