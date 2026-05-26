using System.Drawing.Drawing2D;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Ai;

namespace TypeRacer.Client.Controls;

public class AiCoachSnapshotControl : Control
{
    private string _trainingTitle = "Chưa có giáo án AI";
    private string _dailyChallenge = "Bấm Phân tích AI để tạo daily challenge.";
    private string _ghostTarget = "Ghost target sẽ hiện sau khi AI phản hồi.";
    private string _modeTarget = "Mode/RPM sẽ được cá nhân hóa từ lỗi thật.";
    private string _rewardBadge = "AI reward badge";
    private float _wpmProgress;
    private float _accuracyProgress;
    private bool _isLoading;

    public AiCoachSnapshotControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(520, 108);
        Font = new Font("Segoe UI", 9f);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    public void Clear()
    {
        _isLoading = false;
        _trainingTitle = "Chưa có giáo án AI";
        _dailyChallenge = "Bấm Phân tích AI để tạo daily challenge.";
        _ghostTarget = "Ghost target sẽ hiện sau khi AI phản hồi.";
        _modeTarget = "Mode/RPM sẽ được cá nhân hóa từ lỗi thật.";
        _rewardBadge = "AI reward badge";
        _wpmProgress = 0f;
        _accuracyProgress = 0f;
        Invalidate();
    }

    public void SetLoading()
    {
        _isLoading = true;
        _trainingTitle = "AI đang đọc lỗi thật...";
        _dailyChallenge = "Đang sinh daily challenge từ mistake memory.";
        _ghostTarget = "Đang tính ghost target WPM/accuracy.";
        _modeTarget = "Đang chọn mode, cấp AI và RPM mục tiêu.";
        _rewardBadge = "Đang tạo badge";
        _wpmProgress = 0.35f;
        _accuracyProgress = 0.5f;
        Invalidate();
    }

    public void SetError(string message)
    {
        _isLoading = false;
        _trainingTitle = "AI snapshot chưa sẵn sàng";
        _dailyChallenge = string.IsNullOrWhiteSpace(message) ? "Không nhận được phản hồi AI hợp lệ." : message;
        _ghostTarget = "Có thể bấm Phân tích AI lại.";
        _modeTarget = "Fallback passage bank vẫn có thể dùng để luyện.";
        _rewardBadge = "Retry AI";
        _wpmProgress = 0f;
        _accuracyProgress = 0f;
        Invalidate();
    }

    public void SetData(AiCoachResponse payload, RaceResultDto? result)
    {
        _isLoading = false;
        _trainingTitle = Pick(payload.TrainingTitle, payload.FocusArea, "AI personal training plan");
        _dailyChallenge = BuildDailyChallenge(payload);
        _ghostTarget = BuildGhostTarget(payload, result);
        _modeTarget = BuildModeTarget(payload);
        _rewardBadge = BuildRewardBadge(payload);

        var currentWpm = result?.Wpm ?? 0m;
        var targetWpm = payload.GhostTargetWpm > 0 ? payload.GhostTargetWpm : currentWpm + 4m;
        _wpmProgress = targetWpm > 0 ? (float)Math.Clamp(currentWpm / targetWpm, 0m, 1m) : 0f;

        var currentAccuracy = result?.Accuracy ?? 0m;
        var targetAccuracy = payload.GhostTargetAccuracy > 0 ? payload.GhostTargetAccuracy : Math.Min(99.5m, currentAccuracy + 1.2m);
        _accuracyProgress = targetAccuracy > 0 ? (float)Math.Clamp(currentAccuracy / targetAccuracy, 0m, 1m) : 0f;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? ClientTheme.Surface);

