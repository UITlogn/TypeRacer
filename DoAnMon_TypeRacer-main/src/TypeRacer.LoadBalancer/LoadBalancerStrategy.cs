namespace TypeRacer.LoadBalancer;

/// <summary>
/// Interface cho chiến lược chọn backend.
/// </summary>
public interface ILoadBalancerStrategy
{
    BackendServer? SelectBackend(List<BackendServer> backends);
}

/// <summary>
/// Round-Robin: chọn backend lần lượt theo vòng tròn.
/// Bỏ qua backend không khỏe.
/// </summary>
public class RoundRobinStrategy : ILoadBalancerStrategy
{
    private readonly object _lock = new();
    private int _currentIndex = -1;

    public BackendServer? SelectBackend(List<BackendServer> backends)
    {
        lock (_lock)
        {
            var healthy = backends.Where(b => b.IsHealthy).ToList();
            if (healthy.Count == 0) return null;

            _currentIndex = (_currentIndex + 1) % healthy.Count;
            return healthy[_currentIndex];
        }
    }
}

/// <summary>
/// Least Connections: chọn backend có ít kết nối nhất.
/// Bỏ qua backend không khỏe.
/// </summary>
public class LeastConnectionsStrategy : ILoadBalancerStrategy
{
    public BackendServer? SelectBackend(List<BackendServer> backends)
    {
        return backends
            .Where(b => b.IsHealthy)
            .OrderBy(b => b.ActiveConnections)
            .FirstOrDefault();
    }
}
