using System.Net.Sockets;

namespace TypeRacer.LoadBalancer;

/// <summary>
/// Kiểm tra sức khỏe backend server: thử kết nối TCP.
/// Chạy định kỳ trên thread riêng.
/// </summary>
public class HealthChecker
{
    private readonly List<BackendServer> _backends;
    private readonly int _intervalMs;

    public HealthChecker(List<BackendServer> backends, int intervalMs = 5000)
    {
        _backends = backends;
        _intervalMs = intervalMs;
    }

    /// <summary>Chạy vòng lặp kiểm tra sức khỏe liên tục</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        Console.WriteLine("[HealthChecker] Bắt đầu kiểm tra sức khỏe backends...");

        while (!ct.IsCancellationRequested)
        {
            foreach (var backend in _backends)
            {
                bool wasHealthy = backend.IsHealthy;
                backend.IsHealthy = await CheckHealthAsync(backend);

                // Log khi trạng thái thay đổi
                if (wasHealthy != backend.IsHealthy)
                {
                    if (backend.IsHealthy)
                        Console.WriteLine($"[HealthChecker] {backend.Address} - ONLINE");
                    else
                        Console.WriteLine($"[HealthChecker] {backend.Address} - OFFLINE");
                }
            }

            await Task.Delay(_intervalMs, ct);
        }
    }

    /// <summary>Thử kết nối TCP tới backend, timeout 3 giây</summary>
    private static async Task<bool> CheckHealthAsync(BackendServer backend)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(3000);
            await client.ConnectAsync(backend.Host, backend.Port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