        var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = CreateRoundRect(bounds, 10f))
        using (var bg = new LinearGradientBrush(bounds, Color.FromArgb(249, 252, 255), Color.White, LinearGradientMode.Vertical))
        using (var border = new Pen(Color.FromArgb(209, 220, 236), 1f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        var gap = 8f;
        var inner = RectangleF.Inflate(bounds, -10f, -10f);
        var cardWidth = inner.Width >= 760
            ? (inner.Width - gap * 3f) / 4f
            : (inner.Width - gap) / 2f;
        var cardHeight = inner.Width >= 760
            ? inner.Height
            : (inner.Height - gap) / 2f;

        DrawSnapshotCard(g, new RectangleF(inner.Left, inner.Top, cardWidth, cardHeight),
            "Giáo án AI", _trainingTitle, Color.FromArgb(37, 99, 235), _isLoading ? 0.45f : 0.85f);
        DrawSnapshotCard(g, new RectangleF(inner.Left + cardWidth + gap, inner.Top, cardWidth, cardHeight),
            "Daily challenge", _dailyChallenge, Color.FromArgb(22, 163, 74), _accuracyProgress);

        var secondRowY = inner.Width >= 760 ? inner.Top : inner.Top + cardHeight + gap;
        var thirdX = inner.Width >= 760 ? inner.Left + (cardWidth + gap) * 2f : inner.Left;
        var fourthX = inner.Width >= 760 ? inner.Left + (cardWidth + gap) * 3f : inner.Left + cardWidth + gap;

        DrawSnapshotCard(g, new RectangleF(thirdX, secondRowY, cardWidth, cardHeight),
            "Ghost target", _ghostTarget, Color.FromArgb(217, 119, 6), _wpmProgress);
        DrawSnapshotCard(g, new RectangleF(fourthX, secondRowY, cardWidth, cardHeight),
            "Mode/RPM", $"{_modeTarget} | {_rewardBadge}", Color.FromArgb(124, 58, 237), Math.Max(_wpmProgress, _accuracyProgress));
    }

    private void DrawSnapshotCard(Graphics g, RectangleF rect, string label, string value, Color accent, float progress)
    {
        using (var path = CreateRoundRect(rect, 8f))
        using (var bg = new SolidBrush(Color.FromArgb(244, 248, 253)))
        using (var border = new Pen(Color.FromArgb(218, 228, 241), 1f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        var barRect = new RectangleF(rect.Left, rect.Top, Math.Max(8f, rect.Width * Math.Clamp(progress, 0f, 1f)), 4f);
        using (var barPath = CreateRoundRect(barRect, 4f))
        using (var barBrush = new SolidBrush(accent))
        {
            g.FillPath(barBrush, barPath);
        }

        using var labelBrush = new SolidBrush(Color.FromArgb(83, 96, 118));
        using var valueBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var labelFont = new Font("Segoe UI", 8.2f, FontStyle.Bold);
        using var valueFont = new Font("Segoe UI", rect.Height < 50 ? 8.3f : 9f, FontStyle.Bold);
        using var labelFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var valueFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.LineLimit,
        };

        g.DrawString(label, labelFont, labelBrush, new RectangleF(rect.Left + 9, rect.Top + 8, rect.Width - 18, 16), labelFormat);
        g.DrawString(value, valueFont, valueBrush, new RectangleF(rect.Left + 9, rect.Top + 28, rect.Width - 18, rect.Height - 34), valueFormat);
    }

    private static string BuildDailyChallenge(AiCoachResponse payload)
    {
        var title = Pick(payload.DailyChallengeTitle, "Daily challenge");
        var goal = Pick(payload.DailyChallengeGoal, payload.PracticeWords.FirstOrDefault(), "Luyện 3 lượt theo bài AI.");
        return $"{title}: {goal}";
    }

    private static string BuildGhostTarget(AiCoachResponse payload, RaceResultDto? result)
    {
        var wpm = payload.GhostTargetWpm > 0 ? payload.GhostTargetWpm : (result?.Wpm ?? 0m) + 4m;
        var accuracy = payload.GhostTargetAccuracy > 0 ? payload.GhostTargetAccuracy : Math.Min(99.5m, (result?.Accuracy ?? 94m) + 1.2m);
        return $"{wpm:F1} WPM / {accuracy:F1}% accuracy";
    }

    private static string BuildModeTarget(AiCoachResponse payload)
    {
        var mode = Pick(payload.RecommendedGameMode, "AI Practice");
        var difficulty = Pick(payload.RecommendedDifficulty, "adaptive");
        var rpm = payload.RecommendedTargetRpm > 0 ? $"{payload.RecommendedTargetRpm} RPM" : "auto RPM";
        return $"{mode} - {difficulty} - {rpm}";
    }

    private static string BuildRewardBadge(AiCoachResponse payload)
    {
        var badge = Pick(payload.GhostRewardBadge, payload.DailyChallengeReward, "AI Mission Clear");
        if (payload.PersonalizationScore > 0)
            return $"{badge} | DNA {payload.PersonalizationScore:F0}/100";

        if (!string.IsNullOrWhiteSpace(payload.TrainingPackSignature))
            return $"{badge} | {payload.TrainingPackSignature}";

        return badge;
    }

    private static string Pick(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static GraphicsPath CreateRoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
