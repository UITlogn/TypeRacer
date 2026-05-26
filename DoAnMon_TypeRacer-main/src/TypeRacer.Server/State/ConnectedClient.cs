using System.Net.Sockets;

namespace TypeRacer.Server.State;

public class ConnectedClient
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public TcpClient TcpClient { get; set; } = null!;
    public NetworkStream Stream { get; set; } = null!;
    public string SessionToken { get; set; } = string.Empty;
    public string? CurrentRoomCode { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public DateTime HeartbeatGraceUntil { get; set; } = DateTime.MinValue;
    public bool UseEncryptedMessages { get; set; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    public bool IsAuthenticated => UserId > 0;
}
