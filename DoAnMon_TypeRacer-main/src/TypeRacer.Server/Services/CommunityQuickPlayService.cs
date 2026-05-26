using TypeRacer.Server.Handlers;
using TypeRacer.Server.Logging;
using TypeRacer.Server.State;
using TypeRacer.Shared;
using TypeRacer.Shared.Enums;

namespace TypeRacer.Server.Services;

public sealed class CommunityQuickPlayService
{
    private readonly ServerState _serverState;
    private readonly GameHandler _gameHandler;
    private readonly FileLogger _logger;

    public CommunityQuickPlayService(ServerState serverState, GameHandler gameHandler, FileLogger logger)
    {
        _serverState = serverState;
        _gameHandler = gameHandler;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        EnsureCommunityRoom();
        _logger.Info($"Community Quick Play room ready: {Constants.CommunityRoomCode}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var room = EnsureCommunityRoom();
                var now = DateTime.UtcNow;
                room.NextAutoStartAtUtc ??= now.AddSeconds(Constants.CommunityQuickPlayIntervalSeconds);

                if (room.NextAutoStartAtUtc.Value <= now && room.Status == RoomStatus.Waiting)
                {
                    if (room.Players.Values.Any(p => !p.IsBot))
                    {
                        room.NextAutoStartAtUtc = now.AddSeconds(Constants.CommunityQuickPlayIntervalSeconds);
                        var started = await _gameHandler.TryStartAutomaticRaceAsync(room, ct);
                        _logger.Info(started
                            ? $"Community Quick Play auto-started with {room.Players.Values.Count(p => !p.IsBot)} player(s)"
                            : "Community Quick Play auto-start skipped");
                    }
                    else
                    {
                        room.NextAutoStartAtUtc = now.AddSeconds(Constants.CommunityQuickPlayIntervalSeconds);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Community Quick Play loop failed", ex);
            }

            await Task.Delay(1000, ct);
        }
    }

    private GameRoom EnsureCommunityRoom()
    {
        var room = _serverState.GetRoom(Constants.CommunityRoomCode);
        if (room != null)
            return room;

        room = new GameRoom
        {
            RoomCode = Constants.CommunityRoomCode,
            HostUserId = 0,
            PassageLanguage = "vi",
            RaceDurationSeconds = Constants.CommunityQuickPlayIntervalSeconds,
            EnableAiMode = false,
            GameMode = Constants.GameModeClassic,
            AiPracticeDifficulty = Constants.DefaultAiPracticeDifficulty,
            IsCommunityRoom = true,
            AllowJoinInProgress = true,
            AutoStartIntervalSeconds = Constants.CommunityQuickPlayIntervalSeconds,
            NextAutoStartAtUtc = DateTime.UtcNow.AddSeconds(Constants.CommunityQuickPlayIntervalSeconds),
        };

        _serverState.AddRoom(room);
        return room;
    }
}
