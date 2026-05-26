using TypeRacer.Server.Data;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared.Enums;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Room;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public class RoomHandler : IMessageHandler
{
    private readonly ServerState _serverState;
    private readonly IRaceRepository _raceRepo;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;
    private GameHandler? _gameHandler;

    public RoomHandler(ServerState serverState, IRaceRepository raceRepo, MistakeMemoryService mistakeMemory, FileLogger logger)
    {
        _serverState = serverState;
        _raceRepo = raceRepo;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    /// <summary>Inject GameHandler sau khi cả hai đã được tạo (tránh circular dependency)</summary>
    public void SetGameHandler(GameHandler gameHandler)
    {
        _gameHandler = gameHandler;
    }

    public async Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case MessageType.CREATE_ROOM:
                await HandleCreateRoomAsync(client, session, message, ct);
                break;
            case MessageType.JOIN_ROOM:
                await HandleJoinRoomAsync(client, session, message, ct);
                break;
            case MessageType.LEAVE_ROOM:
                await HandleLeaveRoomAsync(client, session, message, ct);
                break;
            case MessageType.PLAYER_READY:
                await HandlePlayerReadyAsync(client, session, message, ct);
                break;
            case MessageType.ROOM_LIST_REQUEST:
                await HandleRoomListAsync(client, session, ct);
                break;
        }
    }

    private const int MaxRoomsPerServer = 50;

    private async Task HandleCreateRoomAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<CreateRoomRequest>();
        var passageLanguage = NormalizeLanguage(request?.PassageLanguage);
        var raceDurationSeconds = NormalizeDuration(request?.RaceDurationSeconds);
        var gameMode = Shared.Constants.NormalizeGameMode(request?.GameMode);
        var aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(request?.AiPracticeDifficulty);
        var customPassageText = NormalizeCustomPassage(request?.CustomPassageText);

        if (client.CurrentRoomCode != null)
        {
            await session.SendAsync(MessageType.CREATE_ROOM_RESP, new CreateRoomResponse
            {
                Success = false,
                ErrorMessage = "Bạn đang ở trong phòng khác. Hãy rời phòng trước.",
            }, ct);
            return;
        }

        if (_serverState.Rooms.Count >= MaxRoomsPerServer)
        {
            await session.SendAsync(MessageType.CREATE_ROOM_RESP, new CreateRoomResponse
            {
                Success = false,
                ErrorMessage = "Server đã đạt giới hạn phòng. Vui lòng thử lại sau.",
            }, ct);
            return;
        }

        var roomCode = GenerateRoomCode();
        var room = new GameRoom
        {
            RoomCode = roomCode,
            HostUserId = client.UserId,
            PassageLanguage = passageLanguage,
            RaceDurationSeconds = raceDurationSeconds,
            EnableAiMode = request?.EnableAiMode ?? false,
            GameMode = gameMode,
            AiPracticeDifficulty = aiPracticeDifficulty,
            CustomPassageText = customPassageText,
        };

        room.Players[client.UserId] = new PlayerState
        {
            UserId = client.UserId,
            Username = client.Username,
        };

        try
        {
            room.DbRoomId = await _raceRepo.GetOrCreateRoomAsync(roomCode, client.UserId);
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể persist room {roomCode} vào database", ex);
        }

        _serverState.AddRoom(room);
        client.CurrentRoomCode = roomCode;

        _logger.Info($"Tạo phòng: {roomCode} bởi {client.Username}");

        await session.SendAsync(MessageType.CREATE_ROOM_RESP, new CreateRoomResponse
        {
            Success = true,
            RoomCode = roomCode,
            Room = BuildRoomDto(room, client.Username),
        }, ct);
    }

    private async Task HandleJoinRoomAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<JoinRoomRequest>();
        if (request == null)
        {
            await session.SendErrorAsync(ErrorCode.InvalidMessage, "Yêu cầu không hợp lệ");
            return;
        }

        if (client.CurrentRoomCode != null)
        {
            await session.SendAsync(MessageType.JOIN_ROOM_RESP, new JoinRoomResponse
            {
                Success = false,
                ErrorMessage = "Bạn đang ở trong phòng khác.",
            }, ct);
            return;
        }

        var room = _serverState.GetRoom(request.RoomCode);
        if (room == null)
        {
            await session.SendAsync(MessageType.JOIN_ROOM_RESP, new JoinRoomResponse
            {
                Success = false,
                ErrorMessage = "Không tìm thấy phòng.",
            }, ct);
            return;
        }

        if (room.Status != RoomStatus.Waiting && !room.AllowJoinInProgress)
        {
            await session.SendAsync(MessageType.JOIN_ROOM_RESP, new JoinRoomResponse
            {
                Success = false,
                ErrorMessage = "Phòng đã bắt đầu chơi.",
            }, ct);
            return;
        }

        var maxPlayers = room.IsCommunityRoom
            ? Shared.Constants.CommunityQuickPlayMaxPlayers
            : Shared.Constants.MaxPlayersPerRoom;
        if (room.Players.Values.Count(p => !p.IsBot) >= maxPlayers)
        {
            await session.SendAsync(MessageType.JOIN_ROOM_RESP, new JoinRoomResponse
            {
                Success = false,
                ErrorMessage = $"Phòng đã đầy ({maxPlayers} người).",
            }, ct);
            return;
        }

        var playerState = new PlayerState
        {
            UserId = client.UserId,
            Username = client.Username,
        };
        room.Players[client.UserId] = playerState;
        client.CurrentRoomCode = room.RoomCode;
        if (room.HostUserId <= 0)
            room.HostUserId = client.UserId;

        _logger.Info($"{client.Username} vào phòng {room.RoomCode}");

        // Danh sách người chơi
        var players = room.Players.Values.Where(p => !p.IsBot).Select(p => new RoomPlayerDto
        {
            UserId = p.UserId,
            Username = p.Username,
            IsReady = p.IsReady,
            IsHost = p.UserId == room.HostUserId,
        }).ToList();

        var hostClient = _serverState.GetClientByUserId(room.HostUserId);
        var currentRace = _gameHandler?.BuildCurrentRacePayload(room);

        // Gửi phản hồi cho người vào
        await session.SendAsync(MessageType.JOIN_ROOM_RESP, new JoinRoomResponse
        {
            Success = true,
            Room = BuildRoomDto(room, hostClient?.Username),
            Players = players,
            RaceInProgress = currentRace != null,
            CurrentRace = currentRace,
        }, ct);

        // Thông báo cho người chơi khác
        var joinedPayload = new PlayerJoinedPayload
        {
            Player = new RoomPlayerDto
            {
                UserId = client.UserId,
                Username = client.Username,
                IsReady = false,
                IsHost = false,
            },
        };

        await BroadcastToRoomAsync(room, MessageType.PLAYER_JOINED, joinedPayload, excludeUserId: client.UserId);
    }

    private async Task HandleLeaveRoomAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        if (client.CurrentRoomCode == null) return;

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null)
        {
            client.CurrentRoomCode = null;
            return;
        }

        room.Players.TryRemove(client.UserId, out _);
        _mistakeMemory.ClearUserRoom(client.UserId, room.RoomCode);
        client.CurrentRoomCode = null;

        _logger.Info($"{client.Username} rời phòng {room.RoomCode}");

        var leftPayload = new PlayerLeftPayload
        {
            UserId = client.UserId,
            Username = client.Username,
        };

        // Chuyển host nếu cần
        if (room.HostUserId == client.UserId && room.Players.Values.Any(p => !p.IsBot))
        {
            var newHost = room.Players.Values.FirstOrDefault(p => !p.IsBot);
            if (newHost != null)
            {
                room.HostUserId = newHost.UserId;
                leftPayload.NewHostUserId = newHost.UserId;

                try
                {
                    await _raceRepo.UpdateRoomHostAsync(room.RoomCode, newHost.UserId);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Không thể cập nhật host room {room.RoomCode} trên database", ex);
                }
            }
        }

        if (!room.Players.Values.Any(p => !p.IsBot))
        {
            // Hủy race timeout nếu đang chạy
            room.RaceTimeoutCts?.Cancel();
            room.AiBotCts?.Cancel();
            _mistakeMemory.ClearRoom(room.RoomCode);
            if (room.IsCommunityRoom)
            {
                room.ResetAfterLastHumanLeaves();
                _logger.Info($"Phòng cộng đồng {room.RoomCode} trống, giữ lại để Quick Play tự động chạy tiếp");
            }
            else
            {
                _serverState.RemoveRoom(room.RoomCode);
                _logger.Info($"Phòng {room.RoomCode} đã bị xóa (trống)");
            }
        }
        else
        {
            await BroadcastToRoomAsync(room, MessageType.PLAYER_LEFT, leftPayload);

            // Nếu đang Racing và tất cả người còn lại đều finished → kết thúc race
            if (room.Status == RoomStatus.Racing &&
                room.Players.Values.Where(p => !p.IsBot).All(p => p.IsFinished) &&
                _gameHandler != null)
            {
                await _gameHandler.FinishRaceAsync(room);
            }
        }
    }

    private async Task HandlePlayerReadyAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<PlayerReadyRequest>();
        if (request == null || client.CurrentRoomCode == null) return;

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null) return;

        if (room.Players.TryGetValue(client.UserId, out var playerState))
        {
            playerState.IsReady = request.IsReady;
        }

        _logger.Info($"{client.Username} sẵn sàng={request.IsReady} trong phòng {room.RoomCode}");

        // Gửi cập nhật phòng cho tất cả
        var hostClient = _serverState.GetClientByUserId(room.HostUserId);
        var update = new RoomUpdatePayload
        {
            Room = BuildRoomDto(room, hostClient?.Username),
            Players = room.Players.Values.Where(p => !p.IsBot).Select(p => new RoomPlayerDto
            {
                UserId = p.UserId,
                Username = p.Username,
                IsReady = p.IsReady,
                IsHost = p.UserId == room.HostUserId,
            }).ToList(),
        };

        await BroadcastToRoomAsync(room, MessageType.ROOM_UPDATE, update);
    }

    private async Task HandleRoomListAsync(ConnectedClient client, ClientSession session, CancellationToken ct)
    {
        var rooms = _serverState.GetAvailableRooms();

        var response = new RoomListResponse
        {
            Rooms = rooms.Select(r =>
            {
                var hostClient = _serverState.GetClientByUserId(r.HostUserId);
                return BuildRoomDto(r, r.IsCommunityRoom ? Shared.Constants.CommunityRoomName : hostClient?.Username);
            }).ToList(),
        };

        await session.SendAsync(MessageType.ROOM_LIST_RESPONSE, response, ct);
    }

    private async Task BroadcastToRoomAsync<T>(GameRoom room, MessageType type, T payload, int? excludeUserId = null)
    {
        var message = NetworkMessage.Create(type, payload);

        foreach (var player in room.Players.Values)
        {
            if (excludeUserId.HasValue && player.UserId == excludeUserId.Value)
                continue;

            var playerClient = _serverState.GetClientByUserId(player.UserId);
            if (playerClient != null)
            {
                try
                {
                    var session = new ClientSession(playerClient);
                    await session.SendAsync(message);
                }
                catch { }
            }
        }
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private static int NormalizeDuration(int? seconds)
    {
        return Math.Clamp(seconds.GetValueOrDefault(Shared.Constants.DefaultRaceDurationSeconds),
            Shared.Constants.MinRaceDurationSeconds,
            Shared.Constants.MaxRaceDurationSeconds);
    }

    private static RoomDto BuildRoomDto(GameRoom room, string? hostUsername)
    {
        return new RoomDto
        {
            RoomCode = room.RoomCode,
            HostUserId = room.HostUserId,
            HostUsername = hostUsername ?? "Host",
            CurrentPlayers = room.Players.Values.Count(p => !p.IsBot),
            PassageLanguage = room.PassageLanguage,
            RaceDurationSeconds = room.RaceDurationSeconds,
            EnableAiMode = room.EnableAiMode,
            GameMode = room.GameMode,
            AiPracticeDifficulty = room.AiPracticeDifficulty,
            HasCustomPassage = !string.IsNullOrWhiteSpace(room.CustomPassageText),
            IsCommunityRoom = room.IsCommunityRoom,
            IsJoinableInProgress = room.AllowJoinInProgress,
            Status = room.Status.ToString().ToLowerInvariant(),
            SecondsUntilNextStart = room.NextAutoStartAtUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((room.NextAutoStartAtUtc.Value - DateTime.UtcNow).TotalSeconds))
                : 0,
        };
    }

    private static string NormalizeCustomPassage(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        var normalized = string.Join(" ", rawText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        if (normalized.Length < 40)
            return string.Empty;

        return normalized.Length > 1_000 ? normalized[..1_000].TrimEnd() : normalized;
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            "any" => "any",
            _ => "en",
        };
    }
}
