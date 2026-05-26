using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TypeRacer.Server.Logging;
using TypeRacer.Shared;
using TypeRacer.Shared.Payloads.Ai;

namespace TypeRacer.Server.Services;

public class AiCoachOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "openclaude";
    public string Model { get; set; } = "gpt-5.5";
    public string Endpoint { get; set; } = "https://open-claude.com/v1/chat/completions";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;

    // Compatibility aliases for older config keys.
    public string OpenAiModel { get; set; } = string.Empty;
    public string OpenAiBaseUrl { get; set; } = string.Empty;
    public string OpenAiApiKey { get; set; } = string.Empty;
    public int TimeoutMs { get; set; }
    public int MaxRetries { get; set; } = 3;

    public string ResolveModel()
        => !string.IsNullOrWhiteSpace(OpenAiModel) ? OpenAiModel : Model;

    public string ResolveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(OpenAiBaseUrl))
        {
            var baseUrl = OpenAiBaseUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return $"{baseUrl}/chat/completions";
            return $"{baseUrl}/v1/chat/completions";
        }
        var provider = (Provider ?? string.Empty).Trim().ToLowerInvariant();
        if (provider == "openclaude" &&
            (string.IsNullOrWhiteSpace(Endpoint) ||
             Endpoint.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase)))
        {
            return "https://open-claude.com/v1/chat/completions";
        }

        return Endpoint;
    }

    public string ResolveApiKey()
        => !string.IsNullOrWhiteSpace(OpenAiApiKey) ? OpenAiApiKey : ApiKey;

    public int ResolveTimeoutSeconds()
    {
        if (TimeoutMs > 0)
            return Math.Clamp(TimeoutMs / 1000, 3, 90);
        return Math.Clamp(TimeoutSeconds, 3, 90);
    }

    public int ResolveMaxRetries()
        => Math.Clamp(MaxRetries, 1, 5);
}

public class AiCoachService
{
    private const int MaxTypedTextForAnalysis = 1_200;
    private const int TopCharacterMistakeLimit = 6;
    private const int TopWordMistakeLimit = 4;
    private const int TopNgramMistakeLimit = 6;
    private const int MaxMistakeDensityPrecision = 3;
    private const int SuggestedPassageLimit = 12;
    private const int PracticeMissionLimit = 6;

