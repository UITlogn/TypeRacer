#!/usr/bin/env python3
"""Offline evidence audit for the NT106 TypeRacer grading rubric.

The goal is not to replace real runtime tests. This script proves that each
rubric item has concrete source/test/documentation evidence before a demo run.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Evidence:
    name: str
    details: str


class Audit:
    def __init__(self) -> None:
        self._passed: list[Evidence] = []
        self._failed: list[str] = []
        self._warnings: list[str] = []

    def pass_(self, name: str, details: str) -> None:
        self._passed.append(Evidence(name, details))
        print(f"[OK] {name}: {details}")

    def fail(self, name: str, details: str) -> None:
        self._failed.append(f"{name}: {details}")
        print(f"[FAIL] {name}: {details}", file=sys.stderr)

    def warn(self, name: str, details: str) -> None:
        self._warnings.append(f"{name}: {details}")
        print(f"[WARN] {name}: {details}")

    def finish(self) -> int:
        print()
        print(f"Rubric evidence checks passed: {len(self._passed)}")
        if self._warnings:
            print(f"Warnings: {len(self._warnings)}")
        if self._failed:
            print(f"Failures: {len(self._failed)}", file=sys.stderr)
            for item in self._failed:
                print(f"- {item}", file=sys.stderr)
            return 1

        print("RUBRIC EVIDENCE AUDIT PASSED")
        return 0


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT))


def read_text(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def paths(pattern: str) -> list[Path]:
    ignored_parts = {"bin", "obj", ".publish", "__pycache__"}
    return sorted(
        path for path in ROOT.glob(pattern)
        if ignored_parts.isdisjoint(path.relative_to(ROOT).parts)
    )


def assert_paths(audit: Audit, name: str, pattern: str, minimum: int = 1) -> list[Path]:
    matches = paths(pattern)
    if len(matches) < minimum:
        audit.fail(name, f"expected >= {minimum} path(s) matching {pattern}, found {len(matches)}")
        return []

    audit.pass_(name, ", ".join(rel(p) for p in matches[:5]) + (" ..." if len(matches) > 5 else ""))
    return matches


def assert_text(audit: Audit, name: str, path: str, needles: list[str]) -> None:
    file_path = ROOT / path
    if not file_path.exists():
        audit.fail(name, f"missing {path}")
        return

    text = read_text(path)
    missing = [needle for needle in needles if needle not in text]
    if missing:
        audit.fail(name, f"{path} missing marker(s): {', '.join(missing)}")
        return

    audit.pass_(name, path)


def assert_any_text(audit: Audit, name: str, pattern: str, needles: list[str]) -> None:
    matches = paths(pattern)
    if not matches:
        audit.fail(name, f"no files match {pattern}")
        return

    combined = "\n".join(path.read_text(encoding="utf-8", errors="ignore") for path in matches)
    missing = [needle for needle in needles if needle not in combined]
    if missing:
        audit.fail(name, f"{pattern} missing marker(s): {', '.join(missing)}")
        return

    audit.pass_(name, f"{len(matches)} file(s) matched {pattern}")


def count_string_lines(path: str) -> int:
    text = read_text(path)
    return len(re.findall(r'^\s*"[^"]+', text, flags=re.MULTILINE))


def main() -> int:
    audit = Audit()

    # 50 points: networking/application logic and core NT106 techniques.
    assert_text(audit, "App/socket protocol", "src/TypeRacer.Shared/Protocol/MessageType.cs",
                ["LOGIN_REQUEST", "CREATE_ROOM", "RACE_START", "TYPING_UPDATE", "GET_AI_COACH"])
    assert_text(audit, "TCP server", "src/TypeRacer.Server/Network/TcpGameServer.cs",
                ["TcpListener", "AcceptTcpClientAsync", "ClientHandler"])
    assert_text(audit, "Binary frame reader", "src/TypeRacer.Shared/Protocol/MessageReader.cs",
                ["ReadExactAsync", "HeaderSize", "MaxMessageSize"])
    assert_text(audit, "Binary frame writer", "src/TypeRacer.Shared/Protocol/MessageSerializer.cs",
                ["WriteInt32BigEndian", "WriteUInt16BigEndian", "FlushAsync"])

    assert_text(audit, "I/O file logger", "src/TypeRacer.Server/Logging/FileLogger.cs",
                ["FileStream", "StreamWriter", "WriteLine"])
    assert_any_text(audit, "Network I/O streams", "src/**/*.cs",
                    ["NetworkStream", "ReadExactAsync", "WriteAsync"])

    assert_paths(audit, "SQL schema scripts", "database/*.sql", minimum=4)
    assert_text(audit, "SQL connection manager", "src/TypeRacer.Server/Data/DatabaseManager.cs",
                ["Microsoft.Data.SqlClient", "SqlConnection"])
    assert_any_text(audit, "SQL repositories", "src/TypeRacer.Server/Data/*Repository.cs",
                    ["SqlCommand", "INSERT INTO", "SELECT"])

    assert_text(audit, "Thread-safe server state", "src/TypeRacer.Server/State/ServerState.cs",
                ["ConcurrentDictionary"])
    assert_text(audit, "Serialized client writes", "src/TypeRacer.Server/State/ConnectedClient.cs",
                ["SemaphoreSlim", "NetworkStream"])
    assert_text(audit, "Race timeout and finish lock", "src/TypeRacer.Server/State/GameRoom.cs",
                ["RaceTimeoutCts", "Interlocked.CompareExchange"])
    assert_text(audit, "Heartbeat background monitor", "src/TypeRacer.Server/Services/HeartbeatService.cs",
                ["Task.Delay", "HeartbeatTimeoutMs", "DISCONNECT"])
    assert_any_text(audit, "AI long request heartbeat grace", "src/TypeRacer.Server/**/*.cs",
                    ["HeartbeatGraceUntil", "GET_AI_COACH"])

    assert_text(audit, "Auth handler", "src/TypeRacer.Server/Handlers/AuthHandler.cs",
                ["LOGIN_REQUEST", "REGISTER_REQUEST", "MessageType.LOGOUT"])
    assert_text(audit, "Auth service", "src/TypeRacer.Server/Services/AuthService.cs",
                ["VerifyStoredPassword", "HashPassword", "GenerateSessionToken"])
    assert_text(audit, "Password hash and tokens", "src/TypeRacer.Shared/Crypto/HashHelper.cs",
                ["Rfc2898DeriveBytes.Pbkdf2", "RandomNumberGenerator", "SessionTokenLength"])

    assert_text(audit, "Multi-client stress proof", "scripts/race_concurrency_test.py",
                ["--include-timeout-case", "overall={'PASS'"])
    assert_paths(audit, "Load balancer project", "src/TypeRacer.LoadBalancer/*.cs", minimum=5)
    assert_text(audit, "Load balancer failover proof", "scripts/load_balancer_probe_test.py",
                ["RoundRobin", "HEALTH_FAILOVER", "LOAD BALANCER PROBE TEST PASSED"])
    assert_text(audit, "Load balancer atomic counters", "src/TypeRacer.LoadBalancer/BackendServer.cs",
                ["Interlocked.Increment", "Interlocked.CompareExchange"])

    assert_text(audit, "AES payload crypto", "src/TypeRacer.Shared/Crypto/AesEncryption.cs",
                ["Aes.Create", "RandomNumberGenerator.GetBytes", "Decrypt"])
    assert_text(audit, "Race report verification hash", "src/TypeRacer.Client/Forms/ResultForm.cs",
                ["SHA256.HashData", "Verification SHA-256", "File.WriteAllText"])
    assert_text(audit, "Structured analytics export", "src/TypeRacer.Client/Forms/ResultForm.cs",
                ["Xuất data", "BuildAnalyticsJson", "BuildAnalyticsCsv", "JsonSerializer.Serialize", "CSV analytics"])
    assert_text(audit, "Share score clipboard card", "src/TypeRacer.Client/Forms/ResultForm.cs",
                ["Copy score", "Clipboard.SetText", "BuildShareScoreCard", "TypeRacer Score Card", "Verify:"])
    assert_text(audit, "Encrypted protocol runtime test", "scripts/encrypted_protocol_test.py",
                ["ENCRYPTED_FLAG", "assert_encrypted_response", "ENCRYPTED PROTOCOL TEST PASSED"])
    assert_text(audit, "Unit test project", "src/TypeRacer.Tests/TypeRacer.Tests.csproj",
                ["Microsoft.NET.Test.Sdk", "xunit", "TypeRacer.Shared.csproj"])
    assert_any_text(audit, "Core unit tests", "src/TypeRacer.Tests/**/*.cs",
                    ["AesEncryptionTests", "ConstantsTests", "MessageSerializerTests", "Encrypt_UsesRandomIvForSamePlaintext"])

    assert_text(audit, "LAN/Internet presets", "src/TypeRacer.Client/Forms/LoginForm.cs",
                ["Internet", "Wi-Fi/LAN", "Tùy chỉnh"])
    assert_text(audit, "VPS deploy path", "scripts/deploy_vps_sql.sh",
                ["VPS_HOST", "AICOACH__OPENAIMODEL", "systemd"])
    assert_text(audit, "VPS demo docs", "docs/vps_sql_deploy.md",
                ["134.209.108.82", "deploy_vps_sql.sh"])

    # 20 points: UI/UX.
    assert_any_text(audit, "Responsive WinForms guards", "src/TypeRacer.Client/Forms/*.cs",
                    ["MinimumSize = new Size", "AutoEllipsis = true"])
    assert_text(audit, "Shared client theme", "src/TypeRacer.Client/Theme/ClientTheme.cs",
                ["AutoScroll = true", "MinimumSize", "AutoEllipsis", "var minHeight = 44"])
    assert_text(audit, "Race track UI", "src/TypeRacer.Client/Controls/ProgressTrack.cs",
                ["IsAiBot", "Skin", "Progress"])
    assert_text(audit, "Live raw WPM and character stats", "src/TypeRacer.Client/Forms/RaceForm.cs",
                ["Raw:", "Ký tự:", "CreateStatLabel", "WrapContents = true"])
    assert_text(audit, "Result performance timeline", "src/TypeRacer.Client/Controls/PerformanceTimelineControl.cs",
                ["TypingPerformanceSample", "Performance timeline", "WPM / Raw / Accuracy", "Goal next race", "DrawGraph"])
    assert_text(audit, "Result certificate card", "src/TypeRacer.Client/Controls/TypingCertificateControl.cs",
                ["TypingCertificateControl", "Diamond Certificate", "Verification:", "SHA256.HashData", "ResolveTier"])
    assert_text(audit, "Race realtime performance sampling", "src/TypeRacer.Client/Forms/RaceForm.cs",
                ["_performanceSamples", "CapturePerformanceSample", "TypingPerformanceSample"])
    assert_text(audit, "AI coach snapshot cards", "src/TypeRacer.Client/Controls/AiCoachSnapshotControl.cs",
                ["AiCoachSnapshotControl", "Daily challenge", "Ghost target", "Mode/RPM", "SetData"])
    assert_text(audit, "AI result panel UI", "src/TypeRacer.Client/Forms/ResultForm.cs",
                ["Ghost", "MistakeHeatmap", "AdaptiveMicroLessons", "NextSessionChecklist",
                 "WeakKeyDrills", "NgramDrills", "SpacedRepetitionPlan", "MasteryCheckpoints",
                 "MistakeFingerprint", "AdaptiveRaceStrategy", "PersonalizationScore", "TrainingPackSignature",
                 "AiConfidenceScore", "PassageNoveltyScore", "WeakspotCoverageScore",
                 "AiEvidenceTrail", "GeneratedPassageAudit", "AI evidence/originality audit",
                 "TypeAI problem-key story", "ProblemKeyStoryPassage",
                 "PracticeMissions", "Chơi mission AI", "KeyboardHeatmapControl", "PerformanceTimelineControl",
                 "AiCoachSnapshotControl", "TypingCertificateControl", "BuildDailyChallengeCard",
                 "DailyChallengeProgressStore", "Daily Challenge Progress", "BuildPersonalBestCard",
                 "PersonalBestProgressStore", "Personal Best Progress",
                 "BuildKeyboardMasteryCard", "KeyboardMasteryProgressStore", "Keyboard Mastery Progress",
                 "Xuất report", "Xuất data", "Copy score", "SaveFileDialog", "Verification SHA-256"])
    assert_text(audit, "Local daily challenge progress", "src/TypeRacer.Client/State/DailyChallengeProgressStore.cs",
                ["daily-challenges.json", "RecordRace", "ProcessedRaceIds", "AccuracyRacesToday",
                 "PodiumsToday", "Daily Diamond"])
    assert_text(audit, "Local personal best progress", "src/TypeRacer.Client/State/PersonalBestProgressStore.cs",
                ["personal-bests.json", "RecordRace", "BestWpm", "BestAccuracy",
                 "BestConsistency", "ProcessedRaceIds"])
    assert_text(audit, "Local keyboard mastery progress", "src/TypeRacer.Client/State/KeyboardMasteryProgressStore.cs",
                ["keyboard-mastery.json", "RecordRace", "MasteredKeyCount", "WeakestKeys",
                 "NeedsReviewKeys", "ProcessedRaceIds"])
    assert_text(audit, "AI weak-key keyboard heatmap UI", "src/TypeRacer.Client/Controls/KeyboardHeatmapControl.cs",
                ["SetWeakKeys", "topMistypedCharacters", "QWERTYUIOP", "AI heatmap", "CreateRoundRect"])
    assert_text(audit, "Practice focus and stop-on-error UI", "src/TypeRacer.Client/Forms/PracticeForm.cs",
                ["Stop lỗi", "Focus mode", "ToggleFocusMode", "ProcessCmdKey", "TxtInput_KeyPress"])
    assert_text(audit, "Lobby warm-up accuracy streak UI", "src/TypeRacer.Client/Forms/RoomForm.cs",
                ["Warm-up phòng chờ", "BuildWarmupCard", "_txtWarmup", "Warm-up accuracy",
                 "Accuracy streak", "CountCorrectPrefix", "CreateScrollablePageHost(page, 840)"])
    assert_text(audit, "Static UI audit", "scripts/ui_layout_static_audit.py",
                ["MinimumSize", "AutoScroll", "button.AutoEllipsis", "WrapContents = true",
                 "Warm-up phòng chờ", "button touch target below 44px"])

    # 10 points: creativity and differentiating AI/gameplay.
    assert_text(audit, "OpenClaude GPT-5.5 integration", "src/TypeRacer.Server/Services/AiCoachService.cs",
                ["openclaude", "gpt-5.5", "BuildPrompt", "ValidateGeneratedCoach"])
    assert_text(audit, "AI prompt quality", "src/TypeRacer.Server/Services/AiCoachService.cs",
                ["Observed mistake samples in volatile memory", "suggested_passages", "ghost_target_wpm",
                 "mistake_heatmap", "adaptive_micro_lessons", "next_session_checklist",
                 "finger_diagnostics", "progress_prediction", "lesson_ladder", "attempt_replay_cues",
                 "weak_key_drills", "ngram_drills", "spaced_repetition_plan", "mastery_checkpoints",
                 "mistake_fingerprint", "adaptive_race_strategy", "personalization_score",
                 "problem_key_story_passage", "problem_key_story_keys", "BuildProblemKeyStoryPassage",
                 "practice_missions", "Problem-Key Story Mission", "BuildPracticeMissions", "BuildTrainingPackSignature",
                 "BuildPassageNoveltyScore", "BuildWeakspotCoverageScore", "BuildAiConfidenceScore"])
    assert_text(audit, "AI response contract", "src/TypeRacer.Shared/Payloads/Ai/AiCoachResponse.cs",
                ["SuggestedPassages", "MistakeHeatmap", "AdaptiveMicroLessons", "GhostRacePlan",
                 "FingerDiagnostics", "ProgressPrediction", "LessonLadder", "AttemptReplayCues",
                 "WeakKeyDrills", "NgramDrills", "TopMistypedNgrams", "SpacedRepetitionPlan", "MasteryCheckpoints",
                 "PracticeMissions", "ProblemKeyStoryPassage", "ProblemKeyStoryKeys",
                 "MistakeFingerprint", "AdaptiveRaceStrategy", "PersonalizationScore",
                 "AiConfidenceScore", "PassageNoveltyScore", "WeakspotCoverageScore",
                 "AiEvidenceTrail", "GeneratedPassageAudit",
                 "TrainingPackSignature", "Provider", "Model", "IsFallback"])
    assert_text(audit, "AI playable mission DTO", "src/TypeRacer.Shared/Payloads/Ai/AiPracticeMissionDto.cs",
                ["Title", "Objective", "DurationSeconds", "TargetWpm", "TargetAccuracy", "Passage", "RewardBadge"])
    assert_text(audit, "Live n-gram mistake memory", "src/TypeRacer.Server/State/GameRoom.cs",
                ["_observedMistakeNgrams", "ExtractNgramsAt", "GetObservedMistakeNgramsSnapshot"])
    assert_text(audit, "Immediate mistake observation", "src/TypeRacer.Server/Handlers/GameHandler.cs",
                ["case MessageType.TYPING_UPDATE", "ObserveMistakes", "RecordRaceResults"])
    assert_text(audit, "Volatile mistake cleanup", "src/TypeRacer.Server/Services/MistakeMemoryService.cs",
                ["ConcurrentDictionary", "ClearUserRoom", "ClearRoom", "PurgeExpired"])
    assert_any_text(audit, "Mistake cleanup call sites", "src/TypeRacer.Server/**/*.cs",
                    ["ClearUserRoom", "ClearRoom"])

    fallback_count = count_string_lines("src/TypeRacer.Server/Services/AiFallbackPassageBank.cs")
    if fallback_count < 100:
        audit.fail("100 fallback passages", f"found only {fallback_count} fallback passage-like strings")
    else:
        audit.pass_("100 fallback passages", f"{fallback_count} passage-like strings in AiFallbackPassageBank")

    assert_text(audit, "AI test requires real provider when needed", "scripts/custom_text_ai_protocol_test.py",
                ["--require-real-ai", "mistake_sample_count", "is_fallback", "ai_confidence_score",
                 "passage_novelty_score", "generated_passage_audit", "problem_key_story_passage"])
    assert_text(audit, "AI edge-case response shape", "scripts/edge_matrix_test.py",
                ["ghost_target_wpm", "mistake_heatmap", "practice_words", "recommended_game_mode",
                 "finger_diagnostics", "progress_prediction", "lesson_ladder", "attempt_replay_cues",
                 "weak_key_drills", "spaced_repetition_plan", "mastery_checkpoints",
                 "mistake_fingerprint", "adaptive_race_strategy", "personalization_score",
                 "ai_confidence_score", "passage_novelty_score", "weakspot_coverage_score",
                 "ai_evidence_trail", "generated_passage_audit", "problem_key_story_passage",
                 "training_pack_signature"])
    assert_text(audit, "Game mode protocol test", "scripts/game_mode_protocol_test.py",
                ["ai_practice", "easy", "nightmare", "GAME MODE PROTOCOL TEST PASSED"])
    assert_text(audit, "Game mode constants", "src/TypeRacer.Shared/Constants.cs",
                ["GameModeAccuracy", "GameModeNoBackspace", "GameModeSuddenDeath", "GameModeAiPractice",
                 "AiPracticeNightmare"])

    # 20 points: teamwork/demo readiness plus packaging.
    assert_text(audit, "Feature research mapping", "docs/typing_site_feature_research_2026-05-25.md",
                ["TypeRacer", "Monkeytype", "10FastFingers", "Nitro Type", "TypingClub", "keyboard mastery"])
    assert_text(audit, "Evaluation playbook", "docs/evaluation_playbook_2026-05-25.md",
                ["Nội dung kiến thức", "Hình thức", "Hiệu quả hợp tác nhóm", "Tư duy sáng tạo"])
    assert_text(audit, "Teamwork evidence", "docs/teamwork_evidence_2026-05-25.md",
                ["Client/UI", "Server/protocol", "DB/deploy/test", "demo"])
    assert_text(audit, "Demo script covers rubric", "scripts/demo_rubric.sh",
                ["unit_tests", "rubric_evidence_audit", "encrypted_protocol_test", "load_balancer_probe_test",
                 "custom_text_ai_protocol_test", "race_concurrency_test"])
    assert_text(audit, "Packaging script", "scripts/package_client_zip.sh",
                ["TypeRacer.Player.All.zip", "publish src/TypeRacer.Client/TypeRacer.Client.csproj", "win-x64"])

    player_zip = ROOT / "TypeRacer.Player.All.zip"
    if player_zip.exists():
        audit.pass_("Local player zip", f"{rel(player_zip)} ({player_zip.stat().st_size // (1024 * 1024)} MB)")
    else:
        audit.warn("Local player zip", "TypeRacer.Player.All.zip is ignored by git; run scripts/package_client_zip.sh before sharing")

    return audit.finish()


if __name__ == "__main__":
    raise SystemExit(main())
