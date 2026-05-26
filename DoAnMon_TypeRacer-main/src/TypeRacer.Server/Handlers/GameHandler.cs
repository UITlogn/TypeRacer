using System.Text;
using System.Text.RegularExpressions;
using TypeRacer.Server.Data;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared;
using TypeRacer.Shared.Enums;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Game;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public class GameHandler : IMessageHandler
{
    private readonly ServerState _serverState;
    private readonly IPassageRepository _passageRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;

    public GameHandler(ServerState serverState, IPassageRepository passageRepo,
        IRaceRepository raceRepo, MistakeMemoryService mistakeMemory, FileLogger logger)
    {
        _serverState = serverState;
        _passageRepo = passageRepo;
        _raceRepo = raceRepo;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case MessageType.RACE_START:
                // Host requests to start the race
                await HandleStartRaceAsync(client, session, ct);
                break;
            case MessageType.TYPING_UPDATE:
                await HandleTypingUpdateAsync(client, session, message, ct);
                break;
            case MessageType.RACE_FINISH:
                await HandleRaceFinishAsync(client, session, message, ct);
                break;
        }
    }

    public async Task HandleStartRaceAsync(ConnectedClient client, ClientSession session, CancellationToken ct)
    {
        if (client.CurrentRoomCode == null)
        {
            await session.SendErrorAsync(ErrorCode.NotInRoom, "You are not in a room.");
            return;
        }

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null)
        {
            await session.SendErrorAsync(ErrorCode.RoomNotFound, "Room not found.");
            return;
        }

        if (room.HostUserId != client.UserId)
        {
            await session.SendErrorAsync(ErrorCode.NotRoomHost, "Only the host can start the race.");
            return;
        }

        if (room.Status != RoomStatus.Waiting)
        {
            await session.SendErrorAsync(ErrorCode.RoomAlreadyStarted, "Race is already in progress.");
            return;
        }

        // Check all players are ready (except host and server-side bots)
        var notReady = room.Players.Values
            .Where(p => !p.IsBot && p.UserId != room.HostUserId && !p.IsReady)
            .ToList();

        if (notReady.Count > 0)
        {
            await session.SendErrorAsync(ErrorCode.NotAllReady, "Not all players are ready.");
            return;
        }

        if (room.Players.Values.Count(p => !p.IsBot) < 1)
        {
            await session.SendErrorAsync(ErrorCode.NotEnoughPlayers, "Need at least 1 player.");
            return;
        }

        await StartRaceCoreAsync(room, ct, async (code, text) => await session.SendErrorAsync(code, text));
    }

    public async Task<bool> TryStartAutomaticRaceAsync(GameRoom room, CancellationToken ct)
    {
        if (room.Status != RoomStatus.Waiting)
            return false;

        if (room.Players.Values.Count(p => !p.IsBot) < 1)
            return false;

        if (room.HostUserId <= 0)
        {
            var firstHuman = room.Players.Values.FirstOrDefault(p => !p.IsBot);
            if (firstHuman == null)
                return false;
            room.HostUserId = firstHuman.UserId;
        }

        return await StartRaceCoreAsync(room, ct);
    }

    public RaceStartPayload? BuildCurrentRacePayload(GameRoom room)
    {
        if (room.Status != RoomStatus.Racing || string.IsNullOrWhiteSpace(room.PassageText))
            return null;

        var elapsedSeconds = room.StartedAt.HasValue
            ? Math.Max(0, (int)Math.Floor((DateTime.UtcNow - room.StartedAt.Value).TotalSeconds))
            : 0;

        return new RaceStartPayload
        {
            PassageText = room.PassageText,
            PassageId = room.PassageId,
            RoomCode = room.RoomCode,
            PassageLanguage = NormalizeLanguage(room.PassageLanguage),
            RaceDurationSeconds = Math.Clamp(room.RaceDurationSeconds,
                Shared.Constants.MinRaceDurationSeconds,
                Shared.Constants.MaxRaceDurationSeconds),
            RaceElapsedSeconds = elapsedSeconds,
            GameMode = Shared.Constants.NormalizeGameMode(room.GameMode),
            AiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(room.AiPracticeDifficulty),
        };
    }

    private async Task<bool> StartRaceCoreAsync(
        GameRoom room,
        CancellationToken ct,
        Func<ErrorCode, string, Task>? sendErrorAsync = null)
    {
        var gameMode = Shared.Constants.NormalizeGameMode(room.GameMode);
        var aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(room.AiPracticeDifficulty);
        room.GameMode = gameMode;
        room.AiPracticeDifficulty = aiPracticeDifficulty;

        var raceDurationSeconds = Math.Clamp(room.RaceDurationSeconds,
            Shared.Constants.MinRaceDurationSeconds,
            Shared.Constants.MaxRaceDurationSeconds);

        // Get random passage (or AI-generated from recent errors) để tránh lặp trong phòng
        var passage = await PickNextPassageAsync(room);
        if (passage == null)
        {
            if (sendErrorAsync != null)
                await sendErrorAsync(ErrorCode.InternalError, "No passages available.");
            return false;
        }

        room.PassageText = passage.Content;
        room.LastPassageText = passage.Content;
        room.PassageId = passage.Id;
        room.LastPassageId = passage.Id;
        room.MarkPassageUsed(passage.Id);
        room.Status = RoomStatus.Countdown;

        if (gameMode == Shared.Constants.GameModeAiPractice)
            EnsureAiPracticeBot(room, aiPracticeDifficulty);
        else
            RemoveAiPracticeBots(room);

        // Reset player states
        foreach (var player in room.Players.Values)
        {
            player.CurrentPosition = 0;
            player.CorrectChars = 0;
            player.WrongChars = 0;
            player.CurrentWpm = 0;
            player.CurrentAccuracy = 0;
            player.IsCompleted = false;
            player.IsFinished = false;
            player.IsDisqualified = false;
            player.FinishedAt = null;
            player.TimeTakenMs = 0;
            player.TypedText = string.Empty;
            player.BackspaceCount = 0;
            player.BestStreak = 0;
            player.ConsistencyScore = 0;
            player.Achievements.Clear();
            player.ClearObservedMistakes();
        }

        _logger.Info($"Race countdown started in room {room.RoomCode}");

        // Send countdown (3, 2, 1)
        for (int i = Constants.CountdownSeconds; i > 0; i--)
        {
            await BroadcastToRoomAsync(room, MessageType.RACE_COUNTDOWN,
                new RaceCountdownPayload { SecondsRemaining = i });
            await Task.Delay(1000, ct);

            // Nếu phòng trống hoặc status thay đổi → hủy countdown
            if (room.Players.IsEmpty || room.Status != RoomStatus.Countdown)
            {
                room.Status = RoomStatus.Waiting;
                _logger.Info($"Race countdown cancelled in room {room.RoomCode}");
                return false;
            }
        }

        // Kiểm tra lần cuối trước khi start
        if (room.Players.IsEmpty)
        {
            room.Status = RoomStatus.Waiting;
            return false;
        }

        // Start race
        room.Status = RoomStatus.Racing;
        room.StartedAt = DateTime.UtcNow;
        room.RaceId = null;
        room.RaceTimeoutCts?.Cancel();
        room.RaceTimeoutCts?.Dispose();
        room.RaceTimeoutCts = new CancellationTokenSource();
        var raceToken = room.RaceTimeoutCts.Token;

        try
        {
            if (room.DbRoomId <= 0)
            {
                room.DbRoomId = await _raceRepo.GetOrCreateRoomAsync(room.RoomCode, room.HostUserId);
            }

            room.RaceId = await _raceRepo.CreateRaceAsync(room.DbRoomId, room.RoomCode, room.PassageId, room.StartedAt.Value);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create race record at start for room {room.RoomCode}", ex);
        }

        await BroadcastToRoomAsync(room, MessageType.RACE_START, new RaceStartPayload
        {
            PassageText = passage.Content,
            PassageId = passage.Id,
            RoomCode = room.RoomCode,
            PassageLanguage = passage.Language,
            RaceDurationSeconds = raceDurationSeconds,
            RaceElapsedSeconds = 0,
            GameMode = gameMode,
            AiPracticeDifficulty = aiPracticeDifficulty,
        });

        _logger.Info($"Race started in room {room.RoomCode}");

        // Start race timeout
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(raceDurationSeconds), raceToken);
                await FinishRaceAsync(room);
            }
            catch (OperationCanceledException) { }
        });

        if (gameMode == Shared.Constants.GameModeAiPractice)
            StartAiPracticeBot(room, raceToken);

        return true;
    }

    private async Task HandleTypingUpdateAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var update = message.GetPayload<TypingUpdatePayload>();
        if (update == null)
        {
            await session.SendErrorAsync(ErrorCode.InvalidMessage, "Invalid typing update payload.");
            return;
        }

        if (client.CurrentRoomCode == null)
        {
            await session.SendErrorAsync(ErrorCode.NotInRoom, "You are not in a room.");
            return;
        }

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null)
        {
            await session.SendErrorAsync(ErrorCode.RoomNotFound, "Room not found.");
            return;
        }
        if (room.Status != RoomStatus.Racing) return;

        if (room.Players.TryGetValue(client.UserId, out var playerState))
        {
            if (playerState.IsFinished) return;

            // Clamp các giá trị từ client — không tin client
            var maxChars = room.PassageText?.Length ?? 0;
            var typedText = NormalizeTypedText(update.TypedText);
            var typedAnalysis = string.IsNullOrEmpty(typedText)
                ? AnalyzeClientCounters(update.CurrentPosition, update.CorrectChars, update.WrongChars, maxChars)
                : AnalyzeTypedText(room.PassageText ?? string.Empty, typedText);

            playerState.CurrentPosition = typedAnalysis.CurrentPosition;
            playerState.CorrectChars = typedAnalysis.CorrectChars;
            playerState.WrongChars = typedAnalysis.WrongChars;
            playerState.BestStreak = Math.Max(playerState.BestStreak, typedAnalysis.BestStreak);
            playerState.ObserveMistakes(room.PassageText ?? string.Empty, typedText);
            if (!string.IsNullOrEmpty(typedText))
                room.SetPlayerTypedText(client.UserId, typedText);

            // Calculate WPM (dùng giá trị đã clamp)
            if (room.StartedAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - room.StartedAt.Value).TotalSeconds;
                if (elapsed > 0)
                {
                    playerState.CurrentWpm = (typedAnalysis.CorrectChars / 5.0) / (elapsed / 60.0);
                }
            }

            // Calculate accuracy (dùng giá trị đã clamp)
            var totalChars = typedAnalysis.CorrectChars + typedAnalysis.WrongChars;
            playerState.CurrentAccuracy = totalChars > 0
                ? (double)typedAnalysis.CorrectChars / totalChars * 100.0
                : 100.0;

            // Broadcast progress to all players (throttled by client sending rate)
            var progressPayload = new ProgressBroadcastPayload
            {
                RoomCode = room.RoomCode,
                Players = room.Players.Values.Select(p => new PlayerProgressDto
                {
                    UserId = p.UserId,
                    Username = p.Username,
                    Progress = room.PassageText != null
                        ? Math.Min(1.0, (double)p.CurrentPosition / room.PassageText.Length)
                        : 0,
                    Wpm = p.CurrentWpm,
                    IsFinished = p.IsFinished,
                    IsAiBot = p.IsBot,
                }).ToList(),
            };

            await BroadcastToRoomAsync(room, MessageType.PROGRESS_BROADCAST, progressPayload);
        }
    }

    private async Task HandleRaceFinishAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var finish = message.GetPayload<RaceFinishPayload>();
        if (finish == null)
        {
            await session.SendErrorAsync(ErrorCode.InvalidMessage, "Invalid race finish payload.");
            return;
        }

        if (client.CurrentRoomCode == null)
        {
            await session.SendErrorAsync(ErrorCode.NotInRoom, "You are not in a room.");
            return;
        }

        var room = _serverState.GetRoom(client.CurrentRoomCode);
        if (room == null)
        {
            await session.SendErrorAsync(ErrorCode.RoomNotFound, "Room not found.");
            return;
        }
        if (room.Status != RoomStatus.Racing) return;

        if (room.Players.TryGetValue(client.UserId, out var playerState))
        {
            // Chặn finish nhiều lần
            if (playerState.IsFinished) return;

            playerState.IsFinished = true;
            playerState.FinishedAt = DateTime.UtcNow;

            // Validate: CorrectChars không vượt quá passage length
            var maxChars = room.PassageText?.Length ?? 0;
            var typedText = NormalizeTypedText(finish.TypedText);
            var typedAnalysis = string.IsNullOrEmpty(typedText)
                ? AnalyzeClientCounters(finish.CorrectChars + finish.WrongChars, finish.CorrectChars, finish.WrongChars, maxChars)
                : AnalyzeTypedText(room.PassageText ?? string.Empty, typedText);
            playerState.TypedText = typedText;
            playerState.BackspaceCount = Math.Max(0, finish.BackspaceCount);
            playerState.ObserveMistakes(room.PassageText ?? string.Empty, typedText);

            var gameMode = Shared.Constants.NormalizeGameMode(room.GameMode);
            var isDisqualified = finish.IsDisqualified ||
                (gameMode == Shared.Constants.GameModeSuddenDeath && typedAnalysis.WrongChars > 0) ||
                (gameMode == Shared.Constants.GameModeNoBackspace && playerState.BackspaceCount > 0);
            var isCompleted = maxChars > 0 && typedAnalysis.CorrectChars >= maxChars && !finish.IsTimeout && !isDisqualified;

            // Validate: TimeTakenMs phải hợp lý (ít nhất 1 giây, server tự tính nếu cần)
            int timeTakenMs;
            if (room.StartedAt.HasValue)
            {
                timeTakenMs = (int)(DateTime.UtcNow - room.StartedAt.Value).TotalMilliseconds;
            }
            else
            {
                timeTakenMs = Math.Max(1000, finish.TimeTakenMs);
            }

            playerState.CorrectChars = typedAnalysis.CorrectChars;
            playerState.WrongChars = typedAnalysis.WrongChars;
            playerState.IsCompleted = isCompleted;
            playerState.IsDisqualified = isDisqualified;
            playerState.TimeTakenMs = timeTakenMs;
            playerState.BestStreak = typedAnalysis.BestStreak;
            playerState.ConsistencyScore = CalculateConsistencyScore(typedAnalysis, maxChars, isCompleted, isDisqualified);

            // Server tự tính WPM và accuracy — không tin client
            var totalChars = typedAnalysis.CorrectChars + typedAnalysis.WrongChars;
            playerState.CurrentAccuracy = totalChars > 0
                ? (double)typedAnalysis.CorrectChars / totalChars * 100.0
                : 100.0;
            playerState.CurrentWpm = timeTakenMs > 0
                ? (typedAnalysis.CorrectChars / 5.0) / (timeTakenMs / 60000.0)
                : 0;
            playerState.Achievements = BuildAchievements(playerState, gameMode, maxChars);

            if (finish.IsTimeout)
            {
                _logger.Info(
                    $"Player {client.Username} timeout in room {room.RoomCode}, " +
                    $"typed={playerState.TypedText.Length}/{maxChars}");
            }

            _logger.Info($"Player {client.Username} finished race in room {room.RoomCode} - WPM: {playerState.CurrentWpm:F1} | completed={playerState.IsCompleted} | mode={gameMode} | dq={playerState.IsDisqualified}");
        }

        // In AI Practice, show results as soon as all human players are done.
        if (AreRaceParticipantsFinished(room))
        {
            await FinishRaceAsync(room);
        }
    }

    public async Task FinishRaceAsync(GameRoom room)
    {
        // Thread-safe: chỉ 1 thread được chạy finish
        if (!room.TryClaimFinish()) return;

        if (room.Status == RoomStatus.Finished) return;
        room.Status = RoomStatus.Finished;
        room.RaceTimeoutCts?.Cancel();
        room.AiBotCts?.Cancel();

        _logger.Info($"Race finished in room {room.RoomCode}");
        var finishedAt = DateTime.UtcNow;
        var raceStartedAt = room.StartedAt ?? finishedAt;
        var elapsedMs = Math.Max(1000, (int)(finishedAt - raceStartedAt).TotalMilliseconds);
        var gameMode = Shared.Constants.NormalizeGameMode(room.GameMode);

        foreach (var player in room.Players.Values)
        {
            if (!player.IsFinished)
            {
                if (player.IsBot && gameMode == Shared.Constants.GameModeAiPractice)
                    FinalizeUnfinishedAiPracticeBot(player, room.PassageText ?? room.LastPassageText ?? string.Empty, elapsedMs);
                else
                    FinalizeUnfinishedPlayer(player, room.PassageText ?? room.LastPassageText ?? string.Empty, elapsedMs, gameMode);
            }
        }

        // Calculate rankings
        var rankings = RankPlayers(room.Players.Values, gameMode).ToList();

        var results = new List<RaceResultDto>();
        for (int i = 0; i < rankings.Count; i++)
        {
            var p = rankings[i];
            results.Add(new RaceResultDto
            {
                UserId = p.UserId,
                Username = p.Username,
                Position = i + 1,
                Wpm = (decimal)p.CurrentWpm,
                Accuracy = (decimal)p.CurrentAccuracy,
                CharsCorrect = p.CorrectChars,
                CharsWrong = p.WrongChars,
                TimeTakenMs = p.TimeTakenMs,
                IsCompleted = p.IsCompleted,
                TypedText = p.TypedText,
                GameMode = gameMode,
                BackspaceCount = p.BackspaceCount,
                BestStreak = p.BestStreak,
                ConsistencyScore = p.ConsistencyScore,
                IsDisqualified = p.IsDisqualified,
                Achievements = p.Achievements.ToList(),
                IsAiBot = p.IsBot,
                ObservedMistakeCharacters = p.GetObservedMistakeCharactersSnapshot(),
                ObservedMistakeWords = p.GetObservedMistakeWordsSnapshot(),
                ObservedMistakeNgrams = p.GetObservedMistakeNgramsSnapshot(),
            });
        }

        room.LastPassageText = room.PassageText;

        var totalPlayers = Math.Max(1, results.Count(r => !r.IsAiBot));
        var startedAt = room.StartedAt ?? DateTime.UtcNow;
        var raceId = room.RaceId ?? 0;

        if (raceId == 0)
        {
            try
            {
                if (room.DbRoomId <= 0)
                {
                    room.DbRoomId = await _raceRepo.GetOrCreateRoomAsync(room.RoomCode, room.HostUserId);
                }

                raceId = await _raceRepo.CreateRaceAsync(room.DbRoomId, room.RoomCode, room.PassageId, startedAt);
                room.RaceId = raceId;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create race record for room {room.RoomCode}", ex);
            }
        }

        _mistakeMemory.RecordRaceResults(
            room.RoomCode,
            raceId,
            room.LastPassageText ?? room.PassageText ?? string.Empty,
            gameMode,
            results,
            finishedAt);

        // Persist to database in background after race id is known
        _ = Task.Run(async () =>
        {
            try
            {
                if (raceId == 0)
                    return;

                foreach (var result in results.Where(r => !r.IsAiBot && r.UserId > 0))
                {
                    await _raceRepo.InsertResultAsync(raceId, room.RoomCode, result, totalPlayers, finishedAt);
                }

                await _raceRepo.EndRaceAsync(raceId, finishedAt);
                _logger.Info($"Race results saved to DB for room {room.RoomCode}, raceId={raceId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save race results for room {room.RoomCode}", ex);
            }
        });

        // Broadcast results
        var resultPayload = new RaceResultPayload
        {
            RoomCode = room.RoomCode,
            RaceId = raceId,
            Results = results,
        };

        await BroadcastToRoomAsync(room, MessageType.RACE_RESULT, resultPayload);

        // Reset room for next race
        RemoveAiPracticeBots(room);
        room.Status = RoomStatus.Waiting;
        room.PassageText = null;
        room.PassageId = 0;
        room.StartedAt = null;
        room.RaceId = null;
        room.RaceTimeoutCts?.Cancel();
        room.RaceTimeoutCts?.Dispose();
        room.RaceTimeoutCts = null;
        room.AiBotCts?.Cancel();
        room.AiBotCts?.Dispose();
        room.AiBotCts = null;
        room.ResetFinishLock();
        foreach (var player in room.Players.Values)
        {
            player.IsReady = false;
            player.IsCompleted = false;
            player.IsDisqualified = false;
            player.IsFinished = false;
            player.CurrentPosition = 0;
            player.CorrectChars = 0;
            player.WrongChars = 0;
            player.CurrentWpm = 0;
            player.CurrentAccuracy = 0;
            player.TimeTakenMs = 0;
            player.BackspaceCount = 0;
            player.BestStreak = 0;
            player.ConsistencyScore = 0;
            player.Achievements.Clear();
            player.ClearObservedMistakes();
        }
    }

    private void EnsureAiPracticeBot(GameRoom room, string difficulty)
    {
        var normalizedDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(difficulty);
        var botUserId = GetAiPracticeBotUserId(room.RoomCode);
        var targetRpm = Shared.Constants.GetAiPracticeTargetRpm(normalizedDifficulty);

        room.Players[botUserId] = new PlayerState
        {
            UserId = botUserId,
            Username = $"AI {ToAiDifficultyLabel(normalizedDifficulty)} ({targetRpm} RPM)",
            IsReady = true,
            IsBot = true,
            AiPracticeDifficulty = normalizedDifficulty,
            AiTargetRpm = targetRpm,
            CurrentAccuracy = 100.0,
        };
    }

    private static int GetAiPracticeBotUserId(string roomCode)
    {
        var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(roomCode ?? string.Empty);
        if (hash == int.MinValue)
            hash = 0;
        return -100_000 - Math.Abs(hash % 899_999);
    }

    private static void RemoveAiPracticeBots(GameRoom room)
    {
        foreach (var botId in room.Players.Values.Where(p => p.IsBot).Select(p => p.UserId).ToList())
            room.Players.TryRemove(botId, out _);
    }

    private void StartAiPracticeBot(GameRoom room, CancellationToken raceToken)
    {
        var bot = room.Players.Values.FirstOrDefault(p => p.IsBot);
        if (bot == null)
            return;

        room.AiBotCts?.Cancel();
        room.AiBotCts?.Dispose();
        room.AiBotCts = CancellationTokenSource.CreateLinkedTokenSource(raceToken);
        var botToken = room.AiBotCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAiPracticeBotAsync(room, bot, botToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error($"AI practice bot failed in room {room.RoomCode}", ex);
            }
        });
    }

    private async Task RunAiPracticeBotAsync(GameRoom room, PlayerState bot, CancellationToken ct)
    {
        var passageLength = Math.Max(1, room.PassageText?.Length ?? 0);
        var difficulty = Shared.Constants.NormalizeAiPracticeDifficulty(bot.AiPracticeDifficulty);
        var targetRpm = Shared.Constants.GetAiPracticeTargetRpm(difficulty);
        var charsPerSecond = targetRpm * 5.0 / 60.0;
        var phase = Math.Abs(GetAiPracticeBotUserId(room.RoomCode) % 31) / 10.0;

        while (!ct.IsCancellationRequested && room.Status == RoomStatus.Racing && !bot.IsFinished)
        {
            var startedAt = room.StartedAt ?? DateTime.UtcNow;
            var elapsed = Math.Max(0.05, (DateTime.UtcNow - startedAt).TotalSeconds);
            var elapsedMs = Math.Max(1000, (int)(elapsed * 1000));

            var paceWave = 1.0 + Math.Sin(elapsed * 0.85 + phase) * 0.04;
            var targetPosition = Math.Clamp((int)Math.Floor(elapsed * charsPerSecond * paceWave), 0, passageLength);

            if (targetPosition >= passageLength)
            {
                CompleteAiPracticeBot(bot, passageLength, elapsedMs, difficulty);
                await BroadcastToRoomAsync(room, MessageType.PROGRESS_BROADCAST, CreateProgressPayload(room));

                if (AreRaceParticipantsFinished(room))
                    await FinishRaceAsync(room);

                return;
            }

            UpdateAiPracticeBotProgress(bot, targetPosition, passageLength, elapsedMs, difficulty);
            await BroadcastToRoomAsync(room, MessageType.PROGRESS_BROADCAST, CreateProgressPayload(room));
            await Task.Delay(Shared.Constants.ProgressBroadcastIntervalMs, ct);
        }
    }

    private static void UpdateAiPracticeBotProgress(PlayerState bot, int position, int passageLength, int elapsedMs, string difficulty)
    {
        var accuracy = Shared.Constants.GetAiPracticeAccuracy(difficulty);
        var correctedPosition = Math.Clamp(position, 0, passageLength);
        var wrongChars = EstimateWrongChars(correctedPosition, accuracy);

        bot.CurrentPosition = correctedPosition;
        bot.CorrectChars = correctedPosition;
        bot.WrongChars = wrongChars;
        bot.CurrentAccuracy = CalculateAccuracy(correctedPosition, wrongChars);
        bot.CurrentWpm = elapsedMs > 0
            ? (correctedPosition / 5.0) / (elapsedMs / 60000.0)
            : 0;
        bot.BestStreak = Math.Max(bot.BestStreak, correctedPosition);
        bot.AiTargetRpm = Shared.Constants.GetAiPracticeTargetRpm(difficulty);
    }

    private static void CompleteAiPracticeBot(PlayerState bot, int passageLength, int elapsedMs, string difficulty)
    {
        var accuracy = Shared.Constants.GetAiPracticeAccuracy(difficulty);
        var wrongChars = EstimateWrongChars(passageLength, accuracy);

        bot.CurrentPosition = passageLength;
        bot.CorrectChars = passageLength;
        bot.WrongChars = wrongChars;
        bot.CurrentAccuracy = CalculateAccuracy(passageLength, wrongChars);
        bot.CurrentWpm = Shared.Constants.GetAiPracticeTargetRpm(difficulty);
        bot.BestStreak = Math.Max(bot.BestStreak, Math.Max(1, passageLength - wrongChars));
        bot.TimeTakenMs = elapsedMs;
        bot.FinishedAt = DateTime.UtcNow;
        bot.IsFinished = true;
        bot.IsCompleted = true;
        bot.IsDisqualified = false;
        bot.ConsistencyScore = CalculateConsistencyScore(new RaceTypingAnalysis
        {
            CurrentPosition = passageLength,
            CorrectChars = passageLength,
            WrongChars = wrongChars,
            BestStreak = bot.BestStreak,
            TypedLength = passageLength,
        }, passageLength, isCompleted: true, isDisqualified: false);
        bot.Achievements = BuildAchievements(bot, Shared.Constants.GameModeAiPractice, passageLength);
    }

    private static void FinalizeUnfinishedAiPracticeBot(PlayerState bot, string passageText, int elapsedMs)
    {
        var passageLength = Math.Max(0, passageText.Length);
        var difficulty = Shared.Constants.NormalizeAiPracticeDifficulty(bot.AiPracticeDifficulty);
        var targetRpm = Shared.Constants.GetAiPracticeTargetRpm(difficulty);
        var projectedPosition = elapsedMs > 0
            ? (int)Math.Floor(targetRpm * 5.0 * elapsedMs / 60000.0)
            : bot.CurrentPosition;
        var position = Math.Clamp(Math.Max(bot.CurrentPosition, projectedPosition), 0, passageLength);

        if (passageLength > 0 && position >= passageLength)
        {
            CompleteAiPracticeBot(bot, passageLength, elapsedMs, difficulty);
            return;
        }

        UpdateAiPracticeBotProgress(bot, position, passageLength, elapsedMs, difficulty);
        bot.FinishedAt = DateTime.UtcNow;
        bot.IsFinished = true;
        bot.IsCompleted = false;
        bot.IsDisqualified = false;
        bot.TimeTakenMs = elapsedMs;
        bot.ConsistencyScore = CalculateConsistencyScore(new RaceTypingAnalysis
        {
            CurrentPosition = position,
            CorrectChars = bot.CorrectChars,
            WrongChars = bot.WrongChars,
            BestStreak = bot.BestStreak,
            TypedLength = position,
        }, passageLength, isCompleted: false, isDisqualified: false);
        bot.Achievements = BuildAchievements(bot, Shared.Constants.GameModeAiPractice, passageLength);
    }

    private static int EstimateWrongChars(int correctChars, double accuracy)
    {
        if (correctChars <= 0 || accuracy >= 100.0)
            return 0;

        var wrong = correctChars * (100.0 - accuracy) / Math.Max(1.0, accuracy);
        return Math.Max(0, (int)Math.Round(wrong, MidpointRounding.AwayFromZero));
    }

    private static double CalculateAccuracy(int correctChars, int wrongChars)
    {
        var total = correctChars + wrongChars;
        return total > 0
            ? (double)correctChars / total * 100.0
            : 100.0;
    }

    private static ProgressBroadcastPayload CreateProgressPayload(GameRoom room)
    {
        return new ProgressBroadcastPayload
        {
            RoomCode = room.RoomCode,
            Players = room.Players.Values.Select(p => new PlayerProgressDto
            {
                UserId = p.UserId,
                Username = p.Username,
                Progress = room.PassageText != null && room.PassageText.Length > 0
                    ? Math.Min(1.0, (double)p.CurrentPosition / room.PassageText.Length)
                    : 0,
                Wpm = p.CurrentWpm,
                IsFinished = p.IsFinished,
                IsAiBot = p.IsBot,
            }).ToList(),
        };
    }

    private static bool AreRaceParticipantsFinished(GameRoom room)
    {
        var gameMode = Shared.Constants.NormalizeGameMode(room.GameMode);
        if (gameMode == Shared.Constants.GameModeAiPractice)
            return room.Players.Values.Where(p => !p.IsBot).All(p => p.IsFinished);

        return room.Players.Values.All(p => p.IsFinished);
    }

    private async Task<PassageRow?> PickNextPassageAsync(GameRoom room)
    {
        var customPassage = await TryCreateCustomPassageAsync(room);
        if (customPassage != null)
            return customPassage;

        if (room.EnableAiMode)
        {
            var aiPassage = await TryCreateAiPassageFromRecentErrorsAsync(room);
            if (aiPassage != null)
            {
                return aiPassage;
            }
        }

        var language = NormalizeLanguage(room.PassageLanguage);
        var usedIds = room.GetUsedPassageIdsSnapshot();
        var excluded = usedIds.ToList();

        if (room.LastPassageId > 0 && !excluded.Contains(room.LastPassageId))
        {
            excluded.Add(room.LastPassageId);
        }

        var passage = await _passageRepo.GetRandomAsync(language, excluded);
        if (passage != null)
            return passage;

        // Nếu đã dùng hết bài trong ngôn ngữ hiện tại, reset history và thử lại
        if (room.HasAnyUsedPassages())
        {
            room.ResetPassageHistory();
            var retryExcluded = room.LastPassageId > 0
                ? new[] { room.LastPassageId }
                : Array.Empty<int>();
            passage = await _passageRepo.GetRandomAsync(language, retryExcluded);
            if (passage != null)
                return passage;
        }

        // Fallback cuối: cho phép lấy bất kỳ ngôn ngữ nào để không chặn trận đấu.
        if (language != "any")
        {
            passage = await _passageRepo.GetRandomAsync("any", excluded);
            if (passage != null)
                return passage;

            if (room.HasAnyUsedPassages())
            {
                room.ResetPassageHistory();
                var retryExcluded = room.LastPassageId > 0
                    ? new[] { room.LastPassageId }
                    : Array.Empty<int>();
                passage = await _passageRepo.GetRandomAsync("any", retryExcluded);
                if (passage != null)
                    return passage;
            }
        }

        // Trường hợp cực hạn (DB quá ít bài): cho phép dùng lại để không chặn game.
        passage = await _passageRepo.GetRandomAsync(language);
        if (passage != null)
            return passage;

        if (language != "any")
        {
            passage = await _passageRepo.GetRandomAsync("any");
            if (passage != null)
                return passage;
        }

        return null;
    }

    private async Task<PassageRow?> TryCreateCustomPassageAsync(GameRoom room)
    {
        var customText = NormalizeTextForAi(room.CustomPassageText);
        if (string.IsNullOrWhiteSpace(customText) || customText.Length < 40)
            return null;

        var language = NormalizeLanguage(room.PassageLanguage);
        if (language == "any")
            language = VietnameseTextHelper.ContainsVietnameseDiacritics(customText) ? "vi" : "en";

        var passageId = await _passageRepo.GetOrCreateByContentAsync(customText, language);
        return new PassageRow
        {
            Id = passageId,
            Content = customText,
            Language = language,
        };
    }

    private async Task<PassageRow?> TryCreateAiPassageFromRecentErrorsAsync(GameRoom room)
    {
        var language = NormalizeLanguage(room.PassageLanguage);
        var sourcePassage = room.LastPassageText;
        if (string.IsNullOrWhiteSpace(sourcePassage))
            return null;

        var typedSamples = room.Players.Values
            .Select(p => NormalizeTypedText(p.TypedText))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (!typedSamples.Any())
            return null;

        var passageText = BuildTargetedPassageFromErrors(sourcePassage, typedSamples, language);
        if (string.IsNullOrWhiteSpace(passageText))
            return null;

        if (passageText == sourcePassage)
            return null;

        var passageId = await _passageRepo.GetOrCreateByContentAsync(passageText, language);
        return new PassageRow
        {
            Id = passageId,
            Content = passageText,
            Language = language,
        };
    }

    private static string BuildTargetedPassageFromErrors(string basePassage, IReadOnlyList<string> typedSamples, string language)
    {
        var normalizedPassage = NormalizeTextForAi(basePassage);
        var normalizedSamples = typedSamples
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(NormalizeTextForAi)
            .ToList();

        if (!normalizedSamples.Any())
            return string.Empty;

        var topChars = AnalyzeMistakeCharacters(normalizedPassage, normalizedSamples)
            .Take(3)
            .Select(x => x.Key)
            .ToList();

        var topWords = AnalyzeMistakeWords(normalizedPassage, normalizedSamples)
            .Take(2)
            .Select(x => x.Key)
            .ToList();

        if (topChars.Count == 0 && topWords.Count == 0)
            return string.Empty;

        if (language == "vi")
        {
            return BuildVietnameseTargetedPassage(topChars, topWords);
        }

        return BuildEnglishTargetedPassage(topChars, topWords);
    }

    private static IEnumerable<KeyValuePair<char, int>> AnalyzeMistakeCharacters(string passage, IEnumerable<string> typedSamples)
    {
        var counts = new Dictionary<char, int>();

        foreach (var typed in typedSamples)
        {
            var max = Math.Max(passage.Length, typed.Length);
            for (int i = 0; i < max; i++)
            {
                var expected = i < passage.Length ? passage[i] : '\0';
                var actual = i < typed.Length ? typed[i] : '\0';

                if (expected == '\0' && actual == '\0')
                    continue;

                if (actual != expected && expected != '\0')
                {
                    counts[expected] = counts.GetValueOrDefault(expected) + 1;
                }
            }
        }

        return counts
            .Where(kv => !char.IsWhiteSpace(kv.Key) && !char.IsControl(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key);
    }

    private static readonly Regex TokenRegex = new(@"\b[\p{L}\p{M}]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static IEnumerable<KeyValuePair<string, int>> AnalyzeMistakeWords(string passage, IEnumerable<string> typedSamples)
    {
        var passageTokens = TokenRegex.Matches(passage)
            .Select(m => NormalizeTextForAi(m.Value).ToLowerInvariant())
            .Where(t => t.Length > 0)
            .ToList();

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var typed in typedSamples)
        {
            var typedTokens = TokenRegex.Matches(typed)
                .Select(m => NormalizeTextForAi(m.Value).ToLowerInvariant())
                .Where(t => t.Length > 0)
                .ToList();

            var max = Math.Max(passageTokens.Count, typedTokens.Count);
            for (int i = 0; i < max; i++)
            {
                var expected = i < passageTokens.Count ? passageTokens[i] : string.Empty;
                var actual = i < typedTokens.Count ? typedTokens[i] : string.Empty;

                if (string.IsNullOrEmpty(actual) && string.IsNullOrEmpty(expected))
                    continue;

                if (expected != actual)
                {
                    if (!string.IsNullOrWhiteSpace(expected))
                        counts[expected] = counts.GetValueOrDefault(expected) + 1;

                    if (!string.IsNullOrWhiteSpace(actual) && actual != expected)
                        counts[actual] = counts.GetValueOrDefault(actual) + 1;
                }
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Where(kv => kv.Key.Length >= 2);
    }

    private static string BuildVietnameseTargetedPassage(IReadOnlyList<char> chars, IReadOnlyList<string> words)
    {
        var charBlocks = BuildErrorBlocks(chars, 24);
        var wordBlocks = BuildWordBlocks(words, 16);

        return $"Luyện gõ chính xác các chỗ khó: {charBlocks} {wordBlocks}. " +
               "Bạn giữ nhịp đều, nhìn trước một chữ rồi mới bấm tiếp để giảm lỗi. " +
               $"Tập trung gõ đúng các ký tự này: {charBlocks}.";
    }

    private static string BuildEnglishTargetedPassage(IReadOnlyList<char> chars, IReadOnlyList<string> words)
    {
        var charBlocks = BuildErrorBlocks(chars, 24);
        var wordBlocks = BuildWordBlocks(words, 16);

        return $"Practice this focused drill: {charBlocks} {wordBlocks}. " +
               "Keep a calm rhythm and correct every miss as soon as it happens. " +
               $"Type the sequence again: {charBlocks}.";
    }

    private static string BuildErrorBlocks(IReadOnlyList<char> chars, int repeat)
    {
        if (chars.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var ch in chars)
        {
            for (int i = 0; i < 2; i++)
                sb.Append(ch);
            sb.Append(' ');
        }

        var pattern = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(pattern))
            return string.Empty;

        while (pattern.Length < repeat)
        {
            pattern = $"{pattern} {pattern}";
        }

        return pattern.Trim();
    }

    private static string BuildWordBlocks(IReadOnlyList<string> words, int repeat)
    {
        if (words.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var w in words)
        {
            sb.Append(w);
            sb.Append(' ');
        }

        var pattern = sb.ToString().Trim();
        while (pattern.Length < repeat)
        {
            pattern = $"{pattern} {pattern}";
        }

        return pattern.Trim();
    }

    private static string NormalizeTextForAi(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var buffer = new char[normalized.Length];
        var length = 0;

        foreach (var ch in normalized)
        {
            if (char.IsControl(ch) && ch != '\n')
                continue;

            buffer[length++] = char.IsWhiteSpace(ch) ? ' ' : ch;
        }

        return length == 0
            ? string.Empty
            : new string(buffer, 0, length);
    }

    private static string NormalizeTypedText(string value)
    {
        return NormalizeTextForAi(value);
    }

    private static RaceTypingAnalysis AnalyzeTypedText(string passage, string typed)
    {
        var expected = NormalizeTextForAi(passage);
        var actual = NormalizeTypedText(typed);
        var limit = Math.Min(expected.Length, actual.Length);
        var correct = 0;
        var wrong = 0;
        var streak = 0;
        var bestStreak = 0;

        for (var i = 0; i < limit; i++)
        {
            if (actual[i] == expected[i])
            {
                correct++;
                streak++;
                bestStreak = Math.Max(bestStreak, streak);
            }
            else
            {
                wrong++;
                streak = 0;
            }
        }

        if (actual.Length > expected.Length)
            wrong += actual.Length - expected.Length;

        return new RaceTypingAnalysis
        {
            CurrentPosition = Math.Min(actual.Length, expected.Length),
            CorrectChars = correct,
            WrongChars = wrong,
            BestStreak = bestStreak,
            TypedLength = actual.Length,
        };
    }

    private static RaceTypingAnalysis AnalyzeClientCounters(int currentPosition, int correctChars, int wrongChars, int maxChars)
    {
        var cappedCorrect = Math.Clamp(correctChars, 0, maxChars);
        var cappedWrong = Math.Max(0, Math.Min(wrongChars, Math.Max(maxChars * 3, 1)));
        return new RaceTypingAnalysis
        {
            CurrentPosition = Math.Clamp(currentPosition, 0, maxChars),
            CorrectChars = cappedCorrect,
            WrongChars = cappedWrong,
            BestStreak = cappedCorrect,
            TypedLength = Math.Clamp(currentPosition, 0, maxChars),
        };
    }

    private static decimal CalculateConsistencyScore(RaceTypingAnalysis analysis, int passageLength, bool isCompleted, bool isDisqualified)
    {
        var totalChars = Math.Max(1, analysis.CorrectChars + analysis.WrongChars);
        var accuracy = (decimal)analysis.CorrectChars / totalChars * 100m;
        var completion = passageLength > 0
            ? Math.Clamp((decimal)analysis.CurrentPosition / passageLength, 0m, 1m)
            : 0m;
        var score = accuracy * 0.70m + completion * 30m;

        if (!isCompleted)
            score -= 6m;
        if (isDisqualified)
            score = Math.Min(score, 30m);

        return Math.Clamp(decimal.Round(score, 1, MidpointRounding.AwayFromZero), 0m, 100m);
    }

    private static void FinalizeUnfinishedPlayer(PlayerState player, string passageText, int elapsedMs, string gameMode)
    {
        var analysis = string.IsNullOrEmpty(player.TypedText)
            ? AnalyzeClientCounters(player.CurrentPosition, player.CorrectChars, player.WrongChars, passageText.Length)
            : AnalyzeTypedText(passageText, player.TypedText);

        player.FinishedAt = DateTime.UtcNow;
        player.IsFinished = true;
        player.IsCompleted = player.IsBot && passageText.Length > 0 && analysis.CurrentPosition >= passageText.Length;
        player.IsDisqualified =
            (gameMode == Shared.Constants.GameModeSuddenDeath && analysis.WrongChars > 0) ||
            (gameMode == Shared.Constants.GameModeNoBackspace && player.BackspaceCount > 0);
        player.TimeTakenMs = elapsedMs;
        player.CurrentPosition = analysis.CurrentPosition;
        player.CorrectChars = analysis.CorrectChars;
        player.WrongChars = analysis.WrongChars;
        player.BestStreak = Math.Max(player.BestStreak, analysis.BestStreak);
        player.ConsistencyScore = CalculateConsistencyScore(analysis, passageText.Length, player.IsCompleted, player.IsDisqualified);

        var totalChars = analysis.CorrectChars + analysis.WrongChars;
        player.CurrentAccuracy = totalChars > 0
            ? (double)analysis.CorrectChars / totalChars * 100.0
            : 100.0;
        player.CurrentWpm = elapsedMs > 0
            ? (analysis.CorrectChars / 5.0) / (elapsedMs / 60000.0)
            : 0;
        player.Achievements = BuildAchievements(player, gameMode, passageText.Length);
    }

    private static IEnumerable<PlayerState> RankPlayers(IEnumerable<PlayerState> players, string gameMode)
    {
        var query = players.OrderByDescending(p => !p.IsDisqualified);

        if (gameMode == Shared.Constants.GameModeAccuracy)
        {
            return query
                .ThenByDescending(p => p.CurrentAccuracy)
                .ThenByDescending(p => p.IsCompleted)
                .ThenBy(p => p.WrongChars)
                .ThenByDescending(p => p.BestStreak)
                .ThenByDescending(p => p.CurrentWpm);
        }

        return query
            .ThenByDescending(p => p.IsCompleted)
            .ThenByDescending(p => p.CurrentWpm)
            .ThenByDescending(p => p.CurrentAccuracy)
            .ThenByDescending(p => p.BestStreak);
    }

    private static List<string> BuildAchievements(PlayerState player, string gameMode, int passageLength)
    {
        var achievements = new List<string>();

        if (player.IsDisqualified)
        {
            achievements.Add("Bị loại");
            return achievements;
        }

        if (player.IsBot)
            achievements.Add($"AI {ToAiDifficultyLabel(player.AiPracticeDifficulty)}");
        if (player.IsCompleted)
            achievements.Add("Về đích");
        if (player.WrongChars == 0 && player.CorrectChars > 0)
            achievements.Add("Perfect Run");
        if (player.CurrentAccuracy >= 95.0 && player.CorrectChars > 0)
            achievements.Add("Accuracy Master");
        if (player.BestStreak >= Math.Min(50, Math.Max(20, passageLength / 2)))
            achievements.Add("Combo dài");
        if (player.CurrentWpm >= 60.0)
            achievements.Add("Speedster");
        if (player.ConsistencyScore >= 92m)
            achievements.Add("Ổn định cao");
        if (gameMode == Shared.Constants.GameModeNoBackspace && player.BackspaceCount == 0)
            achievements.Add("No Backspace");
        if (gameMode == Shared.Constants.GameModeSuddenDeath && player.WrongChars == 0)
            achievements.Add("Survivor");

        return achievements.Take(5).ToList();
    }

    private static string ToAiDifficultyLabel(string? difficulty)
    {
        return Shared.Constants.NormalizeAiPracticeDifficulty(difficulty) switch
        {
            Shared.Constants.AiPracticeMedium => "Vừa",
            Shared.Constants.AiPracticeHard => "Khó",
            Shared.Constants.AiPracticeNightmare => "Ác mộng",
            _ => "Dễ",
        };
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

    private sealed class RaceTypingAnalysis
    {
        public int CurrentPosition { get; set; }
        public int CorrectChars { get; set; }
        public int WrongChars { get; set; }
        public int BestStreak { get; set; }
        public int TypedLength { get; set; }
    }

    private async Task BroadcastToRoomAsync<T>(GameRoom room, MessageType type, T payload)
    {
        var message = NetworkMessage.Create(type, payload);

        foreach (var player in room.Players.Values)
        {
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
}
