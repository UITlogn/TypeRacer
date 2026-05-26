#!/usr/bin/env python3
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FORMS_DIR = ROOT / "src" / "TypeRacer.Client" / "Forms"
CONTROLS_DIR = ROOT / "src" / "TypeRacer.Client" / "Controls"
THEME_FILE = ROOT / "src" / "TypeRacer.Client" / "Theme" / "ClientTheme.cs"

FIXED_DIALOG_FORMS = {"LoginForm.cs", "RegisterForm.cs"}


def fail(message: str) -> None:
    print(f"[FAIL] {message}", file=sys.stderr)
    raise SystemExit(1)


def assert_theme_guards() -> None:
    text = THEME_FILE.read_text(encoding="utf-8")
    required = [
        "button.AutoEllipsis = true",
        "button.MinimumSize = new Size",
        "var minHeight = 44",
        "CreateScrollablePageHost",
        "AutoScroll = true",
    ]
    for marker in required:
        if marker not in text:
            fail(f"ClientTheme missing layout guard: {marker}")


def assert_form_layout(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    if "public class" not in text or ": Form" not in text:
        return

    if "MinimumSize = new Size" not in text:
        fail(f"{path.name} missing MinimumSize")

    if "FormBorderStyle = FormBorderStyle.FixedDialog" in text and path.name not in FIXED_DIALOG_FORMS:
        fail(f"{path.name} uses FixedDialog without being in the fixed-dialog allowlist")

    if path.name not in FIXED_DIALOG_FORMS and "CreateScrollablePageHost" not in text:
        fail(f"{path.name} should use ClientTheme.CreateScrollablePageHost for small screens")

    assert_common_ui_layout(path, text)

    assert_numeric_controls_fit_fixed_dialog(path, text)

    if path.name == "ResultForm.cs":
        required = [
            "Xuất report",
            "Copy score",
            "SaveFileDialog",
            "File.WriteAllText",
            "Xuất data",
            "BuildAnalyticsJson",
            "BuildAnalyticsCsv",
            "CSV analytics",
            "Clipboard.SetText",
            "SHA256.HashData",
            "Verification SHA-256",
            "WeakKeyDrills",
            "NgramDrills",
            "Adaptive n-gram drills",
            "SpacedRepetitionPlan",
            "MasteryCheckpoints",
            "MistakeFingerprint",
            "AdaptiveRaceStrategy",
            "PersonalizationScore",
            "TrainingPackSignature",
            "AiConfidenceScore",
            "PassageNoveltyScore",
            "WeakspotCoverageScore",
            "AiEvidenceTrail",
            "GeneratedPassageAudit",
            "AI evidence/originality audit",
            "TypeAI problem-key story",
            "ProblemKeyStoryPassage",
            "AI typing fingerprint",
            "Adaptive race strategy",
            "PracticeMissions",
            "AI practice missions",
            "Chơi mission AI",
            "KeyboardHeatmapControl",
            "PerformanceTimelineControl",
            "AiCoachSnapshotControl",
            "TypingCertificateControl",
            "Daily challenge",
            "BuildDailyChallengeCard",
            "DailyChallengeProgressStore",
            "DailyChallengeProgress",
            "Personal best",
            "BuildPersonalBestCard",
            "PersonalBestProgressStore",
            "PersonalBestProgress",
            "Personal Best Progress",
            "Keyboard mastery",
            "BuildKeyboardMasteryCard",
            "KeyboardMasteryProgressStore",
            "KeyboardMasteryProgress",
            "Keyboard Mastery Progress",
            "SetWeakKeys",
            "new FlowLayoutPanel",
            "WrapContents = true",
        ]
        for marker in required:
            if marker not in text:
                fail(f"{path.name} missing report/export UI marker: {marker}")

    if path.name == "RaceForm.cs":
        required = [
            "Raw:",
            "Ký tự:",
            "CreateStatLabel",
            "new FlowLayoutPanel",
            "WrapContents = true",
        ]
        for marker in required:
            if marker not in text:
                fail(f"{path.name} missing live stats/responsive marker: {marker}")

    if path.name == "PracticeForm.cs":
        required = [
            "Stop lỗi",
            "Focus mode",
            "ToggleFocusMode",
            "ProcessCmdKey",
            "TxtInput_KeyPress",
            "_missionDurationSeconds",
            "FormatMissionTarget",
            "WrapContents = true",
        ]
        for marker in required:
            if marker not in text:
                fail(f"{path.name} missing focus/accuracy drill marker: {marker}")

    if path.name == "RoomForm.cs":
        required = [
            "Warm-up phòng chờ",
            "BuildWarmupCard",
            "_txtWarmup",
            "Warm-up accuracy",
            "Accuracy streak",
            "CountCorrectPrefix",
            "CreateScrollablePageHost(page, 940)",
        ]
        for marker in required:
            if marker not in text:
                fail(f"{path.name} missing lobby warm-up/responsive marker: {marker}")


def assert_control_layout(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    if "public class" not in text or (": UserControl" not in text and ": Control" not in text):
        return

    if "BackColor = Color.Transparent" in text and "SupportsTransparentBackColor" not in text:
        fail(f"{path.name} uses transparent BackColor without SupportsTransparentBackColor")

    assert_common_ui_layout(path, text)


def assert_common_ui_layout(path: Path, text: str) -> None:
    for block in re.findall(r"new\s+FlowLayoutPanel\s*\{([^{}]*)\}", text, flags=re.DOTALL):
        if "WrapContents = true" not in block:
            fail(f"{path.name} uses FlowLayoutPanel without WrapContents=true")
        if "AutoScroll = true" not in block:
            fail(f"{path.name} uses FlowLayoutPanel without AutoScroll=true")

    for width, height in re.findall(r"Size\s*=\s*new Size\((\d+),\s*(\d+)\)", text):
        h = int(height)
        if h < 34:
            fail(f"{path.name} has a control Size height below 34px: {width}x{height}")

    for height in re.findall(r"Height\s*=\s*(\d+)", text):
        h = int(height)
        if h < 24:
            fail(f"{path.name} has a control Height below 24px: {height}")

    button_defs = text.count("new Button")
    if button_defs:
        styled = text.count("ClientTheme.StyleButton")
        if styled < button_defs:
            fail(f"{path.name} has unstyled buttons: new Button={button_defs}, StyleButton={styled}")

    for block in re.findall(r"new\s+Button\s*\{([^{}]*)\}", text, flags=re.DOTALL):
        size_match = re.search(r"Size\s*=\s*new Size\(\s*\d+\s*,\s*(\d+)\s*\)", block)
        minimum_size_match = re.search(r"MinimumSize\s*=\s*new Size\(\s*\d+\s*,\s*(\d+)\s*\)", block)
        height_match = re.search(r"Height\s*=\s*(\d+)", block)
        height = (
            int(size_match.group(1))
            if size_match
            else int(minimum_size_match.group(1))
            if minimum_size_match
            else int(height_match.group(1))
            if height_match
            else None
        )
        if height is not None and height < 44:
            fail(f"{path.name} has a button touch target below 44px: height={height}")


def assert_numeric_controls_fit_fixed_dialog(path: Path, text: str) -> None:
    if path.name not in FIXED_DIALOG_FORMS:
        return

    match = re.search(r"MinimumSize\s*=\s*new Size\((\d+),\s*(\d+)\)", text)
    if not match:
        fail(f"{path.name} fixed dialog missing numeric MinimumSize")

    min_width = int(match.group(1))
    min_height = int(match.group(2))
    right_limit = max(1, min_width - 24)
    bottom_limit = max(1, min_height - 64)

    object_blocks = re.findall(r"new\s+\w+\s*\{([^{}]*)\}", text, flags=re.DOTALL)
    for block in object_blocks:
        location = re.search(r"Location\s*=\s*new Point\((\d+),\s*(\d+)\)", block)
        if not location:
            continue

        x = int(location.group(1))
        y = int(location.group(2))
        width_match = re.search(r"Width\s*=\s*(\d+)", block)
        height_match = re.search(r"Height\s*=\s*(\d+)", block)
        size_match = re.search(r"Size\s*=\s*new Size\((\d+),\s*(\d+)\)", block)

        width = int(width_match.group(1)) if width_match else int(size_match.group(1)) if size_match else 0
        height = int(height_match.group(1)) if height_match else int(size_match.group(2)) if size_match else 0

        if width > 0 and x + width > right_limit:
            fail(f"{path.name} fixed dialog control may be clipped horizontally: x={x}, width={width}, limit={right_limit}")

        if height > 0 and y + height > bottom_limit:
            fail(f"{path.name} fixed dialog control may be clipped vertically: y={y}, height={height}, limit={bottom_limit}")


def main() -> int:
    assert_theme_guards()
    form_files = sorted(FORMS_DIR.glob("*.cs"))
    if not form_files:
        fail("No WinForms files found")

    for path in form_files:
        assert_form_layout(path)

    control_files = sorted(CONTROLS_DIR.glob("*.cs"))
    for path in control_files:
        assert_control_layout(path)

    print(f"UI LAYOUT STATIC AUDIT PASSED ({len(form_files)} forms, {len(control_files)} controls)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
