using System.Drawing.Drawing2D;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.Controls;

public sealed record TypingPerformanceSample(double Seconds, double Wpm, double RawWpm, double Accuracy);

public class PerformanceTimelineControl : Control
{
    private readonly List<TypingPerformanceSample> _samples = new();
    private RaceResultDto? _result;
    private int _raceDurationSeconds = Shared.Constants.DefaultRaceDurationSeconds;

    public PerformanceTimelineControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(540, 150);
        Font = new Font("Segoe UI", 9f);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    public void SetData(IEnumerable<TypingPerformanceSample>? samples, RaceResultDto? result, int raceDurationSeconds)
    {
        _samples.Clear();
        if (samples != null)
        {
            _samples.AddRange(samples
                .Where(s => s.Seconds >= 0 && s.Wpm >= 0 && s.RawWpm >= 0 && s.Accuracy >= 0)
                .OrderBy(s => s.Seconds)
                .Take(240));
        }

        _result = result;
        _raceDurationSeconds = Math.Clamp(raceDurationSeconds, 15, Shared.Constants.MaxRaceDurationSeconds);
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
        using (var bg = new LinearGradientBrush(bounds, Color.FromArgb(248, 251, 255), Color.White, LinearGradientMode.Vertical))
        using (var border = new Pen(Color.FromArgb(209, 220, 236), 1f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        DrawHeader(g, bounds);

        var graphWidth = Math.Max(230f, bounds.Width * 0.66f);
        var graphRect = new RectangleF(bounds.Left + 14, bounds.Top + 42, graphWidth - 22, bounds.Height - 56);
        var insightRect = new RectangleF(graphRect.Right + 14, graphRect.Top, bounds.Right - graphRect.Right - 26, graphRect.Height);

        DrawGraph(g, graphRect);
        DrawInsights(g, insightRect);
    }

    private void DrawHeader(Graphics g, RectangleF bounds)
    {
        using var titleBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var subBrush = new SolidBrush(ClientTheme.TextMuted);
        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        g.DrawString("Performance timeline", titleFont, titleBrush, bounds.Left + 14, bounds.Top + 10);

        if (bounds.Width >= 760)
            g.DrawString("WPM / Raw / Accuracy theo nhịp gõ thật", Font, subBrush, bounds.Left + 190, bounds.Top + 12);

        if (bounds.Width < 620)
            return;

        DrawLegend(g, bounds.Right - 248, bounds.Top + 13, Color.FromArgb(37, 99, 235), "WPM");
        DrawLegend(g, bounds.Right - 180, bounds.Top + 13, Color.FromArgb(217, 119, 6), "Raw");
        DrawLegend(g, bounds.Right - 116, bounds.Top + 13, Color.FromArgb(22, 163, 74), "Accuracy");
    }

    private void DrawLegend(Graphics g, float x, float y, Color color, string text)
    {
        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(ClientTheme.TextMuted);
        g.FillEllipse(brush, x, y + 4, 8, 8);
        g.DrawString(text, Font, textBrush, x + 12, y);
    }

    private void DrawGraph(Graphics g, RectangleF rect)
    {
        using var gridPen = new Pen(Color.FromArgb(226, 234, 246), 1f);
        using var axisPen = new Pen(Color.FromArgb(190, 204, 224), 1.2f);
        using var textBrush = new SolidBrush(ClientTheme.TextMuted);

        for (var i = 0; i <= 4; i++)
        {
            var y = rect.Top + rect.Height * i / 4f;
            g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
        }

        g.DrawLine(axisPen, rect.Left, rect.Top, rect.Left, rect.Bottom);
        g.DrawLine(axisPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);

        var samples = GetDrawableSamples();
        var maxValue = Math.Max(100d, samples.Select(s => Math.Max(s.RawWpm, Math.Max(s.Wpm, s.Accuracy))).DefaultIfEmpty(100).Max() * 1.12d);
        DrawLine(g, samples, rect, maxValue, s => s.RawWpm, Color.FromArgb(217, 119, 6));
        DrawLine(g, samples, rect, maxValue, s => s.Wpm, Color.FromArgb(37, 99, 235));
        DrawLine(g, samples, rect, maxValue, s => s.Accuracy, Color.FromArgb(22, 163, 74));

        using var smallFont = new Font("Segoe UI", 8f);
        g.DrawString($"{maxValue:F0}", smallFont, textBrush, rect.Left + 2, rect.Top - 1);
        g.DrawString("0", smallFont, textBrush, rect.Left + 2, rect.Bottom - 15);
        g.DrawString($"0s", smallFont, textBrush, rect.Left, rect.Bottom + 2);
        g.DrawString($"{Math.Max(1, _raceDurationSeconds)}s", smallFont, textBrush, rect.Right - 38, rect.Bottom + 2);

        if (_samples.Count < 2)
        {
            using var fallbackBrush = new SolidBrush(Color.FromArgb(120, ClientTheme.TextMuted));
            g.DrawString("Cần ít nhất 2 mẫu realtime để vẽ đường cong đầy đủ.", smallFont, fallbackBrush,
                new RectangleF(rect.Left + 24, rect.Top + 24, rect.Width - 48, 34));
        }
    }

    private List<TypingPerformanceSample> GetDrawableSamples()
    {
        if (_samples.Count >= 2)
            return _samples;

        if (_result == null)
            return new List<TypingPerformanceSample>
            {
                new(0, 0, 0, 100),
                new(_raceDurationSeconds, 0, 0, 100),
            };

        var elapsedSeconds = Math.Max(1, _result.TimeTakenMs / 1000d);
        var rawWpm = ((_result.CharsCorrect + _result.CharsWrong) / 5d) / (elapsedSeconds / 60d);
        return new List<TypingPerformanceSample>
        {
            new(0, 0, 0, 100),
            new(Math.Min(elapsedSeconds, _raceDurationSeconds), (double)_result.Wpm, rawWpm, (double)_result.Accuracy),
        };
    }

    private void DrawLine(
        Graphics g,
        IReadOnlyList<TypingPerformanceSample> samples,
        RectangleF rect,
        double maxValue,
        Func<TypingPerformanceSample, double> selector,
        Color color)
    {
        if (samples.Count < 2)
            return;

        using var pen = new Pen(color, 2.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        var points = samples
            .Select(s =>
            {
                var x = rect.Left + (float)(Math.Clamp(s.Seconds / Math.Max(1, _raceDurationSeconds), 0, 1) * rect.Width);
                var value = Math.Clamp(selector(s), 0, maxValue);
                var y = rect.Bottom - (float)(value / maxValue * rect.Height);
                return new PointF(x, y);
            })
            .ToArray();

        if (points.Length >= 2)
            g.DrawLines(pen, points);

        using var brush = new SolidBrush(color);
        foreach (var p in points.TakeLast(Math.Min(12, points.Length)))
            g.FillEllipse(brush, p.X - 2.2f, p.Y - 2.2f, 4.4f, 4.4f);
    }

    private void DrawInsights(Graphics g, RectangleF rect)
    {
        if (rect.Width < 150)
            return;

        using var titleBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var textBrush = new SolidBrush(ClientTheme.TextMuted);
        using var titleFont = new Font("Segoe UI", 9.25f, FontStyle.Bold);

        g.DrawString("Goal next race", titleFont, titleBrush, rect.Left, rect.Top);

        var wpm = _result?.Wpm ?? 0m;
        var accuracy = _result?.Accuracy ?? 0m;
        var consistency = _result?.ConsistencyScore ?? 0m;
        var wrong = _result?.CharsWrong ?? 0;
        var total = Math.Max(1, (_result?.CharsCorrect ?? 0) + wrong);
        var errorRate = wrong * 100m / total;
        var targetWpm = Math.Max(20, (int)Math.Ceiling(wpm + 4));
        var targetAccuracy = Math.Min(99.5m, Math.Max(95m, accuracy + 1.2m));

        DrawMetricPill(g, rect.Left, rect.Top + 24, rect.Width, "Target WPM", $"{targetWpm}", (float)Math.Min(1m, wpm / Math.Max(1, targetWpm)), Color.FromArgb(37, 99, 235));
        DrawMetricPill(g, rect.Left, rect.Top + 58, rect.Width, "Accuracy", $"{targetAccuracy:F1}%", (float)Math.Min(1m, accuracy / Math.Max(1, targetAccuracy)), Color.FromArgb(22, 163, 74));
        DrawMetricPill(g, rect.Left, rect.Top + 92, rect.Width, "Stability", $"{consistency:F1}", (float)Math.Min(1m, consistency / 100m), Color.FromArgb(217, 119, 6));

        var advice = errorRate switch
        {
            >= 12m => "AI nên ưu tiên Stop lỗi.",
            >= 6m => "AI nên tăng weak-key drill.",
            _ => "AI có thể tăng RPM.",
        };
        g.DrawString(advice, Font, textBrush, new RectangleF(rect.Left, rect.Bottom - 22, rect.Width, 20));
    }

    private void DrawMetricPill(Graphics g, float x, float y, float width, string label, string value, float progress, Color color)
    {
        var rect = new RectangleF(x, y, width, 26);
        using (var bgPath = CreateRoundRect(rect, 8f))
        using (var bgBrush = new SolidBrush(Color.FromArgb(238, 243, 251)))
        {
            g.FillPath(bgBrush, bgPath);
        }

        var fillRect = new RectangleF(rect.Left, rect.Top, Math.Max(8, rect.Width * Math.Clamp(progress, 0f, 1f)), rect.Height);
        using (var fillPath = CreateRoundRect(fillRect, 8f))
        using (var fillBrush = new SolidBrush(Color.FromArgb(58, color)))
        {
            g.FillPath(fillBrush, fillPath);
        }

        using var labelBrush = new SolidBrush(ClientTheme.TextMuted);
        using var valueBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var smallFont = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        g.DrawString(label, smallFont, labelBrush, rect.Left + 8, rect.Top + 6);

        var valueSize = g.MeasureString(value, smallFont);
        g.DrawString(value, smallFont, valueBrush, rect.Right - valueSize.Width - 8, rect.Top + 6);
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
