using TypeRacer.Shared;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Network;

/// <summary>
/// Quản lý kết nối: tự động gửi heartbeat ping,
/// xử lý reconnect khi mất kết nối.
/// </summary>
public class ConnectionManager : IDisposable
{
    private readonly TcpGameClient _client;
    private System.Threading.Timer? _heartbeatTimer;

    public ConnectionManager(TcpGameClient client)
    {
        _client = client;
    }

    /// <summary>Bắt đầu gửi heartbeat định kỳ</summary>
    public void StartHeartbeat()
    {
        StopHeartbeat(); // Dừng timer cũ trước
        _heartbeatTimer = new System.Threading.Timer(
            async _ => await SendHeartbeatAsync(),
            null,
            Constants.HeartbeatIntervalMs,
            Constants.HeartbeatIntervalMs);
    }

    /// <summary>Dừng heartbeat</summary>
    public void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.SendEmptyAsync(MessageType.HEARTBEAT_PING);
            }
        }
        catch
        {
            // Nếu gửi heartbeat lỗi, bỏ qua — sẽ bị timeout ở server
        }
    }

    public void Dispose()
    {
        StopHeartbeat();
    }
}
