namespace TypeRacer.LoadBalancer;

/// <summary>
/// Thông tin 1 backend server.
/// </summary>
public class BackendServer
{
    private int _activeConnections;
    private bool _isHealthy = true;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public bool IsHealthy
    {
        get => Volatile.Read(ref _isHealthy);
        set => Volatile.Write(ref _isHealthy, value);
    }
    public int ActiveConnections => Volatile.Read(ref _activeConnections);

    public string Address => $"{Host}:{Port}";

    public int IncrementConnections()
        => Interlocked.Increment(ref _activeConnections);

    public int DecrementConnections()
    {
        while (true)
        {
            var current = Volatile.Read(ref _activeConnections);
            if (current <= 0)
                return 0;

            if (Interlocked.CompareExchange(ref _activeConnections, current - 1, current) == current)
                return current - 1;
        }
    }
}
