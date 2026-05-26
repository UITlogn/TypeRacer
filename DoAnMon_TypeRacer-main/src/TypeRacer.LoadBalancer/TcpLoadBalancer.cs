using System.Net;
using System.Net.Sockets;

namespace TypeRacer.LoadBalancer;

/// <summary>
/// TCP Load Balancer — Layer 4 proxy.
/// Nhận kết nối từ client, chọn backend theo chiến lược,
/// rồi chuyển tiếp dữ liệu 2 chiều giữa client ↔ backend.
/// </summary>
public class TcpLoadBalancer
{
    private readonly int _port;
    private readonly List<BackendServer> _backends;
    private readonly ILoadBalancerStrategy _strategy;

    public TcpLoadBalancer(int port, List<BackendServer> backends, ILoadBalancerStrategy strategy)
    {
        _port = port;
        _backends = backends;
        _strategy = strategy;
    }

    /// <summary>Bắt đầu lắng nghe và chuyển tiếp kết nối</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[LoadBalancer] Đang lắng nghe trên port {_port}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var clientTcp = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleConnectionAsync(clientTcp, ct));
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Xử lý 1 kết nối: chọn backend → proxy 2 chiều</summary>
    private async Task HandleConnectionAsync(TcpClient clientTcp, CancellationToken ct)
    {
        var clientEndpoint = clientTcp.Client.RemoteEndPoint?.ToString() ?? "unknown";

        // Chọn backend
        var backend = _strategy.SelectBackend(_backends);
        if (backend == null)
        {
            Console.WriteLine($"[LoadBalancer] Không có backend khả dụng cho {clientEndpoint}");
            clientTcp.Close();
            return;
        }

        var activeConnections = backend.IncrementConnections();
        Console.WriteLine($"[LoadBalancer] {clientEndpoint} → {backend.Address} (kết nối: {activeConnections})");

        TcpClient? backendTcp = null;

        try
        {
            // Kết nối tới backend
            backendTcp = new TcpClient();
            await backendTcp.ConnectAsync(backend.Host, backend.Port, ct);

            var clientStream = clientTcp.GetStream();
            var backendStream = backendTcp.GetStream();

            // Chuyển tiếp 2 chiều: client → backend VÀ backend → client
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var clientToBackend = CopyStreamAsync(clientStream, backendStream, linkedCts.Token);
            var backendToClient = CopyStreamAsync(backendStream, clientStream, linkedCts.Token);

            // Chờ 1 trong 2 hướng kết thúc (tức là 1 bên đóng kết nối)
            await Task.WhenAny(clientToBackend, backendToClient);

            // Hủy hướng còn lại
            linkedCts.Cancel();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[LoadBalancer] Lỗi proxy {clientEndpoint}: {ex.Message}");
        }
        finally
        {
            activeConnections = backend.DecrementConnections();
            Console.WriteLine($"[LoadBalancer] {clientEndpoint} ngắt kết nối ({backend.Address}, còn {activeConnections})");

            clientTcp.Close();
            backendTcp?.Close();
        }
    }

    /// <summary>Copy dữ liệu từ source sang destination (1 chiều)</summary>
    private static async Task CopyStreamAsync(NetworkStream source, NetworkStream destination, CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await source.ReadAsync(buffer, ct);
                if (bytesRead == 0) break; // Kết nối đóng

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                await destination.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }
}
