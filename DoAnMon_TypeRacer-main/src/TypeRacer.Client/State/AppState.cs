using TypeRacer.Client.Network;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.State;

/// <summary>
/// Singleton lưu trạng thái ứng dụng: user hiện tại, kết nối, session.
/// Dùng chung cho tất cả Forms.
/// </summary>
public class AppState
{
    public const string InternetServerHost = "134.209.108.82";
    public const int InternetServerPort = 5000;
    public const string LanServerHost = "192.168.1.5";
    public const int LanServerPort = 5000;
    public const string RadminVpnHost = "26.14.193.24";
    public const int RadminVpnPort = 5000;

    private static AppState? _instance;
    public static AppState Instance => _instance ??= new AppState();

    // Kết nối mạng
    public TcpGameClient Client { get; } = new();
    public MessageDispatcher Dispatcher { get; private set; } = null!;
    public ConnectionManager ConnectionManager { get; private set; } = null!;

    // Thông tin user đã đăng nhập
    public UserDto? CurrentUser { get; set; }
    public string? SessionToken { get; set; }
    public bool IsLoggedIn => CurrentUser != null && SessionToken != null;

    // Thông tin phòng hiện tại
    public string? CurrentRoomCode { get; set; }

    // Cấu hình server
    public string ServerHost { get; set; } = InternetServerHost;
    public int ServerPort { get; set; } = InternetServerPort;

    private bool _initialized;

    /// <summary>Khởi tạo dispatcher (phải gọi từ UI thread, chỉ chạy 1 lần)</summary>
    public void InitializeDispatcher()
    {
        if (_initialized) return;
        _initialized = true;

        Dispatcher = new MessageDispatcher();
        ConnectionManager = new ConnectionManager(Client);

        // Chỉ subscribe 1 lần duy nhất
        Client.MessageReceived += Dispatcher.Dispatch;
    }

    /// <summary>Kết nối tới server</summary>
    public async Task ConnectAsync()
    {
        await Client.ConnectAsync(ServerHost, ServerPort);
        ConnectionManager.StartHeartbeat();
    }

    /// <summary>Đăng xuất và ngắt kết nối</summary>
    public void Logout()
    {
        CurrentUser = null;
        SessionToken = null;
        CurrentRoomCode = null;
        ConnectionManager.StopHeartbeat();
        Client.Disconnect();
    }
}