    private static readonly HttpClient Http = new();
    private static readonly Regex TokenRegex = new(@"\p{L}[\p{L}\p{Mn}\p{Pd}']*", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private readonly AiCoachOptions _options;
    private readonly FileLogger _logger;

    public AiCoachService(AiCoachOptions options, FileLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<AiCoachResponse> GenerateCoachAsync(
        int userId,
        string username,
        GetAiCoachRequest request,
        IReadOnlyList<MistakeMemorySample> mistakeSamples,
        CancellationToken ct)
    {
        var safeRequest = Normalize(request);
        var safeSamples = NormalizeMistakeSamples(mistakeSamples);
        var analysis = AnalyzePerformance(safeRequest, safeSamples);

        if (!_options.Enabled)
            return BuildFallback(safeRequest, analysis, "AI coach đang tắt trong cấu hình.", userId, safeSamples.Count);

        var provider = (_options.Provider ?? string.Empty).Trim().ToLowerInvariant();
        var model = _options.ResolveModel();
        var endpoint = _options.ResolveEndpoint();
        var apiKey = _options.ResolveApiKey();
        var timeoutSeconds = _options.ResolveTimeoutSeconds();
        var maxRetries = _options.ResolveMaxRetries();

        if (IsOpenAiCompatibleProvider(provider) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                using var aiBudgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var totalBudgetSeconds = Math.Clamp(
                    timeoutSeconds * Math.Min(maxRetries, 3) + 15,
                    timeoutSeconds + 8,
                    260);
                aiBudgetCts.CancelAfter(TimeSpan.FromSeconds(totalBudgetSeconds));

                var generatedCoach = await GenerateOpenAiCoachAsync(
                    username,
                    safeRequest,
                    analysis,
                    safeSamples,
                    model,
                    endpoint,
                    apiKey,
                    timeoutSeconds,
                    maxRetries,
                    safeRequest.Language,
                    aiBudgetCts.Token);

                return BuildResponse(
                    success: true,
                    coachText: generatedCoach.CoachText,
                    request: safeRequest,
                    analysis: analysis,
                    provider: provider,
                    model: model,
                    isFallback: false,
                    errorMessage: null,
                    raceId: safeRequest.RaceId,
                    userId: safeRequest.UserId > 0 ? safeRequest.UserId : userId,
                    generatedCoach: generatedCoach,
                    mistakeSampleCount: safeSamples.Count);
            }
            catch (Exception ex)
            {
                _logger.Warn($"AI coach {provider} failed for userId={userId}: {ex.Message}");
                return BuildFallback(safeRequest, analysis, $"{provider} failed: {ex.Message}", userId, safeSamples.Count);
            }
        }

        if (IsOpenAiCompatibleProvider(provider) && string.IsNullOrWhiteSpace(apiKey))
            return BuildFallback(safeRequest, analysis, "Thiếu API key AI. Dùng coach nội bộ.", userId, safeSamples.Count);

        return BuildFallback(safeRequest, analysis, "Provider AI không hợp lệ. Dùng coach nội bộ.", userId, safeSamples.Count);
    }

    private static bool IsOpenAiCompatibleProvider(string provider)
        => provider == "openai" || provider == "openclaude";

    private static async Task<AiGeneratedCoach> GenerateOpenAiCoachAsync(
        string username,
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<MistakeMemorySample> mistakeSamples,
        string model,
        string endpoint,
        string apiKey,
        int timeoutSeconds,
        int maxRetries,
        string languageCode,
        CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Clamp(maxRetries, 1, 5); attempt++)
        {
            try
            {
                var prompt = BuildPrompt(username, request, analysis, mistakeSamples, attempt);
                var generated = await SendOpenAiCoachRequestAsync(
                    prompt,
                    model,
                    endpoint,
                    apiKey,
                    timeoutSeconds,
                    languageCode,
                    ct);

                ValidateGeneratedCoach(generated, request, analysis);
                return generated;
            }
            catch (Exception ex) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        throw new InvalidOperationException($"AI không tạo được bài luyện hợp lệ sau {maxRetries} lần thử: {lastError?.Message}");
    }

    private static async Task<AiGeneratedCoach> SendOpenAiCoachRequestAsync(
        string prompt,
        string model,
        string endpoint,
        string apiKey,
        int timeoutSeconds,
        string languageCode,
        CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = model,
            temperature = 0.52,
            max_tokens = 2600,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = BuildSystemPrompt(languageCode)
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        });

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 3, 90)));

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpReq.Headers.UserAgent.ParseAdd("TypeRacer/1.0");
        httpReq.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var httpResp = await Http.SendAsync(httpReq, linkedCts.Token);
        var respText = await httpResp.Content.ReadAsStringAsync(linkedCts.Token);

        if (!httpResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)httpResp.StatusCode}: {respText}");

        using var doc = JsonDocument.Parse(respText);
        var text = ExtractOpenAiContent(doc.RootElement);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("AI trả về nội dung rỗng.");

        return ParseGeneratedCoach(text);
    }

    private static string BuildSystemPrompt(string languageCode)
    {
        if (NormalizeLanguage(languageCode) == "en")
        {
            return "You are an expert typing coach. Return strict JSON only. Create fresh typing practice passages from the user's observed mistakes.";
        }

        return "Bạn là huấn luyện viên gõ phím chuyên nghiệp. Chỉ trả về JSON hợp lệ. Hãy tạo bài luyện mới dựa trên lỗi người chơi đã thật sự gõ sai.";
    }

    private static string? ExtractOpenAiContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                {
                    sb.Append(part.GetString());
                    continue;
                }

                if (part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("type", out var typeNode) &&
                    typeNode.GetString() == "text" &&
                    part.TryGetProperty("text", out var textNode))
                {
                    sb.Append(textNode.GetString());
                }
            }

            return sb.ToString();
        }

        return null;
    }

    private static string NormalizeCoachOutput(string? text)
    {
        var normalized = NormalizeTextForCoach(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var lines = normalized
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .ToList();

        var compact = string.Join('\n', lines);
        if (compact.Length > 1800)
            compact = compact[..1800].TrimEnd();
        return compact;
    }

    private static AiGeneratedCoach ParseGeneratedCoach(string rawText)
    {
        var jsonText = ExtractJsonObject(rawText);
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        var coachText = NormalizeCoachOutput(ReadString(root, "coach_text"));
        if (string.IsNullOrWhiteSpace(coachText))
            coachText = NormalizeCoachOutput(ReadString(root, "summary"));

        var generated = new AiGeneratedCoach
        {
            CoachText = coachText,
            TrainingTitle = ReadString(root, "training_title"),
            RecommendedGameMode = Constants.NormalizeGameMode(ReadString(root, "recommended_game_mode")),
            RecommendedDifficulty = Constants.NormalizeAiPracticeDifficulty(ReadString(root, "recommended_difficulty")),
            RecommendedTargetRpm = ReadInt(root, "recommended_target_rpm"),
            GhostTargetWpm = ReadDecimal(root, "ghost_target_wpm"),
            GhostTargetAccuracy = ReadDecimal(root, "ghost_target_accuracy"),
            GhostRewardBadge = ReadString(root, "ghost_reward_badge"),
            DailyChallengeTitle = ReadString(root, "daily_challenge_title"),
            DailyChallengeGoal = ReadString(root, "daily_challenge_goal"),
            DailyChallengeReward = ReadString(root, "daily_challenge_reward"),
            Tips = ReadStringList(root, "tips", 6),
            ActionPlan = ReadStringList(root, "action_plan", 6),
            PersonalizedDrills = ReadStringList(root, "personalized_drills", 6),
            PracticeWords = ReadStringList(root, "practice_words", 10),
            AdaptiveMicroLessons = ReadStringList(root, "adaptive_micro_lessons", 6),
            MistakeHeatmap = ReadStringList(root, "mistake_heatmap", 8),
            NextSessionChecklist = ReadStringList(root, "next_session_checklist", 6),
            GhostRacePlan = ReadStringList(root, "ghost_race_plan", 5),
            FingerDiagnostics = ReadStringList(root, "finger_diagnostics", 6),
            ProgressPrediction = ReadStringList(root, "progress_prediction", 6),
            LessonLadder = ReadStringList(root, "lesson_ladder", 6),
            AttemptReplayCues = ReadStringList(root, "attempt_replay_cues", 6),
            WeakKeyDrills = ReadStringList(root, "weak_key_drills", 8),
            NgramDrills = ReadStringList(root, "ngram_drills", 8),
            SpacedRepetitionPlan = ReadStringList(root, "spaced_repetition_plan", 6),
            MasteryCheckpoints = ReadStringList(root, "mastery_checkpoints", 6),
            PracticeMissions = ReadMissionList(root, "practice_missions", PracticeMissionLimit),
            ProblemKeyStoryTitle = ReadString(root, "problem_key_story_title"),
            ProblemKeyStoryTopic = ReadString(root, "problem_key_story_topic"),
            ProblemKeyStoryPassage = CleanSuggestedPassage(ReadString(root, "problem_key_story_passage")),
            ProblemKeyStoryKeys = ReadStringList(root, "problem_key_story_keys", 6)
                .Select(ParseMistakeEntry)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 24)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList(),
            MistakeFingerprint = ReadStringList(root, "mistake_fingerprint", 6),
            AdaptiveRaceStrategy = ReadStringList(root, "adaptive_race_strategy", 6),
            PersonalizationScore = ReadDecimal(root, "personalization_score"),
            SuggestedPassages = ReadStringList(root, "suggested_passages", SuggestedPassageLimit)
                .Select(CleanSuggestedPassage)
                .Where(x => x.Length >= 40)
                .ToList(),
        };

        if (string.IsNullOrWhiteSpace(generated.CoachText))
            throw new InvalidOperationException("AI JSON thiếu coach_text.");

        return generated;
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node))
            return 0;

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var value))
            return value;

        if (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out var parsed))
            return parsed;

        return 0;
    }

    private static decimal ReadDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node))
            return 0m;

        if (node.ValueKind == JsonValueKind.Number && node.TryGetDecimal(out var value))
            return value;

        if (node.ValueKind == JsonValueKind.String && decimal.TryParse(node.GetString(), out var parsed))
            return parsed;

        return 0m;
    }

    private static string ExtractJsonObject(string rawText)
    {
        var text = (rawText ?? string.Empty).Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = Regex.Replace(text, @"^```(?:json)?\s*", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*```$", string.Empty);
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("AI không trả JSON object hợp lệ.");

        return text[start..(end + 1)];
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
            return string.Empty;

        return NormalizeTextForCoach(node.GetString());
    }

    private static List<string> ReadStringList(JsonElement root, string propertyName, int limit)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return node.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => NormalizeTextForCoach(item.GetString()))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();
    }

    private static List<AiPracticeMissionDto> ReadMissionList(JsonElement root, string propertyName, int limit)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Array)
            return new List<AiPracticeMissionDto>();

        var missions = new List<AiPracticeMissionDto>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var mission = NormalizeMission(new AiPracticeMissionDto
            {
                Title = ReadString(item, "title"),
                Objective = ReadString(item, "objective"),
                GameMode = ReadString(item, "game_mode"),
                Difficulty = ReadString(item, "difficulty"),
                DurationSeconds = ReadInt(item, "duration_seconds"),
                TargetWpm = ReadDecimal(item, "target_wpm"),
                TargetAccuracy = ReadDecimal(item, "target_accuracy"),
                TargetRpm = ReadInt(item, "target_rpm"),
                Passage = ReadString(item, "passage"),
                RewardBadge = ReadString(item, "reward_badge"),
                SourceWeakspot = ReadString(item, "source_weakspot"),
            });

            if (IsUsableMission(mission))
                missions.Add(mission);

            if (missions.Count >= Math.Clamp(limit, 1, PracticeMissionLimit))
                break;
        }

        return missions;
    }

    private static string CleanSuggestedPassage(string passage)
    {
        var clean = NormalizeTextForCoach(passage);
        clean = Regex.Replace(clean, @"^\s*(?:\d+[\.\)]|-)\s*", string.Empty);
        if (clean.Length > 240)
            clean = clean[..240].TrimEnd();
        return clean;
    }

    private static AiPracticeMissionDto NormalizeMission(AiPracticeMissionDto mission)
    {
        var passage = CleanSuggestedPassage(mission.Passage);
        var targetAccuracy = mission.TargetAccuracy <= 0 ? 95m : mission.TargetAccuracy;
        var targetWpm = mission.TargetWpm <= 0 ? Math.Max(20m, mission.TargetRpm / 5m) : mission.TargetWpm;
        var difficulty = Constants.NormalizeAiPracticeDifficulty(mission.Difficulty);
        var targetRpm = mission.TargetRpm > 0
            ? Math.Clamp(mission.TargetRpm, 15, 180)
            : Constants.GetAiPracticeTargetRpm(difficulty);

        return new AiPracticeMissionDto
        {
            Title = NormalizeCoachOutput(mission.Title).Length == 0
                ? "AI Practice Mission"
                : NormalizeCoachOutput(mission.Title),
            Objective = NormalizeCoachOutput(mission.Objective).Length == 0
                ? "Hoàn thành mission với accuracy ổn định và giảm lỗi weakspot chính."
                : NormalizeCoachOutput(mission.Objective),
            GameMode = Constants.NormalizeGameMode(mission.GameMode),
            Difficulty = difficulty,
            DurationSeconds = Math.Clamp(mission.DurationSeconds <= 0 ? 180 : mission.DurationSeconds, 30, 600),
            TargetWpm = Math.Clamp(decimal.Round(targetWpm, 1), 10m, 250m),
            TargetAccuracy = Math.Clamp(decimal.Round(targetAccuracy, 1), 70m, 99.9m),
            TargetRpm = targetRpm,
            Passage = passage,
            RewardBadge = NormalizeCoachOutput(mission.RewardBadge).Length == 0
                ? "AI Mission Clear"
                : NormalizeCoachOutput(mission.RewardBadge),
            SourceWeakspot = NormalizeCoachOutput(mission.SourceWeakspot).Length == 0
                ? "weakspot chính"
                : NormalizeCoachOutput(mission.SourceWeakspot),
        };
    }

    private static bool IsUsableMission(AiPracticeMissionDto mission)
        => !string.IsNullOrWhiteSpace(mission.Title) &&
           !string.IsNullOrWhiteSpace(mission.Objective) &&
           !string.IsNullOrWhiteSpace(mission.Passage) &&
           mission.Passage.Length >= 40 &&
           mission.DurationSeconds is >= 30 and <= 600 &&
           mission.TargetAccuracy is >= 70m and <= 99.9m &&
           mission.TargetWpm is >= 10m and <= 250m;

    private static void ValidateGeneratedCoach(AiGeneratedCoach generated, GetAiCoachRequest request, CoachAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(generated.CoachText))
            throw new InvalidOperationException("AI JSON thiếu coach_text.");

        if (generated.SuggestedPassages.Count < 1)
            throw new InvalidOperationException("AI không tạo đoạn luyện nào.");

        var distinctPassages = generated.SuggestedPassages
            .Where(p => !string.IsNullOrWhiteSpace(p) && p.Length >= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctPassages.Count < 4)
            throw new InvalidOperationException("AI tạo đoạn luyện quá ít hoặc bị trùng.");

        var original = NormalizeTextForCoach(request.PassageText).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(original))
        {
            var copied = distinctPassages.Any(p =>
            {
                var normalized = NormalizeTextForCoach(p).ToLowerInvariant();
                return normalized.Length >= 40 &&
                       (normalized == original ||
                        (original.Length >= 80 && original.Contains(normalized, StringComparison.OrdinalIgnoreCase)));
            });

            if (copied)
                throw new InvalidOperationException("AI copy lại đề gốc thay vì tạo bài luyện mới.");
        }

        var targetTokens = analysis.TopMistypedCharacters
            .Concat(analysis.TopMistypedWords)
            .Concat(analysis.TopMistypedNgrams)
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 24)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetTokens.Count > 0)
        {
            var containsTarget = distinctPassages.Any(p =>
                targetTokens.Any(token => p.Contains(token, StringComparison.OrdinalIgnoreCase)));

            if (!containsTarget)
                throw new InvalidOperationException("AI chưa nhúng lỗi hay sai vào bài luyện.");
        }

        var storyPassage = CleanSuggestedPassage(generated.ProblemKeyStoryPassage);
        if (storyPassage.Length < 60)
            throw new InvalidOperationException("AI thiếu problem_key_story_passage đủ dài.");

        if (targetTokens.Count > 0 && !targetTokens.Any(token => storyPassage.Contains(token, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Problem-key story chưa nhúng weak key/ngram thật.");

        generated.ProblemKeyStoryPassage = storyPassage;
        generated.SuggestedPassages = distinctPassages.Take(SuggestedPassageLimit).ToList();
    }

    private static List<string> MergeGeneratedList(IEnumerable<string>? generated, IEnumerable<string> fallback, int limit)
    {
        var merged = new List<string>();
        if (generated != null)
            merged.AddRange(generated);
        merged.AddRange(fallback);
        return DeduplicateLines(merged, limit);
    }

    private static List<AiPracticeMissionDto> MergeMissions(
        IEnumerable<AiPracticeMissionDto>? generated,
        IEnumerable<AiPracticeMissionDto> fallback,
        int limit)
    {
        var output = new List<AiPracticeMissionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mission in (generated ?? Enumerable.Empty<AiPracticeMissionDto>()).Concat(fallback))
        {
            var normalized = NormalizeMission(mission);
            if (!IsUsableMission(normalized))
                continue;

            var key = $"{normalized.Title}|{normalized.Passage}";
            if (!seen.Add(key))
                continue;

            output.Add(normalized);
            if (output.Count >= Math.Clamp(limit, 1, PracticeMissionLimit))
                break;
        }

        return output;
    }

    private static string BuildPrompt(
        string username,
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<MistakeMemorySample> mistakeSamples,
        int attempt)
    {
        var passageSample = string.IsNullOrWhiteSpace(request.PassageText)
            ? "(không có)"
            : BuildPassageSample(request.PassageText);
        var topMistypedCharacters = analysis.TopMistypedCharacters.Count == 0
            ? "(chưa xác định)"
            : string.Join(", ", analysis.TopMistypedCharacters);
        var topMistypedWords = analysis.TopMistypedWords.Count == 0
            ? "(chưa xác định)"
            : string.Join(", ", analysis.TopMistypedWords);
        var topMistypedNgrams = analysis.TopMistypedNgrams.Count == 0
            ? "(chưa xác định)"
            : string.Join(", ", analysis.TopMistypedNgrams);

        var completionPercent = analysis.CompletionRate * 100m;
        var observedSummary = BuildObservedMistakeSummary(mistakeSamples);

        return
            $"Người chơi: {username}\n" +
            $"RaceId: {request.RaceId}, Room: {request.RoomCode}\n" +
            $"WPM: {request.Wpm:F1}, Accuracy: {request.Accuracy:F1}%\n" +
            $"Position: {request.Position}/{request.TotalPlayers}, Completed: {request.IsCompleted}\n" +
            $"Correct: {request.CharsCorrect}, Wrong: {request.CharsWrong}, TimeMs: {request.TimeTakenMs}, Completion: {completionPercent:F1}%\n" +
            $"Recent races: count={request.RecentRaceCount}, completed={request.RecentCompletedCount}, avg_wpm={request.RecentAvgWpm:F1}, avg_acc={request.RecentAvgAccuracy:F1}%, trend_wpm={request.RecentWpmTrend:+0.0;-0.0;0}, trend_acc={request.RecentAccuracyTrend:+0.0;-0.0;0}%\n" +
            $"Mật độ lỗi ký tự: {analysis.MistakeDensity * 100m:F1}% | Ký tự hay sai: {topMistypedCharacters}\n" +
            $"Từ hay sai: {topMistypedWords}\n" +
            $"N-gram/bigram/trigram hay sai: {topMistypedNgrams}\n" +
            $"Ngôn ngữ: {NormalizeLanguage(request.Language)}\n" +
            $"Passage sample: {passageSample}\n" +
            $"Observed mistake samples in volatile memory: {mistakeSamples.Count}\n" +
            $"Observed mistakes from live typing updates (includes mistakes later corrected):\n{observedSummary}\n" +
            $"Phân tích heuristic hiện tại: focus={analysis.FocusArea}, tier={analysis.SkillTier}, estimated_next_wpm={analysis.EstimatedNextWpm:F1}\n" +
            "Return compact strict JSON with this exact shape and no markdown:\n" +
            "{\n" +
            "  \"coach_text\": \"1-2 concise sentences\",\n" +
            "  \"training_title\": \"short title for the next training pack\",\n" +
            "  \"recommended_game_mode\": \"classic|accuracy|no_backspace|sudden_death|ai_practice\",\n" +
            "  \"recommended_difficulty\": \"easy|medium|hard|nightmare\",\n" +
            "  \"recommended_target_rpm\": 45,\n" +
            "  \"ghost_target_wpm\": 52.5,\n" +
            "  \"ghost_target_accuracy\": 96.5,\n" +
            "  \"ghost_reward_badge\": \"short badge name for beating the next ghost target\",\n" +
            "  \"daily_challenge_title\": \"short title for a daily challenge\",\n" +
            "  \"daily_challenge_goal\": \"one measurable goal for the next session\",\n" +
            "  \"daily_challenge_reward\": \"short badge/reward text\",\n" +
            "  \"practice_words\": [\"4-6 missed words or character clusters\"],\n" +
            "  \"adaptive_micro_lessons\": [\"3 compact lessons with duration and target\"],\n" +
            "  \"mistake_heatmap\": [\"3-5 observed weak spots with count/pattern and correction cue\"],\n" +
            "  \"next_session_checklist\": [\"3-5 checklist items\"],\n" +
            "  \"ghost_race_plan\": [\"3-4 steps to race a personal ghost\"],\n" +
            "  \"finger_diagnostics\": [\"2-4 key/finger diagnostics from weak keys\"],\n" +
            "  \"progress_prediction\": [\"3 realistic predictions or risk notes\"],\n" +
            "  \"lesson_ladder\": [\"3-5 progressive lesson steps\"],\n" +
            "  \"attempt_replay_cues\": [\"3 replay-style cues\"],\n" +
            "  \"weak_key_drills\": [\"3-5 key, bigram, trigram, or word drills built from observed weak keys\"],\n" +
            "  \"ngram_drills\": [\"3-5 drills focused on observed weak n-grams like th, nh, ght, tion\"],\n" +
            "  \"spaced_repetition_plan\": [\"3-5 review steps for today, tomorrow, day 3, day 7, and retest\"],\n" +
            "  \"mastery_checkpoints\": [\"3-5 measurable pass/fail gates before increasing difficulty\"],\n" +
            "  \"problem_key_story_title\": \"short TypeAI-style story mission title\",\n" +
            "  \"problem_key_story_topic\": \"story topic chosen from the user's weak keys/ngrams\",\n" +
            "  \"problem_key_story_keys\": [\"2-4 actual weak keys, words, or n-grams used as story seeds\"],\n" +
            "  \"problem_key_story_passage\": \"fresh 90-180 character mini-story containing the weak keys/ngrams naturally\",\n" +
            "  \"mistake_fingerprint\": [\"3-5 fingerprint bullets: dominant miss pattern, trigger context, recovery risk, weakest n-gram, confidence\"],\n" +
            "  \"adaptive_race_strategy\": [\"3-5 race-phase instructions for opening, middle, recovery after error, finish, and retest\"],\n" +
            "  \"personalization_score\": 82,\n" +
            "  \"practice_missions\": [\n" +
            "    {\"title\":\"short playable mission name\",\"objective\":\"measurable pass condition\",\"game_mode\":\"classic|accuracy|no_backspace|sudden_death|ai_practice\",\"difficulty\":\"easy|medium|hard|nightmare\",\"duration_seconds\":180,\"target_wpm\":52.5,\"target_accuracy\":96.5,\"target_rpm\":55,\"passage\":\"fresh 70-150 character text containing the weak keys/ngrams\",\"reward_badge\":\"short badge name\",\"source_weakspot\":\"specific observed weak key, word, or n-gram\"}\n" +
            "  ],\n" +
            "  \"tips\": [\"3-4 concrete tips\"],\n" +
            "  \"action_plan\": [\"3-4 timed actions\"],\n" +
            "  \"personalized_drills\": [\"3-4 drills targeting observed mistakes\"],\n" +
            "  \"suggested_passages\": [\"4-6 brand-new typing passages, 70-150 characters each\"]\n" +
            "}\n" +
            $"Generation attempt: {attempt}. If this is greater than 1, the previous output failed validation, so make the practice passages clearer and complete.\n" +
            "Rules: be compact; problem_key_story_* is required and should work like Typing.com TypeAI: choose the top 2-4 observed problem keys/ngrams, pick a concrete topic, then write one fresh mini-story that includes those weak tokens naturally; practice_missions must contain 3-5 playable missions with unique fresh passages, realistic duration and pass criteria; suggested_passages must be new text, not copied from the original passage; include frequent mistake characters/words/n-grams naturally; mistake_fingerprint must quote the actual weak token/ngram labels and state why the pack is personalized; adaptive_race_strategy must be race-phase specific, not generic advice; heatmap, finger_diagnostics, weak_key_drills, ngram_drills, practice_missions and attempt_replay_cues must be based on observed mistakes, not generic advice; spaced_repetition_plan must schedule review from current mistake data; mastery_checkpoints must be measurable; personalization_score must be 0-100 and higher only when observed volatile mistakes are present; ghost target must be realistic; do not number items inside strings; match the requested language.";
    }

    private static string BuildObservedMistakeSummary(IReadOnlyList<MistakeMemorySample> mistakeSamples)
    {
        if (mistakeSamples.Count == 0)
            return "(không có sample tạm; chỉ dùng dữ liệu request hiện tại)";

        var charCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ngramCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        AddObservedCounts(charCounts, wordCounts, ngramCounts, mistakeSamples);

        var chars = charCounts.Count == 0
            ? "(chưa bắt được ký tự)"
            : string.Join(", ", charCounts.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key}:{x.Value}"));
        var words = wordCounts.Count == 0
            ? "(chưa bắt được từ)"
            : string.Join(", ", wordCounts.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key}:{x.Value}"));
        var ngrams = ngramCounts.Count == 0
            ? "(chưa bắt được n-gram)"
            : string.Join(", ", ngramCounts.OrderByDescending(x => x.Value).Take(12).Select(x => $"{x.Key}:{x.Value}"));

        var samples = mistakeSamples
            .Take(4)
            .Select(sample =>
                $"- race={sample.RaceId}, room={sample.RoomCode}, user={sample.Username}, chars=[{FormatPromptCounts(sample.ObservedMistakeCharacters)}], words=[{FormatPromptCounts(sample.ObservedMistakeWords)}], ngrams=[{FormatPromptCounts(sample.ObservedMistakeNgrams)}], passage=\"{BuildPassageSample(sample.PassageText)}\"")
            .ToList();

        return $"Top chars: {chars}\nTop words: {words}\nTop ngrams: {ngrams}\nSamples:\n{string.Join('\n', samples)}";
    }

    private static string FormatPromptCounts(IReadOnlyDictionary<string, int> counts)
    {
        if (counts.Count == 0)
            return "-";

        return string.Join(", ", counts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(8)
            .Select(x => $"{x.Key}:{x.Value}"));
    }

    private static GetAiCoachRequest Normalize(GetAiCoachRequest request)
    {
        var normalizedPassage = NormalizeTextForCoach(request.PassageText);
        if (normalizedPassage.Length > 420)
            normalizedPassage = normalizedPassage[..420];

        var totalPlayers = Math.Clamp(request.TotalPlayers, 1, 50);
        var position = Math.Clamp(request.Position, 1, totalPlayers);
        var maxChars = normalizedPassage.Length > 0 ? normalizedPassage.Length : 2000;
        var cappedCorrect = Math.Clamp(request.CharsCorrect, 0, maxChars);
        var cappedWrong = Math.Clamp(request.CharsWrong, 0, maxChars);

        if (!request.IsCompleted && cappedCorrect >= maxChars && maxChars > 0)
            cappedCorrect = maxChars - 1;

        return new GetAiCoachRequest
        {
            RaceId = Math.Max(0, request.RaceId),
            RoomCode = (request.RoomCode ?? string.Empty).Trim().ToUpperInvariant(),
            UserId = Math.Max(0, request.UserId),
            Username = NormalizeTextForCoach(request.Username),
            Position = position,
            TotalPlayers = totalPlayers,
            Wpm = Math.Clamp(request.Wpm, 0m, 9999.99m),
            Accuracy = Math.Clamp(request.Accuracy, 0m, 100m),
            CharsCorrect = cappedCorrect,
            CharsWrong = cappedWrong,
            TimeTakenMs = Math.Max(1, request.TimeTakenMs),
            IsCompleted = request.IsCompleted,
            Language = NormalizeLanguage(request.Language),
            PassageText = normalizedPassage,
            TypedText = NormalizeTypedTextForCoach(request.TypedText, MaxTypedTextForAnalysis),
            RecentRaceCount = Math.Clamp(request.RecentRaceCount, 0, 50),
            RecentCompletedCount = Math.Clamp(request.RecentCompletedCount, 0, 50),
            RecentAvgWpm = Math.Clamp(request.RecentAvgWpm, 0m, 500m),
            RecentAvgAccuracy = Math.Clamp(request.RecentAvgAccuracy, 0m, 100m),
            RecentWpmTrend = Math.Clamp(request.RecentWpmTrend, -150m, 150m),
            RecentAccuracyTrend = Math.Clamp(request.RecentAccuracyTrend, -100m, 100m),
        };
    }

    private static IReadOnlyList<MistakeMemorySample> NormalizeMistakeSamples(IReadOnlyList<MistakeMemorySample>? samples)
    {
        if (samples == null || samples.Count == 0)
            return Array.Empty<MistakeMemorySample>();

        return samples
            .Where(sample => sample.UserId > 0 && (!string.IsNullOrWhiteSpace(sample.TypedText) ||
                                                   sample.ObservedMistakeCharacters.Count > 0 ||
                                                   sample.ObservedMistakeWords.Count > 0 ||
                                                   sample.ObservedMistakeNgrams.Count > 0))
            .OrderByDescending(sample => sample.PlayedAt)
            .Take(12)
            .Select(sample => new MistakeMemorySample
            {
                RaceId = Math.Max(0, sample.RaceId),
                RoomCode = NormalizeTextForCoach(sample.RoomCode).ToUpperInvariant(),
                UserId = sample.UserId,
                Username = NormalizeTextForCoach(sample.Username),
                PassageText = NormalizeTypedTextForCoach(sample.PassageText, MaxTypedTextForAnalysis),
                TypedText = NormalizeTypedTextForCoach(sample.TypedText, MaxTypedTextForAnalysis),
                GameMode = NormalizeTextForCoach(sample.GameMode),
                PlayedAt = sample.PlayedAt,
                ObservedMistakeCharacters = NormalizeCountMap(sample.ObservedMistakeCharacters, 12),
                ObservedMistakeWords = NormalizeCountMap(sample.ObservedMistakeWords, 12),
                ObservedMistakeNgrams = NormalizeCountMap(sample.ObservedMistakeNgrams, 16),
            })
            .ToList();
    }

    private static Dictionary<string, int> NormalizeCountMap(IReadOnlyDictionary<string, int>? source, int limit)
    {
        if (source == null || source.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return source
            .Select(kv => new KeyValuePair<string, int>(NormalizeMistakeToken(kv.Key), Math.Clamp(kv.Value, 0, 999)))
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(Math.Clamp(limit, 1, 50))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static AiCoachResponse BuildFallback(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        string reason,
        int userId,
        int mistakeSampleCount)
    {
        return BuildResponse(
            success: true,
            coachText: BuildFallbackCoachText(request, analysis),
            request: request,
            analysis: analysis,
            provider: "heuristic",
            model: "local-rules-v3",
            isFallback: true,
            errorMessage: reason,
            raceId: request.RaceId,
            userId: request.UserId > 0 ? request.UserId : userId,
            generatedCoach: null,
            mistakeSampleCount: mistakeSampleCount);
    }

    private static AiCoachResponse BuildResponse(
        bool success,
        string coachText,
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        string provider,
        string model,
        bool isFallback,
        string? errorMessage,
        int raceId,
        int userId,
        AiGeneratedCoach? generatedCoach,
        int mistakeSampleCount)
    {
        var tips = MergeGeneratedList(generatedCoach?.Tips, analysis.Tips, 6);
        var actionPlan = MergeGeneratedList(generatedCoach?.ActionPlan, analysis.ActionPlan, 6);
        var drills = MergeGeneratedList(generatedCoach?.PersonalizedDrills, analysis.PersonalizedDrills, 6);
        var practiceWords = MergeGeneratedList(generatedCoach?.PracticeWords, BuildPracticeWords(analysis), 10);
        var mistakeHeatmap = MergeGeneratedList(generatedCoach?.MistakeHeatmap, BuildMistakeHeatmap(analysis), 8);
        var adaptiveMicroLessons = MergeGeneratedList(
            generatedCoach?.AdaptiveMicroLessons,
            BuildAdaptiveMicroLessons(request, analysis, practiceWords),
            6);
        var nextSessionChecklist = MergeGeneratedList(
            generatedCoach?.NextSessionChecklist,
            BuildNextSessionChecklist(request, analysis, practiceWords),
            6);
        var ghostTargetWpm = generatedCoach?.GhostTargetWpm > 0
            ? Math.Clamp(decimal.Round(generatedCoach.GhostTargetWpm, 1), 10m, 250m)
            : BuildGhostTargetWpm(request, analysis);
        var ghostTargetAccuracy = generatedCoach?.GhostTargetAccuracy > 0
            ? Math.Clamp(decimal.Round(generatedCoach.GhostTargetAccuracy, 1), 70m, 99.9m)
            : BuildGhostTargetAccuracy(request, analysis);
        var ghostRacePlan = MergeGeneratedList(
            generatedCoach?.GhostRacePlan,
            BuildGhostRacePlan(request, analysis, practiceWords, ghostTargetWpm, ghostTargetAccuracy),
            5);
        var fingerDiagnostics = MergeGeneratedList(
            generatedCoach?.FingerDiagnostics,
            BuildFingerDiagnostics(analysis),
            6);
        var progressPrediction = MergeGeneratedList(
            generatedCoach?.ProgressPrediction,
            BuildProgressPrediction(request, analysis, ghostTargetWpm, ghostTargetAccuracy),
            6);
        var problemKeyStoryKeys = MergeGeneratedList(
            generatedCoach?.ProblemKeyStoryKeys,
            BuildProblemKeyStoryKeys(analysis, practiceWords),
            6);
        var problemKeyStoryTitle = string.IsNullOrWhiteSpace(generatedCoach?.ProblemKeyStoryTitle)
            ? BuildProblemKeyStoryTitle(analysis)
            : NormalizeCoachOutput(generatedCoach.ProblemKeyStoryTitle);
        var problemKeyStoryTopic = string.IsNullOrWhiteSpace(generatedCoach?.ProblemKeyStoryTopic)
            ? BuildProblemKeyStoryTopic(analysis, problemKeyStoryKeys)
            : NormalizeCoachOutput(generatedCoach.ProblemKeyStoryTopic);
        var problemKeyStoryPassage = BuildProblemKeyStoryPassage(
            request,
            analysis,
            problemKeyStoryKeys,
            problemKeyStoryTopic,
            generatedCoach?.ProblemKeyStoryPassage);
        var suggestedPassages = MergeGeneratedList(
            new[] { problemKeyStoryPassage }.Concat(generatedCoach?.SuggestedPassages ?? Enumerable.Empty<string>()),
            analysis.SuggestedPassages,
            SuggestedPassageLimit);
        var recommendedPlan = BuildRecommendedTrainingPlan(request, analysis);
        var recommendedDifficulty = Constants.NormalizeAiPracticeDifficulty(
            string.IsNullOrWhiteSpace(generatedCoach?.RecommendedDifficulty)
                ? recommendedPlan.Difficulty
                : generatedCoach.RecommendedDifficulty);
        var recommendedMode = Constants.NormalizeGameMode(
            string.IsNullOrWhiteSpace(generatedCoach?.RecommendedGameMode)
                ? recommendedPlan.Mode
                : generatedCoach.RecommendedGameMode);
        var recommendedTargetRpm = generatedCoach?.RecommendedTargetRpm > 0
            ? Math.Clamp(generatedCoach.RecommendedTargetRpm, 15, 180)
            : recommendedPlan.TargetRpm;
        var trainingTitle = string.IsNullOrWhiteSpace(generatedCoach?.TrainingTitle)
            ? BuildTrainingTitle(analysis, recommendedDifficulty)
            : NormalizeCoachOutput(generatedCoach.TrainingTitle);
        var dailyChallenge = BuildDailyChallenge(request, analysis, recommendedDifficulty, practiceWords);
        var dailyTitle = string.IsNullOrWhiteSpace(generatedCoach?.DailyChallengeTitle)
            ? dailyChallenge.Title
            : NormalizeCoachOutput(generatedCoach.DailyChallengeTitle);
        var dailyGoal = string.IsNullOrWhiteSpace(generatedCoach?.DailyChallengeGoal)
            ? dailyChallenge.Goal
            : NormalizeCoachOutput(generatedCoach.DailyChallengeGoal);
        var dailyReward = string.IsNullOrWhiteSpace(generatedCoach?.DailyChallengeReward)
            ? dailyChallenge.Reward
            : NormalizeCoachOutput(generatedCoach.DailyChallengeReward);
        var ghostRewardBadge = string.IsNullOrWhiteSpace(generatedCoach?.GhostRewardBadge)
            ? BuildGhostRewardBadge(analysis.FocusArea)
            : NormalizeCoachOutput(generatedCoach.GhostRewardBadge);
        var lessonLadder = MergeGeneratedList(
            generatedCoach?.LessonLadder,
            BuildLessonLadder(request, analysis, practiceWords, recommendedMode, recommendedDifficulty),
            6);
        var attemptReplayCues = MergeGeneratedList(
            generatedCoach?.AttemptReplayCues,
            BuildAttemptReplayCues(request, analysis),
            6);
        var weakKeyDrills = MergeGeneratedList(
            generatedCoach?.WeakKeyDrills,
            BuildWeakKeyDrills(analysis, practiceWords),
            8);
        var ngramDrills = MergeGeneratedList(
            generatedCoach?.NgramDrills,
            BuildNgramDrills(analysis),
            8);
        var spacedRepetitionPlan = MergeGeneratedList(
            generatedCoach?.SpacedRepetitionPlan,
            BuildSpacedRepetitionPlan(request, analysis, practiceWords, recommendedMode, recommendedDifficulty),
            6);
        var masteryCheckpoints = MergeGeneratedList(
            generatedCoach?.MasteryCheckpoints,
            BuildMasteryCheckpoints(request, analysis, ghostTargetWpm, ghostTargetAccuracy, weakKeyDrills.Concat(ngramDrills).ToList()),
            6);
        var practiceMissions = MergeMissions(
            generatedCoach?.PracticeMissions,
            BuildPracticeMissions(
                request,
                analysis,
                suggestedPassages,
                recommendedMode,
                recommendedDifficulty,
                recommendedTargetRpm,
                ghostTargetWpm,
                ghostTargetAccuracy,
                dailyReward),
            PracticeMissionLimit);
        var mistakeFingerprint = MergeGeneratedList(
            generatedCoach?.MistakeFingerprint,
            BuildMistakeFingerprint(request, analysis, mistakeSampleCount, practiceWords),
            6);
        var adaptiveRaceStrategy = MergeGeneratedList(
            generatedCoach?.AdaptiveRaceStrategy,
            BuildAdaptiveRaceStrategy(request, analysis, ghostTargetWpm, ghostTargetAccuracy, mistakeFingerprint),
            6);
        var personalizationScore = generatedCoach?.PersonalizationScore > 0
            ? Math.Clamp(decimal.Round(generatedCoach.PersonalizationScore, 1), 0m, 100m)
            : BuildPersonalizationScore(request, analysis, mistakeSampleCount, practiceWords, suggestedPassages);
        var trainingPackSignature = BuildTrainingPackSignature(
            userId,
            raceId,
            provider,
            model,
            trainingTitle,
            practiceWords,
            suggestedPassages,
            problemKeyStoryKeys,
            problemKeyStoryPassage,
            mistakeFingerprint,
            adaptiveRaceStrategy);
        var passageNoveltyScore = BuildPassageNoveltyScore(request, suggestedPassages);
        var weakspotCoverageScore = BuildWeakspotCoverageScore(analysis, suggestedPassages, practiceWords);
        var aiConfidenceScore = BuildAiConfidenceScore(
            isFallback,
            mistakeSampleCount,
            passageNoveltyScore,
            weakspotCoverageScore,
            personalizationScore,
            suggestedPassages.Count);
        var generatedPassageAudit = BuildGeneratedPassageAudit(request, analysis, suggestedPassages);
        var aiEvidenceTrail = BuildAiEvidenceTrail(
            provider,
            model,
            isFallback,
            mistakeSampleCount,
            analysis,
            suggestedPassages.Count,
            passageNoveltyScore,
            weakspotCoverageScore,
            aiConfidenceScore,
            trainingPackSignature,
            problemKeyStoryKeys,
            problemKeyStoryPassage);

        return new AiCoachResponse
        {
            Success = success,
            CoachText = coachText,
            Tips = tips,
            ActionPlan = actionPlan,
            PersonalizedDrills = drills,
            SuggestedPassages = suggestedPassages,
            PracticeWords = practiceWords,
            AdaptiveMicroLessons = adaptiveMicroLessons,
            MistakeHeatmap = mistakeHeatmap,
            NextSessionChecklist = nextSessionChecklist,
            GhostRacePlan = ghostRacePlan,
            FingerDiagnostics = fingerDiagnostics,
            ProgressPrediction = progressPrediction,
            LessonLadder = lessonLadder,
            AttemptReplayCues = attemptReplayCues,
            WeakKeyDrills = weakKeyDrills,
            NgramDrills = ngramDrills,
            SpacedRepetitionPlan = spacedRepetitionPlan,
            MasteryCheckpoints = masteryCheckpoints,
            PracticeMissions = practiceMissions,
            ProblemKeyStoryTitle = problemKeyStoryTitle,
            ProblemKeyStoryTopic = problemKeyStoryTopic,
            ProblemKeyStoryPassage = problemKeyStoryPassage,
            ProblemKeyStoryKeys = problemKeyStoryKeys,
            MistakeFingerprint = mistakeFingerprint,
            AdaptiveRaceStrategy = adaptiveRaceStrategy,
            TopMistypedCharacters = analysis.TopMistypedCharacters,
            TopMistypedWords = analysis.TopMistypedWords,
            TopMistypedNgrams = analysis.TopMistypedNgrams,
            MistakeDensity = analysis.MistakeDensity,
            PersonalizationScore = personalizationScore,
            AiConfidenceScore = aiConfidenceScore,
            PassageNoveltyScore = passageNoveltyScore,
            WeakspotCoverageScore = weakspotCoverageScore,
            TrainingPackSignature = trainingPackSignature,
            AiEvidenceTrail = aiEvidenceTrail,
            GeneratedPassageAudit = generatedPassageAudit,
            FocusArea = analysis.FocusArea,
            SkillTier = analysis.SkillTier,
            EstimatedNextWpm = analysis.EstimatedNextWpm,
            RecentRaceCount = request.RecentRaceCount,
            RecentCompletedCount = request.RecentCompletedCount,
            RecentWpmTrend = request.RecentWpmTrend,
            RecentAccuracyTrend = request.RecentAccuracyTrend,
            MistakeSampleCount = mistakeSampleCount,
            GeneratedPassageCount = suggestedPassages.Count,
            TrainingTitle = trainingTitle,
            RecommendedGameMode = recommendedMode,
            RecommendedDifficulty = recommendedDifficulty,
            RecommendedTargetRpm = recommendedTargetRpm,
            DailyChallengeTitle = dailyTitle,
            DailyChallengeGoal = dailyGoal,
            DailyChallengeReward = dailyReward,
            GhostTargetWpm = ghostTargetWpm,
            GhostTargetAccuracy = ghostTargetAccuracy,
            GhostRewardBadge = ghostRewardBadge,
            Provider = provider,
            Model = model,
            IsFallback = isFallback,
            ErrorMessage = errorMessage,
            RaceId = raceId,
            UserId = userId,
        };
    }

    private static List<string> BuildPracticeWords(CoachAnalysis analysis)
    {
        var words = analysis.TopMistypedWords
            .Select(ParseMistakeEntry)
            .Where(x => x.Length >= 2)
            .Concat(analysis.TopMistypedNgrams
                .Select(ParseMistakeEntry)
                .Where(x => x.Length >= 2 && x.Length <= 6))
            .Concat(analysis.TopMistypedCharacters
                .Select(ParseMistakeEntry)
                .Where(x => x.Length > 0 && x.Length <= 4)
                .Select(x => string.Concat(Enumerable.Repeat(x, Math.Max(2, 6 / Math.Max(1, x.Length))))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (words.Count >= 4)
            return words;

        var focusWords = analysis.FocusArea switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" => new[] { "chính xác", "khoảng trắng", "dấu câu", "nhịp đều" },
            "Khôi phục tốc độ" or "Tốc độ nền" => new[] { "tăng tốc", "giữ nhịp", "bứt tốc", "ổn định" },
            "Giữ nhịp cuối bài" => new[] { "đoạn cuối", "không vội", "phục hồi", "về đích" },
            _ => new[] { "khởi động", "tập trung", "đều tay", "bình tĩnh" },
        };

        return words.Concat(focusWords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static List<string> BuildProblemKeyStoryKeys(CoachAnalysis analysis, IReadOnlyList<string> practiceWords)
    {
        var keys = analysis.TopMistypedNgrams
            .Concat(analysis.TopMistypedCharacters)
            .Concat(analysis.TopMistypedWords)
            .Concat(practiceWords)
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 24)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (keys.Count > 0)
            return keys;

        return analysis.FocusArea switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" => new List<string> { "accuracy", "space" },
            "Khôi phục tốc độ" or "Tốc độ nền" => new List<string> { "tempo", "speed" },
            "Giữ nhịp cuối bài" => new List<string> { "finish", "rhythm" },
            _ => new List<string> { "focus", "rhythm" },
        };
    }

    private static string BuildProblemKeyStoryTitle(CoachAnalysis analysis)
        => $"TypeAI problem-key story: {analysis.FocusArea}";

    private static string BuildProblemKeyStoryTopic(CoachAnalysis analysis, IReadOnlyList<string> storyKeys)
    {
        var keyLabel = storyKeys.Count == 0
            ? analysis.FocusArea.ToLowerInvariant()
            : string.Join(", ", storyKeys.Take(4));
        return $"Mini story luyện cụm yếu: {keyLabel}";
    }

    private static string BuildProblemKeyStoryPassage(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> storyKeys,
        string storyTopic,
        string? generatedPassage)
    {
        var candidate = CleanSuggestedPassage(generatedPassage ?? string.Empty);
        var keys = storyKeys
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 24)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var original = NormalizeTextForCoach(request.PassageText);

        if (candidate.Length >= 60 &&
            (keys.Count == 0 || keys.Any(key => candidate.Contains(key, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(original) || !original.Contains(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return candidate;
        }

        if (keys.Count == 0)
            keys.AddRange(BuildProblemKeyStoryKeys(analysis, Array.Empty<string>()).Take(3));

        var keyLine = string.Join(", ", keys.Take(4));
        var topic = string.IsNullOrWhiteSpace(storyTopic)
            ? BuildProblemKeyStoryTopic(analysis, keys)
            : storyTopic;

        var language = NormalizeLanguage(request.Language);
        var fallback = language == "vi"
            ? $"Trong phòng lab TypeAI, An chọn chủ đề {topic} và luyện cụm {keyLine}. Mỗi lần chuông sáng lên, An đọc trước một nhịp, gõ chậm hơn, rồi giữ tay thật đều đến cuối câu."
            : $"In the TypeAI lab, Mia picks the topic {topic} and practices {keyLine}. Each time the signal glows, she reads one beat ahead, slows down, and keeps a steady rhythm to the final word.";

        return CleanSuggestedPassage(fallback);
    }

    private static List<AiPracticeMissionDto> BuildPracticeMissions(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> suggestedPassages,
        string recommendedMode,
        string recommendedDifficulty,
        int recommendedTargetRpm,
        decimal ghostTargetWpm,
        decimal ghostTargetAccuracy,
        string rewardBadge)
    {
        var weakspots = analysis.TopMistypedNgrams
            .Concat(analysis.TopMistypedWords)
            .Concat(analysis.TopMistypedCharacters)
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(PracticeMissionLimit)
            .ToList();

        if (weakspots.Count == 0)
            weakspots.Add(analysis.FocusArea);

        var passages = suggestedPassages
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length >= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(PracticeMissionLimit)
            .ToList();

        while (passages.Count < 4)
        {
            passages.AddRange(BuildSuggestedPassages(
                NormalizeLanguage(request.Language),
                analysis.FocusArea,
                analysis.TopMistypedCharacters,
                analysis.TopMistypedWords,
                analysis.TopMistypedNgrams));
            passages = passages
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length >= 40)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(PracticeMissionLimit)
                .ToList();
        }

        var baseAccuracy = Math.Clamp(decimal.Round(Math.Max(request.Accuracy + 1m, ghostTargetAccuracy), 1), 85m, 99.9m);
        var baseWpm = Math.Clamp(decimal.Round(Math.Max(request.Wpm + 1.5m, ghostTargetWpm - 1m), 1), 10m, 250m);
        var mode = Constants.NormalizeGameMode(recommendedMode);
        var difficulty = Constants.NormalizeAiPracticeDifficulty(recommendedDifficulty);
        var targetRpm = recommendedTargetRpm > 0
            ? Math.Clamp(recommendedTargetRpm, 15, 180)
            : Constants.GetAiPracticeTargetRpm(difficulty);

        var missionSpecs = new[]
        {
            new { Title = "Problem-Key Story Mission", Seconds = 120, AccuracyBoost = 0.4m, WpmBoost = -4m, Mode = Constants.GameModeAccuracy },
            new { Title = "Weakspot Repair Mission", Seconds = 150, AccuracyBoost = 0.5m, WpmBoost = -2m, Mode = Constants.GameModeNoBackspace },
            new { Title = "Ghost Chase Mission", Seconds = 180, AccuracyBoost = 0m, WpmBoost = 0m, Mode = mode },
            new { Title = "Certification Prep Mission", Seconds = 300, AccuracyBoost = 0.2m, WpmBoost = 1.5m, Mode = Constants.GameModeClassic },
            new { Title = "Nightmare Boss Mission", Seconds = 210, AccuracyBoost = 0.8m, WpmBoost = 3m, Mode = Constants.GameModeSuddenDeath },
            new { Title = "Daily Retest Mission", Seconds = 120, AccuracyBoost = 0.4m, WpmBoost = 2m, Mode = mode },
        };

        var missions = new List<AiPracticeMissionDto>();
        for (var i = 0; i < Math.Min(missionSpecs.Length, passages.Count); i++)
        {
            var spec = missionSpecs[i];
            var weakspot = weakspots[i % weakspots.Count];
            var targetAccuracy = Math.Clamp(decimal.Round(baseAccuracy + spec.AccuracyBoost, 1), 85m, 99.9m);
            var targetWpm = Math.Clamp(decimal.Round(baseWpm + spec.WpmBoost, 1), 10m, 250m);
            var missionDifficulty = i == 4 ? Constants.AiPracticeNightmare : difficulty;

            missions.Add(new AiPracticeMissionDto
            {
                Title = spec.Title,
                Objective = BuildMissionObjective(spec.Title, weakspot, targetWpm, targetAccuracy),
                GameMode = spec.Mode,
                Difficulty = missionDifficulty,
                DurationSeconds = spec.Seconds,
                TargetWpm = targetWpm,
                TargetAccuracy = targetAccuracy,
                TargetRpm = targetRpm,
                Passage = passages[i],
                RewardBadge = string.IsNullOrWhiteSpace(rewardBadge)
                    ? BuildChallengeBadge(analysis.FocusArea)
                    : rewardBadge,
                SourceWeakspot = weakspot,
            });
        }

        return missions;
    }

    private static string BuildMissionObjective(
        string title,
        string weakspot,
        decimal targetWpm,
        decimal targetAccuracy)
    {
        var weakspotText = string.IsNullOrWhiteSpace(weakspot) ? "weakspot chính" : weakspot;
        if (title.Contains("Certification", StringComparison.OrdinalIgnoreCase))
            return $"Mô phỏng bài kiểm tra 5 phút: WPM >= {targetWpm:F1}, accuracy >= {targetAccuracy:F1}%, giảm lỗi ở '{weakspotText}'.";
        if (title.Contains("Boss", StringComparison.OrdinalIgnoreCase))
            return $"Boss round: không sai dây chuyền ở '{weakspotText}', giữ WPM >= {targetWpm:F1} và accuracy >= {targetAccuracy:F1}%.";
        if (title.Contains("Story", StringComparison.OrdinalIgnoreCase))
            return $"TypeAI story mission: gõ sạch mini-story chứa '{weakspotText}', accuracy >= {targetAccuracy:F1}% trước khi tăng tốc.";
        if (title.Contains("Ghost", StringComparison.OrdinalIgnoreCase))
            return $"Đuổi ghost cá nhân: bám target {targetWpm:F1} WPM, accuracy >= {targetAccuracy:F1}%, ưu tiên '{weakspotText}'.";
        return $"Hoàn thành mission với accuracy >= {targetAccuracy:F1}% và xử lý sạch weakspot '{weakspotText}'.";
    }

    private static List<string> BuildMistakeFingerprint(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        int mistakeSampleCount,
        IReadOnlyList<string> practiceWords)
    {
        var primary = PickPrimaryWeakspot(analysis, practiceWords);
        var evidence = mistakeSampleCount > 0
            ? $"{mistakeSampleCount} sample lỗi volatile đã bắt ngay lúc gõ sai"
            : "payload trận hiện tại vì chưa có sample volatile";
        var weakspotCount = analysis.TopMistypedCharacters.Count +
                            analysis.TopMistypedWords.Count +
                            analysis.TopMistypedNgrams.Count;
        var density = analysis.MistakeDensity * 100m;
        var recoveryRisk = request.CharsWrong > 0 || density >= 4m
            ? "cao, vì lỗi có thể kéo thành chuỗi nếu bù tốc ngay sau ký tự sai"
            : "vừa, vì dữ liệu sai ít nhưng vẫn cần kiểm tra n-gram yếu";
        var trendSignal = request.RecentRaceCount >= 4
            ? $"trend WPM {request.RecentWpmTrend:+0.0;-0.0;0}, accuracy {request.RecentAccuracyTrend:+0.0;-0.0;0}%"
            : "chưa đủ 4 race để khóa trend dài hơn";

        return DeduplicateLines(new[]
        {
            $"Typing DNA: weakspot chính '{primary}' từ {evidence}.",
            $"Mẫu lỗi: {weakspotCount} cụm/keys được gom thành heatmap; mật độ lỗi khoảng {density:F1}%.",
            $"Ngữ cảnh kích hoạt: focus '{analysis.FocusArea}', tier {analysis.SkillTier}, {trendSignal}.",
            $"Rủi ro hồi phục sau lỗi: {recoveryRisk}.",
            $"Seed bài luyện cá nhân: {string.Join(", ", practiceWords.Take(5))}.",
        }, 6);
    }

    private static List<string> BuildAdaptiveRaceStrategy(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        decimal ghostTargetWpm,
        decimal ghostTargetAccuracy,
        IReadOnlyList<string> mistakeFingerprint)
    {
        var primary = PickPrimaryWeakspot(analysis, mistakeFingerprint);
        var openingCap = Math.Clamp(decimal.Round(Math.Max(10m, request.Wpm * 0.92m), 1), 10m, 240m);
        var cruiseTarget = Math.Clamp(decimal.Round(Math.Max(openingCap + 1m, Math.Min(ghostTargetWpm, request.Wpm + 3m)), 1), 10m, 250m);

        return DeduplicateLines(new[]
        {
            $"Opening 0-20s: khóa nhịp quanh {openingCap:F1} WPM, không sprint khi gặp '{primary}'.",
            $"Middle 20-70%: nếu '{primary}' xuất hiện, giảm 5% tốc trong 2 từ rồi quay lại {cruiseTarget:F1} WPM.",
            "Recovery sau lỗi: không bấm bù tốc; gõ 2 từ kế tiếp chậm hơn, giữ mắt ở từ tiếp theo.",
            $"Finish 70-100%: chỉ đẩy tốc nếu accuracy đang >= {ghostTargetAccuracy:F1}%, ưu tiên về đích sạch.",
            $"Retest: sau mission, chạy AI Coach lại; fingerprint phải đổi hoặc mật độ lỗi '{analysis.FocusArea}' phải giảm.",
        }, 6);
    }

    private static decimal BuildPersonalizationScore(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        int mistakeSampleCount,
        IReadOnlyList<string> practiceWords,
        IReadOnlyList<string> suggestedPassages)
    {
        var weakspotCount = analysis.TopMistypedCharacters.Count +
                            analysis.TopMistypedWords.Count +
                            analysis.TopMistypedNgrams.Count;
        var score = 42m;
        score += Math.Min(28m, mistakeSampleCount * 12m);
        score += Math.Min(18m, weakspotCount * 2.5m);
        score += practiceWords.Count >= 4 ? 5m : practiceWords.Count;
        score += suggestedPassages.Count >= 10 ? 7m : Math.Min(6m, suggestedPassages.Count * 0.5m);

        if (string.IsNullOrWhiteSpace(request.TypedText) && mistakeSampleCount == 0)
            score -= 8m;
        if (analysis.MistakeDensity > 0m)
            score += Math.Min(6m, analysis.MistakeDensity * 100m);

        return Math.Clamp(decimal.Round(score, 1), 0m, 100m);
    }

    private static string BuildTrainingPackSignature(
        int userId,
        int raceId,
        string provider,
        string model,
        string trainingTitle,
        IReadOnlyList<string> practiceWords,
        IReadOnlyList<string> suggestedPassages,
        IReadOnlyList<string> problemKeyStoryKeys,
        string problemKeyStoryPassage,
        IReadOnlyList<string> mistakeFingerprint,
        IReadOnlyList<string> adaptiveRaceStrategy)
    {
        var body = string.Join("|", new[]
        {
            userId.ToString(),
            raceId.ToString(),
            provider,
            model,
            trainingTitle,
            string.Join(";", practiceWords.Take(10)),
            string.Join(";", suggestedPassages.Take(12)),
            string.Join(";", problemKeyStoryKeys.Take(6)),
            problemKeyStoryPassage,
            string.Join(";", mistakeFingerprint.Take(6)),
            string.Join(";", adaptiveRaceStrategy.Take(6)),
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))[..16];
    }

    private static decimal BuildPassageNoveltyScore(GetAiCoachRequest request, IReadOnlyList<string> suggestedPassages)
    {
        var passages = suggestedPassages
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeTextForCoach)
            .Where(x => x.Length >= 40)
            .ToList();

        if (passages.Count == 0)
            return 0m;

        var distinctCount = passages.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var distinctRatio = (decimal)distinctCount / passages.Count;
        var original = NormalizeTextForCoach(request.PassageText);
        var maxOverlap = string.IsNullOrWhiteSpace(original)
            ? 0m
            : passages.Max(passage => EstimateTokenOverlap(original, passage));
        var avgLength = passages.Average(x => x.Length);
        var lengthScore = avgLength is >= 70 and <= 170
            ? 15m
            : avgLength >= 40 && avgLength <= 240 ? 10m : 4m;
        var countScore = Math.Min(25m, (decimal)passages.Count / SuggestedPassageLimit * 25m);

        var score = 10m + countScore + distinctRatio * 20m + (1m - maxOverlap) * 30m + lengthScore;
        return Math.Clamp(decimal.Round(score, 1), 0m, 100m);
    }

    private static decimal BuildWeakspotCoverageScore(
        CoachAnalysis analysis,
        IReadOnlyList<string> suggestedPassages,
        IReadOnlyList<string> practiceWords)
    {
        var targetTokens = BuildTargetTokens(analysis);
        if (targetTokens.Count == 0)
        {
            var fallbackScore = 45m + Math.Min(25m, practiceWords.Count * 4m) + Math.Min(30m, suggestedPassages.Count * 2m);
            return Math.Clamp(decimal.Round(fallbackScore, 1), 0m, 100m);
        }

        var passageBlob = string.Join(" ", suggestedPassages);
        var practiceBlob = string.Join(" ", practiceWords);
        var covered = targetTokens.Count(token =>
            ContainsToken(passageBlob, token) || ContainsToken(practiceBlob, token));
        var coverageRatio = (decimal)covered / targetTokens.Count;
        var passageCountScore = Math.Min(20m, suggestedPassages.Count * 1.7m);
        var score = coverageRatio * 80m + passageCountScore;

        return Math.Clamp(decimal.Round(score, 1), 0m, 100m);
    }

    private static decimal BuildAiConfidenceScore(
        bool isFallback,
        int mistakeSampleCount,
        decimal passageNoveltyScore,
        decimal weakspotCoverageScore,
        decimal personalizationScore,
        int generatedPassageCount)
    {
        var score = passageNoveltyScore * 0.28m +
                    weakspotCoverageScore * 0.25m +
                    personalizationScore * 0.25m;
        score += isFallback ? -12m : 14m;
        score += Math.Min(10m, mistakeSampleCount * 5m);
        score += Math.Min(8m, generatedPassageCount * 0.8m);

        return Math.Clamp(decimal.Round(score, 1), 0m, 100m);
    }

    private static List<string> BuildAiEvidenceTrail(
        string provider,
        string model,
        bool isFallback,
        int mistakeSampleCount,
        CoachAnalysis analysis,
        int generatedPassageCount,
        decimal passageNoveltyScore,
        decimal weakspotCoverageScore,
        decimal aiConfidenceScore,
        string trainingPackSignature,
        IReadOnlyList<string> problemKeyStoryKeys,
        string problemKeyStoryPassage)
    {
        var chars = CompactEvidenceList(analysis.TopMistypedCharacters, 4);
        var words = CompactEvidenceList(analysis.TopMistypedWords, 4);
        var ngrams = CompactEvidenceList(analysis.TopMistypedNgrams, 5);
        var storyKeys = CompactEvidenceList(problemKeyStoryKeys, 4);
        var route = isFallback ? "fallback bank" : "OpenClaude live generation";

        return DeduplicateLines(new[]
        {
            $"AI route: {provider}/{model} qua {route}.",
            $"Mistake seed: {mistakeSampleCount} sample volatile; chars=[{chars}], words=[{words}], ngrams=[{ngrams}].",
            $"TypeAI problem-key story: keys=[{storyKeys}], length={problemKeyStoryPassage.Length}.",
            $"Generation audit: {generatedPassageCount} bài, novelty {passageNoveltyScore:F1}/100, weakspot coverage {weakspotCoverageScore:F1}/100.",
            $"Confidence: {aiConfidenceScore:F1}/100; training pack signature {trainingPackSignature}.",
        }, 6);
    }

    private static List<string> BuildGeneratedPassageAudit(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> suggestedPassages)
    {
        var targetTokens = BuildTargetTokens(analysis);
        var original = NormalizeTextForCoach(request.PassageText);
        var audits = new List<string>();
        var index = 1;

        foreach (var passage in suggestedPassages
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Take(8))
        {
            var weakspotHits = targetTokens
                .Where(token => ContainsToken(passage, token))
                .Take(5)
                .ToList();
            var overlap = string.IsNullOrWhiteSpace(original)
                ? 0m
                : EstimateTokenOverlap(original, passage) * 100m;
            var hitLabel = weakspotHits.Count == 0
                ? "-"
                : string.Join(", ", weakspotHits);

            audits.Add(
                $"Passage {index}: {passage.Length} ký tự, weakspot hits {weakspotHits.Count}/{Math.Max(1, targetTokens.Count)} [{hitLabel}], overlap đề gốc {overlap:F1}%.");
            index++;
        }

        if (audits.Count == 0)
            audits.Add("Chưa có bài luyện để audit; server sẽ dùng fallback bank nếu AI provider lỗi.");

        return audits;
    }

    private static List<string> BuildTargetTokens(CoachAnalysis analysis)
    {
        return analysis.TopMistypedNgrams
            .Concat(analysis.TopMistypedWords)
            .Concat(analysis.TopMistypedCharacters)
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 24)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string CompactEvidenceList(IReadOnlyList<string> values, int limit)
    {
        var items = values
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 10))
            .ToList();

        return items.Count == 0 ? "-" : string.Join(", ", items);
    }

    private static bool ContainsToken(string text, string token)
        => !string.IsNullOrWhiteSpace(text) &&
           !string.IsNullOrWhiteSpace(token) &&
           text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static decimal EstimateTokenOverlap(string original, string candidate)
    {
        var originalTokens = TokenizeForOverlap(original);
        var candidateTokens = TokenizeForOverlap(candidate);
        if (originalTokens.Count == 0 || candidateTokens.Count == 0)
            return 0m;

        var intersection = originalTokens.Count(token => candidateTokens.Contains(token));
        var union = originalTokens.Count + candidateTokens.Count - intersection;
        if (union <= 0)
            return 0m;

        return Math.Clamp((decimal)intersection / union, 0m, 1m);
    }

    private static HashSet<string> TokenizeForOverlap(string text)
    {
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TokenRegex.Matches(NormalizeTextForCoach(text).ToLowerInvariant()))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2)
                output.Add(token);
        }

        return output;
    }

    private static string PickPrimaryWeakspot(CoachAnalysis analysis, IReadOnlyList<string>? fallback = null)
    {
        var token = analysis.TopMistypedNgrams
            .Concat(analysis.TopMistypedWords)
            .Concat(analysis.TopMistypedCharacters)
            .Select(ParseMistakeEntry)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(token))
            return token;

        token = fallback?
            .Select(ParseMistakeEntry)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(token)
            ? analysis.FocusArea.ToLowerInvariant()
            : token;
    }

    private static DailyChallenge BuildDailyChallenge(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        string difficulty,
        IReadOnlyList<string> practiceWords)
    {
        var difficultyLabel = difficulty switch
        {
            Constants.AiPracticeMedium => "vừa",
            Constants.AiPracticeHard => "khó",
            Constants.AiPracticeNightmare => "ác mộng",
            _ => "dễ",
        };

        var targetAccuracy = analysis.FocusArea is "Độ chính xác" or "Ổn định độ chính xác"
            ? Math.Min(99m, Math.Max(92m, decimal.Round(request.Accuracy + 2m, 1)))
            : Math.Min(98m, Math.Max(90m, decimal.Round(request.Accuracy + 1m, 1)));
        var targetWpm = Math.Max(15m, decimal.Round(analysis.EstimatedNextWpm, 1));
        var focusToken = practiceWords.Count > 0 ? practiceWords[0] : analysis.FocusArea.ToLowerInvariant();

        var title = $"Daily challenge: {analysis.FocusArea}";
        var goal = $"Hoàn thành 3 lượt cấp {difficultyLabel}, accuracy >= {targetAccuracy:F1}% và WPM >= {targetWpm:F1}, ưu tiên cụm \"{focusToken}\".";
        var reward = $"Badge đề xuất: {BuildChallengeBadge(analysis.FocusArea)}";
        return new DailyChallenge(title, goal, reward);
    }

    private static List<string> BuildMistakeHeatmap(CoachAnalysis analysis)
    {
        var heatmap = new List<string>();

        heatmap.AddRange(analysis.TopMistypedCharacters
            .Take(4)
            .Select(item =>
            {
                var token = ParseMistakeEntry(item);
                return string.IsNullOrWhiteSpace(token)
                    ? $"Ký tự đặc biệt: {item}; cue sửa: chậm lại một nhịp trước khoảng trắng/dấu câu."
                    : $"Ký tự '{token}': {item}; cue sửa: đọc trước ký tự kế tiếp rồi mới nhấn.";
            }));

        heatmap.AddRange(analysis.TopMistypedWords
            .Take(4)
            .Select(item =>
            {
                var token = ParseMistakeEntry(item);
                return $"Từ/cụm '{token}': {item}; cue sửa: gõ chậm 2 lần, lần thứ 3 tăng nhẹ tốc.";
            }));

        heatmap.AddRange(analysis.TopMistypedNgrams
            .Take(4)
            .Select(item =>
            {
                var token = ParseMistakeEntry(item);
                return $"N-gram '{token}': {item}; cue sửa: đọc cả cụm trước khi nhấn phím đầu tiên.";
            }));

        if (heatmap.Count == 0)
        {
            heatmap.Add("Chưa đủ lỗi cụ thể để tạo heatmap; AI sẽ ưu tiên nhịp đều, khoảng trắng và dấu câu.");
        }

        return DeduplicateLines(heatmap, 8);
    }

    private static List<string> BuildAdaptiveMicroLessons(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> practiceWords)
    {
        var focusWord = practiceWords.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? analysis.FocusArea.ToLowerInvariant();
        var targetAccuracy = Math.Clamp(decimal.Round(Math.Max(request.Accuracy + 1m, 92m), 1), 70m, 99.5m);
        var targetWpm = Math.Clamp(decimal.Round(analysis.EstimatedNextWpm, 1), 10m, 220m);

        var lessons = new List<string>
        {
            $"90s warm-up: gõ '{focusWord}' theo nhịp chậm, mục tiêu accuracy >= {targetAccuracy:F1}%.",
            $"2 phút weakspot loop: dùng 3 mục đầu trong heatmap, sai vẫn đi tiếp nhưng ghi nhớ vị trí lệch.",
            $"3 phút speed ladder: bắt đầu thấp hơn {Math.Max(10, (int)request.Wpm - 5)} WPM, tăng 3 WPM mỗi vòng nếu accuracy ổn.",
        };

        lessons.Add(analysis.FocusArea switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" =>
                "60s stop-on-word drill: khi sai một từ, gõ lại nguyên từ đó 2 lần rồi tiếp tục.",
            "Khôi phục tốc độ" or "Tốc độ nền" =>
                $"75s burst drill: 20 giây nhanh, 20 giây giữ nhịp; dừng nếu accuracy dưới {targetAccuracy:F1}%.",
            "Giữ nhịp cuối bài" =>
                "90s endgame drill: chỉ luyện 30% cuối passage, giữ cadence không đổi ở 5 từ cuối.",
            _ =>
                $"2 phút race sim: hoàn thành một lượt full, mục tiêu WPM >= {targetWpm:F1} và không bỏ cuộc giữa bài.",
        });

        lessons.Add("30s review: đọc lại heatmap, chọn 1 lỗi duy nhất để thắng trong race tiếp theo.");
        return DeduplicateLines(lessons, 6);
    }

    private static List<string> BuildNextSessionChecklist(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> practiceWords)
    {
        var firstWord = practiceWords.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "weakspot";
        var targetWpm = Math.Clamp(decimal.Round(analysis.EstimatedNextWpm, 1), 10m, 220m);
        var targetAccuracy = Math.Clamp(decimal.Round(Math.Max(request.Accuracy + 1m, 90m), 1), 70m, 99.5m);

        return DeduplicateLines(new[]
        {
            $"Chọn mode {analysis.FocusArea} trước, chưa đổi mode khi chưa xong 3 lượt.",
            $"Lượt 1: accuracy >= {targetAccuracy:F1}%, WPM không cần vượt mục tiêu.",
            $"Lượt 2: thêm cụm '{firstWord}' vào đoạn luyện và giữ lỗi lặp lại <= 2 lần.",
            $"Lượt 3: WPM mục tiêu >= {targetWpm:F1}, không để sai dây chuyền sau lỗi đầu.",
            "Sau phiên: bấm AI Coach lại để so sánh heatmap mới với heatmap cũ.",
        }, 6);
    }

    private static decimal BuildGhostTargetWpm(GetAiCoachRequest request, CoachAnalysis analysis)
    {
        var baseTarget = Math.Max(request.Wpm + 2m, analysis.EstimatedNextWpm);
        if (request.RecentRaceCount >= 4 && request.RecentWpmTrend < 0)
            baseTarget = Math.Max(baseTarget, request.RecentAvgWpm + 1.5m);

        if (analysis.FocusArea is "Độ chính xác" or "Ổn định độ chính xác")
            baseTarget = Math.Min(baseTarget, Math.Max(15m, request.Wpm + 3m));

        return Math.Clamp(decimal.Round(baseTarget, 1), 10m, 250m);
    }

    private static decimal BuildGhostTargetAccuracy(GetAiCoachRequest request, CoachAnalysis analysis)
    {
        var boost = analysis.FocusArea is "Độ chính xác" or "Ổn định độ chính xác" ? 2.0m : 1.0m;
        var target = Math.Max(90m, request.Accuracy + boost);
        if (request.Accuracy >= 97m)
            target = Math.Min(99.5m, request.Accuracy + 0.4m);

        return Math.Clamp(decimal.Round(target, 1), 70m, 99.9m);
    }

    private static List<string> BuildGhostRacePlan(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> practiceWords,
        decimal ghostTargetWpm,
        decimal ghostTargetAccuracy)
    {
        var focusToken = practiceWords.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? analysis.FocusArea.ToLowerInvariant();
        return DeduplicateLines(new[]
        {
            $"Set ghost: WPM {ghostTargetWpm:F1}, accuracy {ghostTargetAccuracy:F1}%, focus '{focusToken}'.",
            "Lượt 1 đua dưới ghost 5% để giữ nhịp và học đoạn khó.",
            "Lượt 2 bám sát ghost đến 70% bài, không bù tốc sau lỗi đầu.",
            $"Lượt 3 chỉ thắng ghost khi accuracy >= {ghostTargetAccuracy:F1}%, nếu thấp hơn thì lặp lại micro-lesson.",
            request.RecentRaceCount >= 4
                ? $"So với trend gần đây: WPM {request.RecentWpmTrend:+0.0;-0.0;0}, accuracy {request.RecentAccuracyTrend:+0.0;-0.0;0}%; mục tiêu là đảo trend xấu trước khi tăng tốc."
                : "Sau 3 lượt, chạy AI Coach lại để tạo ghost mới từ dữ liệu vừa gõ.",
        }, 5);
    }

    private static List<string> BuildFingerDiagnostics(CoachAnalysis analysis)
    {
        var diagnostics = new List<string>();
        foreach (var item in analysis.TopMistypedCharacters.Take(6))
        {
            var token = ParseMistakeEntry(item);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var cue = BuildFingerCue(token);
            diagnostics.Add($"Key '{token}': {item}; {cue}");
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add("Chưa đủ weak-key cụ thể; ưu tiên home row, cổ tay trung lập và khoảng trắng sạch.");
            diagnostics.Add("Space/punctuation check: giữ ngón cái nhẹ, chậm lại trước dấu câu để tránh lệch cụm.");
        }

        return DeduplicateLines(diagnostics, 6);
    }

    private static string BuildFingerCue(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return "finger cue: đọc trước ký tự kế tiếp rồi mới nhấn.";

        var first = normalized[0];
        var finger = first switch
        {
            'q' or 'a' or 'z' => "ngón út trái",
            'w' or 's' or 'x' => "ngón áp út trái",
            'e' or 'd' or 'c' => "ngón giữa trái",
            'r' or 't' or 'f' or 'g' or 'v' or 'b' => "ngón trỏ trái",
            'y' or 'u' or 'h' or 'j' or 'n' or 'm' => "ngón trỏ phải",
            'i' or 'k' => "ngón giữa phải",
            'o' or 'l' => "ngón áp út phải",
            'p' => "ngón út phải",
            _ when char.IsDigit(first) => "hàng số",
            _ => "ký tự đặc biệt/dấu tiếng Việt",
        };

        return $"finger cue: dùng {finger}, giữ tay ở home row và giảm nhịp ngay trước phím này.";
    }

    private static List<string> BuildProgressPrediction(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        decimal ghostTargetWpm,
        decimal ghostTargetAccuracy)
    {
        var currentWpm = Math.Max(0m, request.Wpm);
        var nextRaceLow = Math.Clamp(decimal.Round(Math.Max(currentWpm, analysis.EstimatedNextWpm - 1.5m), 1), 0m, 250m);
        var nextRaceHigh = Math.Clamp(decimal.Round(Math.Max(nextRaceLow + 0.5m, analysis.EstimatedNextWpm + 1.5m), 1), 0m, 260m);
        var threeRaceTarget = Math.Clamp(decimal.Round(ghostTargetWpm + 2m, 1), 10m, 260m);
        var sevenDayTarget = Math.Clamp(decimal.Round(ghostTargetWpm + 6m, 1), 12m, 280m);
        var accuracyTarget = Math.Clamp(decimal.Round(Math.Max(ghostTargetAccuracy, request.Accuracy + 1m), 1), 70m, 99.9m);
        var risk = analysis.FocusArea is "Độ chính xác" or "Ổn định độ chính xác"
            ? "rủi ro chính là tăng tốc quá sớm làm accuracy tụt."
            : analysis.FocusArea == "Giữ nhịp cuối bài"
                ? "rủi ro chính là hụt nhịp ở 30% cuối bài."
                : "rủi ro chính là nhịp không đều sau lỗi đầu.";

        return DeduplicateLines(new[]
        {
            $"Next race forecast: {nextRaceLow:F1}-{nextRaceHigh:F1} WPM nếu giữ accuracy >= {accuracyTarget:F1}%.",
            $"3-race target: bám ghost {ghostTargetWpm:F1} WPM rồi nâng lên {threeRaceTarget:F1} WPM khi heatmap giảm.",
            $"7-day goal: đạt {sevenDayTarget:F1} WPM hoặc {accuracyTarget:F1}% accuracy trong cùng mode.",
            $"Risk alert: {risk}",
            request.RecentRaceCount >= 4
                ? $"Trend check: WPM {request.RecentWpmTrend:+0.0;-0.0;0}, accuracy {request.RecentAccuracyTrend:+0.0;-0.0;0}%; ưu tiên đảo trend xấu trước khi đổi bài."
                : "Trend check: cần thêm ít nhất 4 race để dự báo dài hạn chắc hơn.",
        }, 6);
    }

    private static List<string> BuildLessonLadder(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> practiceWords,
        string recommendedMode,
        string recommendedDifficulty)
    {
        var focusToken = practiceWords.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? analysis.FocusArea.ToLowerInvariant();
        var modeLabel = recommendedMode switch
        {
            Constants.GameModeAccuracy => "Accuracy Challenge",
            Constants.GameModeNoBackspace => "No Backspace",
            Constants.GameModeSuddenDeath => "Sudden Death",
            Constants.GameModeAiPractice => "AI Practice",
            _ => "Classic",
        };
        var difficultyLabel = recommendedDifficulty switch
        {
            Constants.AiPracticeMedium => "vừa",
            Constants.AiPracticeHard => "khó",
            Constants.AiPracticeNightmare => "ác mộng",
            _ => "dễ",
        };

        return DeduplicateLines(new[]
        {
            $"Level 1 - Warm-up: 90s gõ chậm với focus '{focusToken}', accuracy >= {Math.Max(90m, request.Accuracy):F1}%.",
            "Level 2 - Weak-key loop: lặp 3 mục đầu trong finger diagnostics, chỉ tăng tốc khi không còn sai dây chuyền.",
            $"Level 3 - Mode drill: chơi {modeLabel} cấp {difficultyLabel}, giữ cùng cadence trong toàn bài.",
            "Level 4 - Boss round: chọn đoạn AI dài nhất, sai quá 3 lần thì quay lại Level 2.",
            "Level 5 - Ghost check: chỉ qua bài khi thắng ghost mà accuracy không thấp hơn mục tiêu.",
        }, 6);
    }

    private static List<string> BuildAttemptReplayCues(GetAiCoachRequest request, CoachAnalysis analysis)
    {
        var completionRate = analysis.CompletionRate * 100m;
        var seconds = Math.Max(1, request.TimeTakenMs / 1000);
        var typedTotal = Math.Max(1, request.CharsCorrect + request.CharsWrong);
        var errorWindow = request.CharsWrong > 0
            ? $"Có {request.CharsWrong} lỗi trong {typedTotal} ký tự typed; replay nên soi cụm trước lỗi, không chỉ ký tự sai."
            : "Không có lỗi rõ từ payload; replay nên soi nhịp mở bài và đoạn giảm tốc.";

        return DeduplicateLines(new[]
        {
            $"Opening cue: 0-20s đầu cần giữ nhịp dưới ngưỡng ghost, tránh burst khi chưa ổn định.",
            $"Middle cue: ở khoảng 50% bài, kiểm tra focus '{analysis.FocusArea}' và không đổi cadence đột ngột.",
            $"Finish cue: completion {completionRate:F1}% trong {seconds}s; đoạn cuối cần nhịp đều hơn là bù tốc.",
            $"Error cue: {errorWindow}",
            "Recovery cue: sau lỗi đầu, gõ 2 từ kế tiếp chậm hơn 5% để tránh lỗi dây chuyền.",
        }, 6);
    }

    private static List<string> BuildWeakKeyDrills(CoachAnalysis analysis, IReadOnlyList<string> practiceWords)
    {
        var weakKeys = analysis.TopMistypedCharacters
            .Select(ParseMistakeEntry)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var weakWords = analysis.TopMistypedWords
            .Select(ParseMistakeEntry)
            .Concat(practiceWords)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var drills = new List<string>();
        foreach (var key in weakKeys)
        {
            var anchorWord = weakWords.FirstOrDefault(w => w.Contains(key, StringComparison.OrdinalIgnoreCase)) ??
                             weakWords.FirstOrDefault() ??
                             key;
            drills.Add($"Weak-key ladder '{key}': gõ {BuildNgramPattern(key)} trong 60s, rồi gõ anchor word '{anchorWord}' 5 lần sạch.");
        }

        foreach (var word in weakWords.Take(4))
            drills.Add($"Word repair '{word}': gõ chậm 2 lần, tăng 5% tốc ở lần 3, chỉ qua bài nếu không lặp cùng lỗi.");

        if (drills.Count == 0)
        {
            drills.Add("Home-row reset: 60s gõ asdf jkl; thật đều, đưa tay về vị trí gốc sau mỗi cụm.");
            drills.Add("Space rhythm: 90s gõ cụm 2-3 từ, nhấn space bằng ngón cái nhẹ và không kéo dài nhịp.");
            drills.Add("Punctuation control: luyện dấu câu chậm hơn 10%, đọc trước dấu kế tiếp rồi mới tăng tốc.");
        }

        drills.Add("Mixed weak-key sprint: chọn 3 drill yếu nhất, chạy 20s chậm + 20s vừa + 20s ghost pace.");
        return DeduplicateLines(drills, 8);
    }

    private static List<string> BuildNgramDrills(CoachAnalysis analysis)
    {
        var ngrams = analysis.TopMistypedNgrams
            .Select(ParseMistakeEntry)
            .Where(x => x.Length is >= 2 and <= 8)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var drills = new List<string>();
        foreach (var ngram in ngrams)
        {
            drills.Add($"N-gram isolate '{ngram}': gõ {BuildNgramPattern(ngram)} trong 45s, sai vẫn đi tiếp nhưng đánh dấu cụm.");
            drills.Add($"N-gram embed '{ngram}': tạo 5 từ/cụm chứa '{ngram}', gõ mỗi cụm 3 lần với accuracy >= 96%.");
        }

        if (drills.Count == 0)
        {
            drills.Add("Bigrams scan: chọn 3 cụm hai ký tự trong heatmap, gõ chậm 45s rồi tăng nhẹ 15s cuối.");
            drills.Add("Trigram rhythm: luyện cụm 3 ký tự quanh vị trí sai, giữ nhịp đều thay vì sửa bằng Backspace.");
            drills.Add("N-gram retest: sau 3 lượt, AI Coach phải thấy top n-gram đổi hoặc giảm count.");
        }

        drills.Add("Adaptive n-gram round: chọn n-gram sai nhiều nhất, buộc mọi passage luyện chứa cụm đó ít nhất 3 lần.");
        return DeduplicateLines(drills, 8);
    }

    private static string BuildNgramPattern(string key)
    {
        var token = NormalizeTextForCoach(key).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return "home row";

        if (token.Length == 1)
            return $"{token}a {token}e a{token} e{token} {token}{token}";

        return $"{token} {token.ToLowerInvariant()} {token}{token[0]}";
    }

    private static List<string> BuildSpacedRepetitionPlan(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        IReadOnlyList<string> practiceWords,
        string recommendedMode,
        string recommendedDifficulty)
    {
        var focusToken = practiceWords.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? analysis.FocusArea.ToLowerInvariant();
        var targetAccuracy = Math.Clamp(decimal.Round(Math.Max(request.Accuracy + 1m, 92m), 1), 70m, 99.5m);
        var targetWpm = Math.Clamp(decimal.Round(analysis.EstimatedNextWpm, 1), 10m, 250m);
        var modeLabel = BuildModeLabel(recommendedMode);
        var difficultyLabel = BuildDifficultyLabel(recommendedDifficulty);

        return DeduplicateLines(new[]
        {
            $"Today: 3 lượt weak-key deck với focus '{focusToken}', accuracy >= {targetAccuracy:F1}% trước khi đua.",
            $"Tomorrow: mở bằng 2 phút {modeLabel} cấp {difficultyLabel}, chỉ tăng tốc khi lỗi lặp lại <= 2.",
            $"Day 3: retest cùng focus '{focusToken}', mục tiêu WPM >= {Math.Max(10m, targetWpm - 1.5m):F1} và heatmap giảm.",
            $"Day 7: chạy boss round dài nhất, mục tiêu WPM >= {targetWpm:F1}, accuracy >= {targetAccuracy:F1}%.",
            "After each retest: xuất report hoặc lưu kết quả để so heatmap, ghost target và checklist mới.",
        }, 6);
    }

    private static List<string> BuildMasteryCheckpoints(
        GetAiCoachRequest request,
        CoachAnalysis analysis,
        decimal ghostTargetWpm,
        decimal ghostTargetAccuracy,
        IReadOnlyList<string> weakKeyDrills)
    {
        var firstDrill = weakKeyDrills.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? analysis.FocusArea;
        var maxDensity = Math.Max(0.01m, Math.Min(0.08m, analysis.MistakeDensity * 0.75m));
        var consistencyFloor = request.RecentRaceCount >= 4 ? "delta WPM không âm trong 3 lượt" : "hoàn thành 3 lượt liên tiếp";

        return DeduplicateLines(new[]
        {
            $"Gate 1: hoàn thành drill '{firstDrill}' với lỗi lặp lại <= 2 trước khi tăng độ khó.",
            $"Gate 2: thắng ghost {ghostTargetWpm:F1} WPM nhưng accuracy không thấp hơn {ghostTargetAccuracy:F1}%.",
            $"Gate 3: mật độ lỗi còn <= {maxDensity * 100m:F1}% hoặc top weak-key giảm ít nhất 1 bậc.",
            $"Gate 4: {consistencyFloor}, không đổi mode giữa chừng.",
            "Gate 5: AI Coach lần sau phải tạo heatmap khác hoặc checkpoint này chưa được xem là qua.",
        }, 6);
    }

    private static string BuildModeLabel(string mode)
    {
        return mode switch
        {
            Constants.GameModeAccuracy => "Accuracy Challenge",
            Constants.GameModeNoBackspace => "No Backspace",
            Constants.GameModeSuddenDeath => "Sudden Death",
            Constants.GameModeAiPractice => "AI Practice",
            _ => "Classic",
        };
    }

    private static string BuildDifficultyLabel(string difficulty)
    {
        return difficulty switch
        {
            Constants.AiPracticeMedium => "vừa",
            Constants.AiPracticeHard => "khó",
            Constants.AiPracticeNightmare => "ác mộng",
            _ => "dễ",
        };
    }

    private static string BuildGhostRewardBadge(string focusArea)
    {
        return focusArea switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" => "Ghost Precision",
            "Khôi phục tốc độ" or "Tốc độ nền" => "Ghost Tempo",
            "Giữ nhịp cuối bài" => "Ghost Finisher",
            "Bứt tốc 20 giây đầu" => "Ghost Starter",
            _ => "Ghost Challenger",
        };
    }

    private static string BuildChallengeBadge(string focusArea)
    {
        return focusArea switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" => "Precision Streak",
            "Khôi phục tốc độ" or "Tốc độ nền" => "Tempo Builder",
            "Giữ nhịp cuối bài" => "Endgame Control",
            "Bứt tốc 20 giây đầu" => "Fast Start",
            "Hoàn thành bài đua" => "Race Finisher",
            _ => "Daily Grinder",
        };
    }

    private static string BuildTrainingTitle(CoachAnalysis analysis, string difficulty)
    {
        var difficultyLabel = difficulty switch
        {
            Constants.AiPracticeMedium => "vừa",
            Constants.AiPracticeHard => "khó",
            Constants.AiPracticeNightmare => "ác mộng",
            _ => "dễ",
        };

        return $"Gói luyện {analysis.FocusArea.ToLowerInvariant()} - cấp {difficultyLabel}";
    }

    private static TrainingPlan BuildRecommendedTrainingPlan(GetAiCoachRequest request, CoachAnalysis analysis)
    {
        var focus = analysis.FocusArea;
        var difficulty = request.Wpm switch
        {
            >= 75m when request.Accuracy >= 96m => Constants.AiPracticeNightmare,
            >= 55m when request.Accuracy >= 93m => Constants.AiPracticeHard,
            >= 35m when request.Accuracy >= 88m => Constants.AiPracticeMedium,
            _ => Constants.AiPracticeEasy,
        };

        var mode = focus switch
        {
            "Độ chính xác" or "Ổn định độ chính xác" => Constants.GameModeAccuracy,
            "Khôi phục tốc độ" or "Tốc độ nền" => Constants.GameModeAiPractice,
            "Bứt tốc 20 giây đầu" => Constants.GameModeAiPractice,
            "Giữ nhịp cuối bài" => Constants.GameModeNoBackspace,
            "Hoàn thành bài đua" => Constants.GameModeClassic,
            _ => request.Accuracy >= 97m ? Constants.GameModeNoBackspace : Constants.GameModeClassic,
        };

        if (focus == "Độ chính xác" && request.Accuracy < 85m)
            difficulty = Constants.AiPracticeEasy;
        else if (focus == "Ổn định độ chính xác" && difficulty == Constants.AiPracticeNightmare)
            difficulty = Constants.AiPracticeHard;
        else if ((focus == "Khôi phục tốc độ" || focus == "Tốc độ nền") && difficulty == Constants.AiPracticeEasy && request.Wpm >= 28m)
            difficulty = Constants.AiPracticeMedium;

        return new TrainingPlan(
            mode,
            difficulty,
            Constants.GetAiPracticeTargetRpm(difficulty));
    }

    private static string BuildFallbackCoachText(GetAiCoachRequest request, CoachAnalysis analysis)
    {
        var completion = request.IsCompleted
            ? "Bạn đã hoàn thành race."
            : "Bạn chưa hoàn thành race, cần ưu tiên độ ổn định trước.";

        var trend = request.RecentRaceCount >= 4
            ? $" Xu hướng gần đây: WPM {request.RecentWpmTrend:+0.0;-0.0;0}, accuracy {request.RecentAccuracyTrend:+0.0;-0.0;0}%."
            : string.Empty;

        return $"{completion} Trọng tâm hiện tại: {analysis.FocusArea}. " +
               $"Mức kỹ năng: {analysis.SkillTier}. " +
               $"Mật độ lỗi: {analysis.MistakeDensity * 100m:F1}%. " +
               $"Nếu bám đúng plan, WPM kế tiếp có thể đạt khoảng {analysis.EstimatedNextWpm:F1}.{trend}";
    }

    private static CoachAnalysis AnalyzePerformance(GetAiCoachRequest request, IReadOnlyList<MistakeMemorySample> mistakeSamples)
    {
        var totalChars = Math.Max(1, request.CharsCorrect + request.CharsWrong);
        var errorRate = (decimal)request.CharsWrong / totalChars;
        var language = NormalizeLanguage(request.Language);
        var completionRate = EstimateCompletionRate(request);
        var hardWords = ExtractHardWords(request.PassageText)
            .Concat(mistakeSamples.SelectMany(sample => ExtractHardWords(sample.PassageText)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        var mistakes = AnalyzeMistakes(request, mistakeSamples);

        var focusArea = DetermineFocusArea(request, errorRate, completionRate);
        var skillTier = DetermineSkillTier(request.Wpm, request.Accuracy, completionRate);
        var estimatedNextWpm = EstimateNextWpm(request, focusArea, errorRate, completionRate);

        var tips = BuildTips(request, focusArea, errorRate, completionRate);
        var actionPlan = BuildActionPlan(focusArea, completionRate);
        var drills = BuildDrills(focusArea, hardWords);
        var passages = BuildSuggestedPassages(
            language,
            focusArea,
            mistakes.TopMistypedCharacters,
            mistakes.TopMistypedWords,
            mistakes.TopMistypedNgrams);

        return new CoachAnalysis
        {
            FocusArea = focusArea,
            SkillTier = skillTier,
            EstimatedNextWpm = estimatedNextWpm,
            CompletionRate = completionRate,
            MistakeDensity = mistakes.MistakeDensity,
            Tips = DeduplicateLines(tips, 5),
            ActionPlan = DeduplicateLines(actionPlan, 5),
            PersonalizedDrills = DeduplicateLines(drills, 4),
            SuggestedPassages = DeduplicateLines(passages, SuggestedPassageLimit),
            TopMistypedCharacters = mistakes.TopMistypedCharacters,
            TopMistypedWords = mistakes.TopMistypedWords,
            TopMistypedNgrams = mistakes.TopMistypedNgrams,
        };
    }

    private static string DetermineFocusArea(GetAiCoachRequest request, decimal errorRate, decimal completionRate)
    {
        if (!request.IsCompleted && completionRate < 0.70m)
            return "Hoàn thành bài đua";
        if (request.RecentRaceCount >= 4 && request.RecentAccuracyTrend <= -2m)
            return "Ổn định độ chính xác";
        if (request.RecentRaceCount >= 4 && request.RecentWpmTrend <= -4m)
            return "Khôi phục tốc độ";
        if (request.Accuracy < 90m || errorRate >= 0.09m)
            return "Độ chính xác";
        if (!request.IsCompleted || completionRate < 0.95m)
            return "Giữ nhịp cuối bài";
        if (request.Wpm < 45m)
            return "Tốc độ nền";
        if (request.Position > Math.Max(1, request.TotalPlayers / 2))
            return "Bứt tốc 20 giây đầu";
        return "Ổn định phong độ";
    }

    private static string DetermineSkillTier(decimal wpm, decimal accuracy, decimal completionRate)
    {
        if (completionRate < 0.70m)
            return "Developing";
        if (wpm < 30m || accuracy < 85m) return "Beginner";
        if (wpm < 50m || accuracy < 92m) return "Developing";
        if (wpm < 75m) return "Advanced";
        return "Elite";
    }

    private static decimal EstimateNextWpm(GetAiCoachRequest request, string focusArea, decimal errorRate, decimal completionRate)
    {
        decimal boost = focusArea switch
        {
            "Ổn định độ chính xác" => 3.2m,
            "Khôi phục tốc độ" => 3.8m,
            "Độ chính xác" => request.Accuracy < 88m ? 4.5m : 3m,
            "Tốc độ nền" => 5.5m,
            "Giữ nhịp cuối bài" => 3.8m,
            "Bứt tốc 20 giây đầu" => 3.5m,
            "Hoàn thành bài đua" => 2.5m,
            _ => 2m,
        };

        if (errorRate > 0.12m)
            boost -= 1.2m;
        if (completionRate < 0.80m)
            boost -= 1.0m;

        var estimate = request.Wpm + boost;
        return Math.Clamp(decimal.Round(estimate, 1), 5m, 250m);
    }

    private static List<string> BuildTips(GetAiCoachRequest request, string focusArea, decimal errorRate, decimal completionRate)
    {
        var tips = new List<string>
        {
            $"Trọng tâm buổi này: {focusArea}.",
            $"Tỷ lệ hoàn thành hiện tại khoảng {completionRate * 100m:F1}%.",
        };

        if (request.RecentRaceCount >= 4)
        {
            tips.Add($"Xu hướng {request.RecentRaceCount} race gần nhất: WPM {request.RecentWpmTrend:+0.0;-0.0;0}, accuracy {request.RecentAccuracyTrend:+0.0;-0.0;0}%.");
        }

        if (request.Accuracy < 92m)
            tips.Add($"Giảm lỗi trước khi tăng tốc: mục tiêu accuracy >= 92% (hiện tại {request.Accuracy:F1}%).");

        if (request.Wpm < 50m)
            tips.Add($"Giữ nhịp đều theo cụm 2-3 từ để tăng WPM ổn định (hiện tại {request.Wpm:F1}).");

        if (errorRate >= 0.08m)
            tips.Add("Khi gặp từ dài hoặc có dấu câu, giảm nhịp 5% để tránh chuỗi lỗi liên tiếp.");

        if (!request.IsCompleted || completionRate < 0.95m)
            tips.Add("Ưu tiên giữ nhịp 20 giây cuối để về đích ổn định, chưa cần đẩy tốc tối đa.");

        if (focusArea == "Ổn định độ chính xác")
            tips.Add("Đặt trần tốc độ thấp hơn 8% trong 3 race kế tiếp để kéo accuracy quay lại mốc an toàn.");

        if (focusArea == "Khôi phục tốc độ")
            tips.Add("Bật nhịp metronome cố định trong 2 phút đầu để tránh dao động tốc độ quá mạnh.");

        if (tips.Count < 4)
            tips.Add("Trong 15 giây đầu race, tập trung chính xác để xây đà tâm lý và nhịp gõ.");

        while (tips.Count < 3)
        {
            tips.Add("Luyện 2 race ngắn liên tiếp và nghỉ 20 giây giữa mỗi race để giữ nhịp.");
        }

        return tips;
    }

    private static List<string> BuildActionPlan(string focusArea, decimal completionRate)
    {
        var steps = new List<string>
        {
            "2 phút warm-up: gõ chậm, không nhìn bàn phím, mục tiêu accuracy >= 96%.",
        };

        switch (focusArea)
        {
            case "Ổn định độ chính xác":
                steps.Add("4 phút consistency drill: giữ cùng một cadence trong toàn bộ race, không đổi nhịp đột ngột.");
                steps.Add("2 phút error review: ghi 5 ký tự sai nhiều nhất và gõ lại theo cặp từ.");
                break;
            case "Khôi phục tốc độ":
                steps.Add("4 phút tempo rebuild: tăng 3 WPM mỗi 45 giây cho tới khi chạm mức race trung bình.");
                steps.Add("2 phút burst reset: 15 giây nhanh + 25 giây ổn định, lặp 3 vòng.");
                break;
            case "Độ chính xác":
                steps.Add("4 phút drill chính xác: giảm tốc 10-15%, tập trung ký tự dễ sai.");
                steps.Add("2 phút punctuation drill: luyện cụm có dấu câu và khoảng trắng.");
                break;
            case "Tốc độ nền":
                steps.Add("4 phút cadence drill: tăng dần nhịp mỗi 60 giây nhưng giữ accuracy >= 90%.");
                steps.Add("2 phút burst drill: 20 giây nhanh + 20 giây ổn định lặp lại.");
                break;
            case "Hoàn thành bài đua":
                steps.Add("4 phút endurance drill: gõ hết passage, không dừng giữa chừng.");
                steps.Add("2 phút ổn định cổ tay và nhịp thở để giữ tập trung.");
                break;
            case "Giữ nhịp cuối bài":
                steps.Add("4 phút last-30%-drill: cố định nhịp trong đoạn cuối, không đổi cadence đột ngột.");
                steps.Add("2 phút recovery drill: sai 1 ký tự vẫn đi tiếp, không dừng lại sửa.");
                break;
            default:
                steps.Add("4 phút race simulation: giữ nhịp ngay từ 20 giây đầu.");
                steps.Add("2 phút review lỗi: ghi lại 3 lỗi lặp lại nhiều nhất.");
                break;
        }

        if (completionRate < 0.85m)
            steps.Add("1 phút checkpoint: sau mỗi 20 giây tự nhắc giữ nhịp thay vì tăng tốc.");

        steps.Add("2 phút final run: chạy 1 race full và so sánh WPM/accuracy với race trước.");
        return steps;
    }

    private static List<string> BuildDrills(string focusArea, List<string> hardWords)
    {
        var drills = new List<string>();
        if (hardWords.Count > 0)
        {
            drills.Add($"Word drill: lặp 3 vòng các từ khó \"{string.Join(", ", hardWords)}\".");
        }

        if (focusArea == "Độ chính xác")
        {
            drills.Add("Accuracy ladder: mỗi lần sai 1 ký tự thì lùi lại 1 từ và gõ lại đúng.");
            drills.Add("No-backspace drill: không dùng Backspace trong 90 giây để rèn tập trung.");
        }
        else if (focusArea == "Ổn định độ chính xác")
        {
            drills.Add("Stability drill: 3 race liên tiếp giữ accuracy > 94% với WPM dao động < 5.");
            drills.Add("Error map drill: gõ lại cụm chứa ký tự sai nhiều nhất trong 2 phút.");
        }
        else if (focusArea == "Khôi phục tốc độ")
        {
            drills.Add("Tempo ladder: tăng dần 2 WPM sau mỗi 40 giây nhưng không để accuracy < 90%.");
            drills.Add("Rhythm lock: gõ theo nhịp cố định 65 BPM trong 90 giây rồi tăng lên 72 BPM.");
        }
        else if (focusArea == "Tốc độ nền")
        {
            drills.Add("Metronome typing: giữ tốc độ gõ đều theo nhịp 60-70 BPM.");
            drills.Add("Sprint blocks: 4 hiệp, mỗi hiệp 25 giây bứt tốc + 20 giây hồi nhịp.");
        }
        else
        {
            drills.Add("Opening burst drill: 15 giây đầu tăng tốc vừa phải, không hy sinh accuracy.");
            drills.Add("Endurance block: gõ liền mạch 2 phút, ưu tiên hoàn thành đầy đủ.");
        }

        return drills.Take(4).ToList();
    }

    private static decimal EstimateCompletionRate(GetAiCoachRequest request)
    {
        var passageLength = request.PassageText.Length;
        if (passageLength <= 0)
        {
            if (request.IsCompleted)
                return 1m;

            var totalTyped = Math.Max(1, request.CharsCorrect + request.CharsWrong);
            return Math.Clamp((decimal)request.CharsCorrect / totalTyped, 0m, 1m);
        }

        return Math.Clamp((decimal)request.CharsCorrect / passageLength, 0m, 1m);
    }

    private static MistakeProfile AnalyzeMistakes(GetAiCoachRequest request, IReadOnlyList<MistakeMemorySample> mistakeSamples)
    {
        var charCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ngramCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        AddObservedCounts(charCounts, wordCounts, ngramCounts, mistakeSamples);
        AddMismatchCounts(charCounts, wordCounts, ngramCounts, request.PassageText, request.TypedText);

        if (charCounts.Count == 0 && wordCounts.Count == 0 && ngramCounts.Count == 0)
            return AnalyzeMistakes(request.PassageText, request.TypedText);

        var topMistypedChars = charCounts
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(TopCharacterMistakeLimit)
            .Select(kv => $"{kv.Key} ({kv.Value} lần)")
            .ToList();

        var topMistypedWords = wordCounts
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(TopWordMistakeLimit)
            .Select(kv => $"{kv.Key} ({kv.Value} lần)")
            .ToList();

        var topMistypedNgrams = ngramCounts
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(TopNgramMistakeLimit)
            .Select(kv => $"{kv.Key} ({kv.Value} lần)")
            .ToList();

        var observedEvents = Math.Max(
            request.CharsWrong,
            mistakeSamples.SelectMany(x => x.ObservedMistakeCharacters).Sum(x => x.Value) / 2);
        var denominator = Math.Max(1, request.PassageText.Length);
        var density = Math.Clamp(
            decimal.Round((decimal)observedEvents / denominator, MaxMistakeDensityPrecision, MidpointRounding.AwayFromZero),
            0m,
            1m);

        return new MistakeProfile
        {
            TopMistypedCharacters = topMistypedChars,
            TopMistypedWords = topMistypedWords,
            TopMistypedNgrams = topMistypedNgrams,
            MistakeDensity = density,
        };
    }

    private static void AddObservedCounts(
        Dictionary<string, int> charCounts,
        Dictionary<string, int> wordCounts,
        Dictionary<string, int> ngramCounts,
        IReadOnlyList<MistakeMemorySample> mistakeSamples)
    {
        foreach (var sample in mistakeSamples)
        {
            foreach (var item in sample.ObservedMistakeCharacters)
                Increment(charCounts, NormalizeMistakeToken(item.Key), item.Value);

            foreach (var item in sample.ObservedMistakeWords)
                Increment(wordCounts, NormalizeMistakeToken(item.Key), item.Value);

            foreach (var item in sample.ObservedMistakeNgrams)
                Increment(ngramCounts, NormalizeMistakeToken(item.Key), item.Value);
        }
    }

    private static void AddMismatchCounts(
        Dictionary<string, int> charCounts,
        Dictionary<string, int> wordCounts,
        Dictionary<string, int> ngramCounts,
        string passageText,
        string typedText)
    {
        foreach (var item in AnalyzeMistakeCharacters(NormalizeTextForCoach(passageText), NormalizeTextForCoach(typedText)))
            Increment(charCounts, FormatMistakeSymbol(item.Key), item.Value);

        foreach (var item in AnalyzeMistakeWords(NormalizeTextForCoach(passageText), NormalizeTextForCoach(typedText)))
            Increment(wordCounts, item.Key, item.Value);

        foreach (var item in AnalyzeMistakeNgrams(NormalizeTextForCoach(passageText), NormalizeTextForCoach(typedText)))
            Increment(ngramCounts, item.Key, item.Value);
    }

    private static void Increment(Dictionary<string, int> counts, string key, int amount)
    {
        if (string.IsNullOrWhiteSpace(key) || amount <= 0)
            return;

        counts[key] = counts.GetValueOrDefault(key) + amount;
    }

    private static string NormalizeMistakeToken(string? value)
        => NormalizeTextForCoach(value).Trim();

    private static MistakeProfile AnalyzeMistakes(string passageText, string typedText)
    {
        var passage = NormalizeTextForCoach(passageText);
        var typed = NormalizeTextForCoach(typedText);

        if (string.IsNullOrWhiteSpace(passage) || string.IsNullOrWhiteSpace(typed))
        {
            return new MistakeProfile
            {
                TopMistypedCharacters = new List<string>(),
                TopMistypedWords = new List<string>(),
                TopMistypedNgrams = new List<string>(),
                MistakeDensity = 0m,
            };
        }

        var topMistypedChars = AnalyzeMistakeCharacters(passage, typed)
            .Take(TopCharacterMistakeLimit)
            .Select(kv => $"{FormatMistakeSymbol(kv.Key)} ({kv.Value} lần)")
            .ToList();

        var topMistypedWords = AnalyzeMistakeWords(passage, typed)
            .Take(TopWordMistakeLimit)
            .Select(kv => $"{kv.Key} ({kv.Value} lần)")
            .ToList();

        var topMistypedNgrams = AnalyzeMistakeNgrams(passage, typed)
            .Take(TopNgramMistakeLimit)
            .Select(kv => $"{kv.Key} ({kv.Value} lần)")
            .ToList();

        var denominator = Math.Max(1, Math.Max(passage.Length, typed.Length));
        var mismatchCount = CountMismatchChars(passage, typed);

        return new MistakeProfile
        {
            TopMistypedCharacters = topMistypedChars,
            TopMistypedWords = topMistypedWords,
            TopMistypedNgrams = topMistypedNgrams,
            MistakeDensity = Math.Clamp(decimal.Round((decimal)mismatchCount / denominator, MaxMistakeDensityPrecision, MidpointRounding.AwayFromZero), 0m, 1m),
        };
    }

    private static int CountMismatchChars(string passage, string typed)
    {
        var max = Math.Max(passage.Length, typed.Length);
        var mismatch = 0;

        for (int i = 0; i < max; i++)
        {
            var expected = i < passage.Length ? passage[i] : '\0';
            var actual = i < typed.Length ? typed[i] : '\0';

            if (expected != actual)
                mismatch++;
        }

        return mismatch;
    }

    private static string FormatMistakeSymbol(char symbol)
    {
        return symbol switch
        {
            ' ' => "space",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => symbol.ToString(),
        };
    }

    private static IEnumerable<KeyValuePair<char, int>> AnalyzeMistakeCharacters(string passage, string typedSamples)
    {
        var counts = new Dictionary<char, int>();

        var max = Math.Max(passage.Length, typedSamples.Length);
        for (int i = 0; i < max; i++)
        {
            var expected = i < passage.Length ? passage[i] : '\0';
            var actual = i < typedSamples.Length ? typedSamples[i] : '\0';

            if (expected == actual)
                continue;

            if (expected != '\0' && !char.IsWhiteSpace(expected) && !char.IsControl(expected))
            {
                counts[expected] = counts.GetValueOrDefault(expected) + 1;
            }

            if (actual != '\0' && !char.IsWhiteSpace(actual) && !char.IsControl(actual) && actual != expected)
            {
                counts[actual] = counts.GetValueOrDefault(actual) + 1;
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key);
    }

    private static IEnumerable<KeyValuePair<string, int>> AnalyzeMistakeWords(string passage, string typedSamples)
    {
        var passageTokens = TokenRegex.Matches(passage)
            .Select(m => NormalizeTextForCoach(m.Value).ToLowerInvariant())
            .Where(t => t.Length > 0)
            .ToList();

        var typedTokens = TokenRegex.Matches(typedSamples)
            .Select(m => NormalizeTextForCoach(m.Value).ToLowerInvariant())
            .Where(t => t.Length > 0)
            .ToList();

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var max = Math.Max(passageTokens.Count, typedTokens.Count);

        for (int i = 0; i < max; i++)
        {
            var expected = i < passageTokens.Count ? passageTokens[i] : string.Empty;
            var actual = i < typedTokens.Count ? typedTokens[i] : string.Empty;

            if (expected == actual)
                continue;

            if (!string.IsNullOrWhiteSpace(expected))
                counts[expected] = counts.GetValueOrDefault(expected) + 1;
            if (!string.IsNullOrWhiteSpace(actual) && actual != expected)
                counts[actual] = counts.GetValueOrDefault(actual) + 1;
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Where(kv => kv.Key.Length >= 2)
            .ToList();
    }

    private static IEnumerable<KeyValuePair<string, int>> AnalyzeMistakeNgrams(string passage, string typedSamples)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var max = Math.Max(passage.Length, typedSamples.Length);

        for (var i = 0; i < max; i++)
        {
            var expected = i < passage.Length ? passage[i] : '\0';
            var actual = i < typedSamples.Length ? typedSamples[i] : '\0';
            if (expected == actual || expected == '\0')
                continue;

            foreach (var ngram in ExtractNgramsAt(passage, i))
                Increment(counts, ngram, 1);
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Where(kv => kv.Key.Length >= 2)
            .ToList();
    }

    private static IEnumerable<string> ExtractNgramsAt(string text, int index)
    {
        if (string.IsNullOrWhiteSpace(text) || index < 0 || index >= text.Length || !IsWordChar(text[index]))
            return Enumerable.Empty<string>();

        var start = index;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;

        var end = index;
        while (end + 1 < text.Length && IsWordChar(text[end + 1]))
            end++;

        var word = text[start..(end + 1)].Trim().ToLowerInvariant();
        if (word.Length < 2)
            return Enumerable.Empty<string>();

        var localIndex = Math.Clamp(index - start, 0, word.Length - 1);
        var ngrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var size = 2; size <= Math.Min(4, word.Length); size++)
        {
            var leftStart = Math.Clamp(localIndex - size + 1, 0, word.Length - size);
            ngrams.Add(word.Substring(leftStart, size));

            if (localIndex + size <= word.Length)
                ngrams.Add(word.Substring(localIndex, size));
        }

        return ngrams;
    }

    private static bool IsWordChar(char value)
        => char.IsLetterOrDigit(value) || value == '\'' || value == '-';

    private static List<string> DeduplicateLines(IEnumerable<string> source, int maxCount)
    {
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in source)
        {
            var line = NormalizeTextForCoach(raw);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var key = line.TrimEnd('.', '!', '?', ' ').ToLowerInvariant();
            if (!seen.Add(key))
                continue;

            output.Add(line);
            if (output.Count >= maxCount)
                break;
        }

        return output;
    }

    private static string BuildPassageSample(string passageText)
    {
        var compact = NormalizeTextForCoach(passageText);
        if (compact.Length > 220)
            compact = compact[..220];
        return compact;
    }

    private static string NormalizeTextForCoach(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        normalized = MultiSpaceRegex.Replace(normalized, " ").Trim();
        return normalized;
    }

    private static string NormalizeTypedTextForCoach(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = NormalizeTextForCoach(value);
        return normalized.Length > maxLength
            ? normalized[..maxLength]
            : normalized;
    }

    private static List<string> BuildSuggestedPassages(
        string language,
        string focusArea,
        IReadOnlyList<string> topMistypedCharacters,
        IReadOnlyList<string> topMistypedWords,
        IReadOnlyList<string> topMistypedNgrams)
    {
        if (topMistypedCharacters.Count > 0 || topMistypedWords.Count > 0 || topMistypedNgrams.Count > 0)
        {
            var chars = topMistypedCharacters
                .Select(ParseMistakeEntry)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(TopCharacterMistakeLimit)
                .ToList();

            var words = topMistypedWords
                .Select(ParseMistakeEntry)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(TopWordMistakeLimit)
                .ToList();

            var ngrams = topMistypedNgrams
                .Select(ParseMistakeEntry)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(TopNgramMistakeLimit)
                .ToList();

            return DeduplicateLines(
                BuildMistakeTargetedPassages(language, chars, words, ngrams)
                    .Concat(AiFallbackPassageBank.Pick(language, focusArea, chars.Concat(ngrams).ToList(), words, SuggestedPassageLimit)),
                SuggestedPassageLimit);
        }

        if (language == "vi")
        {
            var builtIn = focusArea switch
            {
                "Độ chính xác" => new List<string>
                {
                    "Buổi sáng trời mát, tôi đi dạo quanh hồ và lắng nghe tiếng chim hót trong vòm cây xanh.",
                    "Khi viết báo cáo, hãy kiểm tra dấu câu cẩn thận để câu văn rõ nghĩa và dễ đọc hơn.",
                    "Sự bình tĩnh giúp bạn gõ chính xác, đặc biệt ở những cụm từ dài và nhiều dấu tiếng Việt.",
                    "Người luyện tốt thường đọc trước một nhịp, sau đó gõ từng cụm nhỏ để tránh cuốn theo tốc độ.",
                    "Nếu thấy tay bắt đầu vội, hãy giảm tốc nhẹ và giữ chính xác từng dấu thanh trong câu.",
                    "Một đoạn văn rõ ràng sẽ dễ gõ hơn khi bạn chia nó thành các cụm ngắn có nhịp ổn định."
                },
                "Tốc độ nền" => new List<string>
                {
                    "Tôi luyện gõ mỗi ngày mười phút để tăng tốc độ ổn định mà vẫn giữ độ chính xác cao.",
                    "Nhịp gõ đều, cổ tay thả lỏng và mắt tập trung vào màn hình là ba yếu tố rất quan trọng.",
                    "Khi giữ được nhịp, bạn sẽ tăng WPM tự nhiên mà không cần gồng tay quá mức.",
                    "Hãy giữ nhịp từ đầu đến cuối, tăng tốc ở những cụm quen và chậm lại ở cụm nhiều dấu.",
                    "Tốc độ bền đến từ nhiều lần chạy ngắn, mỗi lần chỉ tăng một chút nhưng không đánh đổi accuracy.",
                    "Trong bài luyện này, hãy đọc trước ba từ và để ngón tay di chuyển theo nhịp đều."
                },
                _ => new List<string>
                {
                    "Mỗi trận đua là một cơ hội cải thiện bản thân, vì vậy hãy giữ tập trung từ đầu đến cuối.",
                    "Nếu lỡ sai vài ký tự, đừng hoảng; chỉ cần lấy lại nhịp và tiếp tục gõ thật đều.",
                    "Kết quả tốt đến từ thói quen luyện tập đều đặn, không phải từ một buổi cố gắng quá sức.",
                    "Khi bắt đầu bài mới, hãy nhìn toàn câu trước rồi mới tăng tốc trong những cụm dễ.",
                    "Một nhịp thở ngắn trước đoạn khó giúp tay bớt căng và giảm lỗi dây chuyền.",
                    "Sau mỗi lỗi, hãy quay lại nhịp chính thay vì cố bù tốc ở những từ tiếp theo."
                },
            };

            return DeduplicateLines(
                AiFallbackPassageBank.Pick(language, focusArea, Array.Empty<string>(), Array.Empty<string>(), SuggestedPassageLimit)
                    .Concat(builtIn),
                SuggestedPassageLimit);
        }

        var englishBuiltIn = focusArea switch
        {
            "Độ chính xác" => new List<string>
            {
                "Clear punctuation and steady spacing often decide whether a fast race stays accurate.",
                "Type each clause with control, then gradually increase pace without breaking your rhythm.",
                "Precision in the first minute usually creates confidence for the rest of the passage.",
                "Read a few words ahead, keep your shoulders relaxed, and let accuracy set the pace.",
                "When a phrase looks tricky, slow down before the mistake instead of fixing it afterward.",
                "A clean sentence with steady spacing is better practice than a rushed line full of corrections."
            },
            "Tốc độ nền" => new List<string>
            {
                "Consistent tempo beats random bursts when you want long term typing speed growth.",
                "Relax your shoulders, keep your wrists neutral, and let your fingers do the work.",
                "Short acceleration blocks can raise your baseline speed while accuracy stays stable.",
                "Build speed in small steps, then protect the rhythm when the sentence becomes less familiar.",
                "A smooth opening helps the whole race feel lighter, even when the final line is harder.",
                "Practice short blocks with one steady cadence before trying to sprint through the passage."
            },
            _ => new List<string>
            {
                "Strong starts come from calm breathing and deliberate keystrokes in the opening seconds.",
                "When mistakes happen, recover quickly and continue instead of forcing perfect backtracking.",
                "A focused ten minute session can improve both confidence and race consistency.",
                "Keep your eyes ahead of your hands so the next phrase is ready before your fingers arrive.",
                "The best recovery after an error is a steady rhythm, not a sudden burst of extra speed.",
                "Finish the final words with the same control you used at the start of the race."
            },
        };

        return DeduplicateLines(
            AiFallbackPassageBank.Pick(language, focusArea, Array.Empty<string>(), Array.Empty<string>(), SuggestedPassageLimit)
                .Concat(englishBuiltIn),
            SuggestedPassageLimit);
    }

    private static string ParseMistakeEntry(string item)
    {
        var raw = item.Trim();
        var openParen = raw.IndexOf("(", StringComparison.Ordinal);
        if (openParen > 0)
            return raw[..openParen].Trim();

        if (raw == "space" || raw == "\\n" || raw == "\\r" || raw == "\\t")
            return string.Empty;

        return raw;
    }

    private static List<string> BuildMistakeTargetedPassages(
        string language,
        IReadOnlyList<string> chars,
        IReadOnlyList<string> words,
        IReadOnlyList<string> ngrams)
    {
        var charBlock = BuildRepeatedCharBlock(chars, 24);
        var wordBlock = BuildRepeatedWordBlock(words, 18);
        var ngramBlock = BuildRepeatedWordBlock(ngrams, 20);
        var ngramFocus = string.IsNullOrWhiteSpace(ngramBlock)
            ? (string.IsNullOrWhiteSpace(charBlock) ? wordBlock : charBlock)
            : ngramBlock;
        var phraseFocus = string.IsNullOrWhiteSpace(wordBlock) ? ngramFocus : wordBlock;
        var merged = string.Join(" ", new[] { charBlock, ngramBlock, wordBlock }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        var reminder = BuildFocusReminder(language);

        var result = new List<string>
        {
            BuildMistakeOpening(language, charBlock, wordBlock),
            string.IsNullOrWhiteSpace(merged)
                ? BuildMistakeOpening(language, string.Empty, string.Empty)
                : language == "vi"
                    ? $"Luyện tập trọng điểm: {merged}. {reminder}"
                    : $"Focused drill for frequent misses: {merged}. {reminder}",
            language == "vi"
                ? "Tập trung 3 vòng: gõ chậm, rồi tăng tốc nhẹ; đánh dấu điểm sai và sửa ngay."
                : "Do 3 rounds: start slow, then gradually speed up; mark the exact misses and fix immediately.",
            language == "vi"
                ? $"Đọc trước một cụm ngắn rồi gõ đều tay: {phraseFocus}. Đừng tăng tốc khi gặp {ngramFocus}."
                : $"Read one short phrase ahead, then type evenly: {phraseFocus}. Do not rush when {ngramFocus} appears.",
            language == "vi"
                ? $"Bài kiểm soát lỗi: {merged}. Sai thì vẫn đi tiếp, nhưng ghi nhớ vị trí vừa lệch nhịp."
                : $"Error control drill: {merged}. Continue after a mistake, but remember the exact spot that broke rhythm.",
            language == "vi"
                ? $"Giữ accuracy trước tốc độ trong đoạn này: {phraseFocus}. Mục tiêu là ít lỗi lặp lại hơn vòng trước."
                : $"Protect accuracy before speed in this line: {phraseFocus}. The goal is fewer repeated misses than last round.",
            language == "vi"
                ? $"Luyện nhịp ổn định với n-gram {ngramFocus}; mỗi cụm gõ chậm một lần rồi tăng nhẹ ở lần sau."
                : $"Stabilize rhythm with n-grams {ngramFocus}; type each phrase slowly once, then add a little pace next time.",
            language == "vi"
                ? $"Chạy một lượt cuối với {merged}. Tập trung mắt vào từ kế tiếp thay vì nhìn lại lỗi cũ."
                : $"Run one final pass with {merged}. Keep your eyes on the next word instead of staring at the old mistake."
        };

        return DeduplicateLines(result, SuggestedPassageLimit);
    }

    private static string BuildMistakeOpening(string language, string charBlock, string wordBlock)
    {
        var charPart = string.IsNullOrWhiteSpace(charBlock) ? "các ký tự khó" : charBlock;
        var wordPart = string.IsNullOrWhiteSpace(wordBlock) ? "các cụm từ hay sai" : wordBlock;

        if (language == "vi")
        {
            return $"Luyện nhanh: {charPart} {wordPart}. Giữ nhịp đều, sửa ngay mỗi lỗi sai.";
        }

        return $"Quick drill: {charPart} {wordPart}. Keep rhythm steady and correct immediately.";
    }

    private static string BuildFocusReminder(string language)
    {
        return language == "vi"
            ? "Mục tiêu: sai ≤ 2%, nhịp ổn định, không vội tốc."
            : "Target: errors <= 2%, rhythm stable, no speed rush.";
    }

    private static string BuildRepeatedCharBlock(IReadOnlyList<string> chars, int targetLength)
    {
        if (chars.Count == 0)
            return string.Empty;

        var pattern = string.Join(" ", chars.Select(c => string.Concat(Enumerable.Repeat(c, 2))));
        while (!string.IsNullOrWhiteSpace(pattern) && pattern.Length < targetLength)
            pattern = $"{pattern} {pattern}";

        return pattern.Trim();
    }

    private static string BuildRepeatedWordBlock(IReadOnlyList<string> words, int targetLength)
    {
        if (words.Count == 0)
            return string.Empty;

        var pattern = string.Join(" ", words);
        while (!string.IsNullOrWhiteSpace(pattern) && pattern.Length < targetLength)
            pattern = $"{pattern} {pattern}";

        return pattern.Trim();
    }

    private static IEnumerable<string> ExtractHardWords(string passageText)
    {
        if (string.IsNullOrWhiteSpace(passageText))
            return Enumerable.Empty<string>();

        var normalized = NormalizeTextForCoach(passageText);

        return TokenRegex.Matches(normalized)
            .Select(m => m.Value.Trim())
            .Where(w => w.Length >= 6)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(w => w.Length)
            .Take(6)
            .ToList();
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "vi").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            _ => "vi",
        };
    }

    private sealed class CoachAnalysis
    {
        public string FocusArea { get; set; } = string.Empty;
        public string SkillTier { get; set; } = string.Empty;
        public decimal EstimatedNextWpm { get; set; }
        public decimal CompletionRate { get; set; }
        public decimal MistakeDensity { get; set; }
        public List<string> TopMistypedCharacters { get; set; } = new();
        public List<string> TopMistypedWords { get; set; } = new();
        public List<string> TopMistypedNgrams { get; set; } = new();
        public List<string> Tips { get; set; } = new();
        public List<string> ActionPlan { get; set; } = new();
        public List<string> PersonalizedDrills { get; set; } = new();
        public List<string> SuggestedPassages { get; set; } = new();
    }

    private sealed class MistakeProfile
    {
        public List<string> TopMistypedCharacters { get; set; } = new();
        public List<string> TopMistypedWords { get; set; } = new();
        public List<string> TopMistypedNgrams { get; set; } = new();
        public decimal MistakeDensity { get; set; }
    }

    private sealed class AiGeneratedCoach
    {
        public string CoachText { get; set; } = string.Empty;
        public string TrainingTitle { get; set; } = string.Empty;
        public string RecommendedGameMode { get; set; } = string.Empty;
        public string RecommendedDifficulty { get; set; } = string.Empty;
        public int RecommendedTargetRpm { get; set; }
        public decimal GhostTargetWpm { get; set; }
        public decimal GhostTargetAccuracy { get; set; }
        public string GhostRewardBadge { get; set; } = string.Empty;
        public string DailyChallengeTitle { get; set; } = string.Empty;
        public string DailyChallengeGoal { get; set; } = string.Empty;
        public string DailyChallengeReward { get; set; } = string.Empty;
        public List<string> Tips { get; set; } = new();
        public List<string> ActionPlan { get; set; } = new();
        public List<string> PersonalizedDrills { get; set; } = new();
        public List<string> PracticeWords { get; set; } = new();
        public List<string> AdaptiveMicroLessons { get; set; } = new();
        public List<string> MistakeHeatmap { get; set; } = new();
        public List<string> NextSessionChecklist { get; set; } = new();
        public List<string> GhostRacePlan { get; set; } = new();
        public List<string> FingerDiagnostics { get; set; } = new();
        public List<string> ProgressPrediction { get; set; } = new();
        public List<string> LessonLadder { get; set; } = new();
        public List<string> AttemptReplayCues { get; set; } = new();
        public List<string> WeakKeyDrills { get; set; } = new();
        public List<string> NgramDrills { get; set; } = new();
        public List<string> SpacedRepetitionPlan { get; set; } = new();
        public List<string> MasteryCheckpoints { get; set; } = new();
        public List<AiPracticeMissionDto> PracticeMissions { get; set; } = new();
        public string ProblemKeyStoryTitle { get; set; } = string.Empty;
        public string ProblemKeyStoryTopic { get; set; } = string.Empty;
        public string ProblemKeyStoryPassage { get; set; } = string.Empty;
        public List<string> ProblemKeyStoryKeys { get; set; } = new();
        public List<string> MistakeFingerprint { get; set; } = new();
        public List<string> AdaptiveRaceStrategy { get; set; } = new();
        public decimal PersonalizationScore { get; set; }
        public List<string> SuggestedPassages { get; set; } = new();
    }

    private readonly record struct TrainingPlan(string Mode, string Difficulty, int TargetRpm);
    private readonly record struct DailyChallenge(string Title, string Goal, string Reward);
}
