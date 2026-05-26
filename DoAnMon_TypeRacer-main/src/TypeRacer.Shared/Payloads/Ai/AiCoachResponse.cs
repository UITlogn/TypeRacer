namespace TypeRacer.Shared.Payloads.Ai;

public class AiCoachResponse
{
    public bool Success { get; set; }
    public string CoachText { get; set; } = string.Empty;
    public List<string> Tips { get; set; } = new();
    public List<string> ActionPlan { get; set; } = new();
    public List<string> PersonalizedDrills { get; set; } = new();
    public List<string> SuggestedPassages { get; set; } = new();
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
    public List<string> TopMistypedCharacters { get; set; } = new();
    public List<string> TopMistypedWords { get; set; } = new();
    public List<string> TopMistypedNgrams { get; set; } = new();
    public decimal MistakeDensity { get; set; }
    public decimal PersonalizationScore { get; set; }
    public decimal AiConfidenceScore { get; set; }
    public decimal PassageNoveltyScore { get; set; }
    public decimal WeakspotCoverageScore { get; set; }
    public string TrainingPackSignature { get; set; } = string.Empty;
    public List<string> AiEvidenceTrail { get; set; } = new();
    public List<string> GeneratedPassageAudit { get; set; } = new();
    public string FocusArea { get; set; } = string.Empty;
    public string SkillTier { get; set; } = string.Empty;
    public decimal EstimatedNextWpm { get; set; }
    public int RecentRaceCount { get; set; }
    public int RecentCompletedCount { get; set; }
    public decimal RecentWpmTrend { get; set; }
    public decimal RecentAccuracyTrend { get; set; }
    public int MistakeSampleCount { get; set; }
    public int GeneratedPassageCount { get; set; }
    public string TrainingTitle { get; set; } = string.Empty;
    public string RecommendedGameMode { get; set; } = string.Empty;
    public string RecommendedDifficulty { get; set; } = string.Empty;
    public int RecommendedTargetRpm { get; set; }
    public string DailyChallengeTitle { get; set; } = string.Empty;
    public string DailyChallengeGoal { get; set; } = string.Empty;
    public string DailyChallengeReward { get; set; } = string.Empty;
    public decimal GhostTargetWpm { get; set; }
    public decimal GhostTargetAccuracy { get; set; }
    public string GhostRewardBadge { get; set; } = string.Empty;
    public string Provider { get; set; } = "heuristic";
    public string Model { get; set; } = "local-rules-v1";
    public bool IsFallback { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int RaceId { get; set; }
    public int UserId { get; set; }
}
