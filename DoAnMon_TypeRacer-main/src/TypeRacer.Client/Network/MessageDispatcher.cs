using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Network;

/// <summary>
/// Phân phối message nhận được từ server tới UI thread.
/// Dùng SynchronizationContext.Post để cập nhật UI an toàn từ thread khác.
/// </summary>
public class MessageDispatcher
{
    private readonly SynchronizationContext? _syncContext;

    // Các event theo loại message - Form đăng ký lắng nghe
    public event Action<NetworkMessage>? OnLoginResponse;
    public event Action<NetworkMessage>? OnRegisterResponse;
    public event Action<NetworkMessage>? OnCreateRoomResponse;
    public event Action<NetworkMessage>? OnJoinRoomResponse;
    public event Action<NetworkMessage>? OnRoomUpdate;
    public event Action<NetworkMessage>? OnPlayerJoined;
    public event Action<NetworkMessage>? OnPlayerLeft;
    public event Action<NetworkMessage>? OnRoomListResponse;
    public event Action<NetworkMessage>? OnRaceCountdown;
    public event Action<NetworkMessage>? OnRaceStart;
    public event Action<NetworkMessage>? OnProgressBroadcast;
    public event Action<NetworkMessage>? OnRaceResult;
    public event Action<NetworkMessage>? OnChatBroadcast;
    public event Action<NetworkMessage>? OnProfileResponse;
    public event Action<NetworkMessage>? OnLeaderboardResponse;
    public event Action<NetworkMessage>? OnMatchHistoryResponse;
    public event Action<NetworkMessage>? OnAiCoachResponse;
    public event Action<NetworkMessage>? OnError;
    public event Action<NetworkMessage>? OnHeartbeatPong;
    public event Action<NetworkMessage>? OnDisconnect;

    public MessageDispatcher()
    {
        // Lưu lại SynchronizationContext của UI thread (phải gọi từ UI thread)
        _syncContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Phân phối message tới event tương ứng, chuyển về UI thread.
    /// Gọi method này từ TcpGameClient.MessageReceived event.
    /// </summary>
    public void Dispatch(NetworkMessage message)
    {
        Action<NetworkMessage>? handler = message.Type switch
        {
            MessageType.LOGIN_RESPONSE      => OnLoginResponse,
            MessageType.REGISTER_RESPONSE   => OnRegisterResponse,
            MessageType.CREATE_ROOM_RESP    => OnCreateRoomResponse,
            MessageType.JOIN_ROOM_RESP      => OnJoinRoomResponse,
            MessageType.ROOM_UPDATE         => OnRoomUpdate,
            MessageType.PLAYER_JOINED       => OnPlayerJoined,
            MessageType.PLAYER_LEFT         => OnPlayerLeft,
            MessageType.ROOM_LIST_RESPONSE  => OnRoomListResponse,
            MessageType.RACE_COUNTDOWN      => OnRaceCountdown,
            MessageType.RACE_START          => OnRaceStart,
            MessageType.PROGRESS_BROADCAST  => OnProgressBroadcast,
            MessageType.RACE_RESULT         => OnRaceResult,
            MessageType.CHAT_BROADCAST      => OnChatBroadcast,
            MessageType.PROFILE_RESPONSE    => OnProfileResponse,
            MessageType.LEADERBOARD_RESP    => OnLeaderboardResponse,
            MessageType.MATCH_HISTORY_RESP  => OnMatchHistoryResponse,
            MessageType.AI_COACH_RESPONSE   => OnAiCoachResponse,
            MessageType.ERROR               => OnError,
            MessageType.HEARTBEAT_PONG      => OnHeartbeatPong,
            MessageType.DISCONNECT          => OnDisconnect,
            _ => null,
        };

        if (handler == null) return;

        // Chuyển về UI thread nếu có SynchronizationContext
        if (_syncContext != null)
        {
            _syncContext.Post(_ => handler(message), null);
        }
        else
        {
            handler(message);
        }
    }
}
