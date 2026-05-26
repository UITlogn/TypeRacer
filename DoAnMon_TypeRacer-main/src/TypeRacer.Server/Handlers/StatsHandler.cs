using TypeRacer.Server.Data;
using TypeRacer.Server.Logging;
using TypeRacer.Server.Network;
using TypeRacer.Server.Services;
using TypeRacer.Server.State;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Ai;
using TypeRacer.Shared.Payloads.Stats;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Server.Handlers;

public class StatsHandler : IMessageHandler
{
    private readonly IUserRepository _userRepo;
    private readonly IRaceRepository _raceRepo;
    private readonly AiCoachService _aiCoachService;
    private readonly MistakeMemoryService _mistakeMemory;
    private readonly FileLogger _logger;

    public StatsHandler(
        IUserRepository userRepo,
        IRaceRepository raceRepo,
        AiCoachService aiCoachService,
        MistakeMemoryService mistakeMemory,
        FileLogger logger)
    {
        _userRepo = userRepo;
        _raceRepo = raceRepo;
        _aiCoachService = aiCoachService;
        _mistakeMemory = mistakeMemory;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case MessageType.GET_PROFILE:
                await HandleGetProfileAsync(client, session, message, ct);
                break;
            case MessageType.GET_LEADERBOARD:
                await HandleGetLeaderboardAsync(client, session, message, ct);
                break;
            case MessageType.GET_MATCH_HISTORY:
                await HandleGetMatchHistoryAsync(client, session, message, ct);
                break;
            case MessageType.GET_AI_COACH:
                await HandleGetAiCoachAsync(client, session, message, ct);
                break;
        }
    }

    private async Task HandleGetProfileAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<GetProfileRequest>();
        var userId = request?.UserId ?? client.UserId;

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null)
        {
            await session.SendAsync(MessageType.PROFILE_RESPONSE, new ProfileResponse
            {
                Success = false,
                ErrorMessage = "User not found.",
            }, ct);
            return;
        }

        await session.SendAsync(MessageType.PROFILE_RESPONSE, new ProfileResponse
        {
            Success = true,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
            },
        }, ct);
    }

    private async Task HandleGetLeaderboardAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<GetLeaderboardRequest>();
        var top = Math.Clamp(request?.Top ?? 20, 1, 100);
        var sortBy = request?.SortBy ?? "avg_wpm";

        var rows = await _userRepo.GetLeaderboardAsync(top, sortBy);

        var entries = rows.Select((r, i) => new LeaderboardEntryDto
        {
            Rank = i + 1,
            UserId = r.UserId,
            Username = r.Username,
            AvgWpm = r.AvgWpm,
            BestWpm = r.BestWpm,
            TotalRaces = r.TotalRaces,
            TotalWins = r.TotalWins,
        }).ToList();

        await session.SendAsync(MessageType.LEADERBOARD_RESP, new LeaderboardResponse
        {
            Entries = entries,
        }, ct);
    }

    private async Task HandleGetMatchHistoryAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<GetMatchHistoryRequest>();
        var userId = request?.UserId ?? client.UserId;
        var limit = Math.Clamp(request?.Limit ?? 20, 1, 100);

        var history = await _raceRepo.GetMatchHistoryAsync(userId, limit);

        var matches = history.Select(h => new MatchHistoryEntryDto
        {
            RaceId = h.RaceId,
            RoomCode = h.RoomCode,
            Position = h.Position,
            TotalPlayers = h.TotalPlayers,
            Wpm = h.Wpm,
            Accuracy = h.Accuracy,
            TimeTakenMs = h.TimeTakenMs,
            IsCompleted = h.IsCompleted,
            PlayedAt = h.PlayedAt,
        }).ToList();

        await session.SendAsync(MessageType.MATCH_HISTORY_RESP, new MatchHistoryResponse
        {
            Matches = matches,
        }, ct);
    }

    private async Task HandleGetAiCoachAsync(ConnectedClient client, ClientSession session, NetworkMessage message, CancellationToken ct)
    {
        var request = message.GetPayload<GetAiCoachRequest>();
        if (request == null)
        {
            await session.SendErrorAsync(Shared.Enums.ErrorCode.InvalidMessage, "Yêu cầu AI coach không hợp lệ.", ct);
            return;
        }

        try
        {
            var recentHistory = await _raceRepo.GetMatchHistoryAsync(client.UserId, 8);
            InjectRecentPerformanceContext(request, recentHistory);
            var mistakeSamples = _mistakeMemory.GetSamples(client.UserId, request.RoomCode, request.RaceId, 12);

            client.HeartbeatGraceUntil = DateTime.UtcNow.AddMinutes(5);
            var response = await _aiCoachService.GenerateCoachAsync(client.UserId, client.Username, request, mistakeSamples, ct);
            client.LastHeartbeat = DateTime.UtcNow;
            await session.SendAsync(MessageType.AI_COACH_RESPONSE, response, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"AI coach failed for user={client.Username}", ex);
            await session.SendAsync(MessageType.AI_COACH_RESPONSE, new AiCoachResponse
            {
                Success = false,
                Provider = "error",
                Model = "none",
                IsFallback = true,
                CoachText = string.Empty,
                Tips = new List<string>(),
                ActionPlan = new List<string>(),
                ErrorMessage = "Không thể tạo phân tích AI lúc này.",
                RaceId = request.RaceId,
                UserId = client.UserId,
            }, ct);
        }
        finally
        {
            client.HeartbeatGraceUntil = DateTime.MinValue;
            client.LastHeartbeat = DateTime.UtcNow;
        }
    }

    private static void InjectRecentPerformanceContext(GetAiCoachRequest request, IReadOnlyList<MatchHistoryRow> history)
    {
        if (history.Count == 0)
            return;

        var races = history
            .Where(x => x != null)
            .OrderByDescending(x => x.PlayedAt)
            .Take(8)
            .ToList();

        if (races.Count == 0)
            return;

        request.RecentRaceCount = races.Count;
        request.RecentCompletedCount = races.Count(x => x.IsCompleted);
        request.RecentAvgWpm = decimal.Round(races.Average(x => x.Wpm), 1);
        request.RecentAvgAccuracy = decimal.Round(races.Average(x => x.Accuracy), 1);

        var recentWindow = races.Take(Math.Min(4, races.Count)).ToList();
        var previousWindow = races.Skip(recentWindow.Count).Take(Math.Min(4, races.Count - recentWindow.Count)).ToList();

        if (previousWindow.Count > 0)
        {
            request.RecentWpmTrend = decimal.Round(
                recentWindow.Average(x => x.Wpm) - previousWindow.Average(x => x.Wpm),
                1);

            request.RecentAccuracyTrend = decimal.Round(
                recentWindow.Average(x => x.Accuracy) - previousWindow.Average(x => x.Accuracy),
                1);
        }
        else
        {
            request.RecentWpmTrend = 0m;
            request.RecentAccuracyTrend = 0m;
        }
    }
}
