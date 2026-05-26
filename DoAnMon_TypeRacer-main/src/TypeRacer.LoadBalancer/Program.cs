using Microsoft.Extensions.Configuration;

namespace TypeRacer.LoadBalancer;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Đọc cấu hình
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var port = int.TryParse(config["LoadBalancer:Port"], out var p) ? p : 4999;
        var strategyName = config["LoadBalancer:Strategy"] ?? "RoundRobin";
        var healthCheckInterval = int.TryParse(config["LoadBalancer:HealthCheckIntervalMs"], out var hci) ? hci : 5000;

        // Đọc danh sách backend servers
        var backends = new List<BackendServer>();
        var backendsSection = config.GetSection("LoadBalancer:Backends");
        foreach (var child in backendsSection.GetChildren())
        {
            backends.Add(new BackendServer
            {
                Host = child["Host"] ?? "127.0.0.1",
                Port = int.Parse(child["Port"] ?? "5000"),
            });
        }

        if (backends.Count == 0)
        {
            Console.WriteLine("[LỖI] Không tìm thấy backend server nào trong cấu hình.");
            return;
        }

        Console.WriteLine("=== TypeRacer Load Balancer ===");
        Console.WriteLine($"Port: {port}");
        Console.WriteLine($"Chiến lược: {strategyName}");
        Console.WriteLine($"Backends ({backends.Count}):");
        foreach (var b in backends)
            Console.WriteLine($"  - {b.Address}");

        // Chọn chiến lược phân tải
        ILoadBalancerStrategy strategy = strategyName.ToLower() switch
        {
            "leastconnections" => new LeastConnectionsStrategy(),
            _ => new RoundRobinStrategy(),
        };

        // Cancellation
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nĐang tắt...");
        };

        // Chạy health checker trên thread riêng
        var healthChecker = new HealthChecker(backends, healthCheckInterval);
        var healthTask = Task.Run(() => healthChecker.StartAsync(cts.Token));

        // Chạy load balancer
        var lb = new TcpLoadBalancer(port, backends, strategy);

        Console.WriteLine($"\nĐang lắng nghe trên port {port}... (Nhấn Ctrl+C để dừng)");
        await lb.StartAsync(cts.Token);
    }
}
