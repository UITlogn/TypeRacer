using System.Drawing.Drawing2D;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.Controls;

/// <summary>
/// TypeRacer-like track control.
/// Draws lanes with car skins and smooth progress animation from 0% to 100%.
/// </summary>
public class ProgressTrack : Control
{
    private sealed class LaneState
    {
        public int UserId;
        public string Username = string.Empty;
        public double DisplayProgress;
        public double TargetProgress;
        public double Wpm;
        public bool IsFinished;
        public bool IsAiBot;
        public int SkinIndex;
    }

    private readonly Dictionary<int, LaneState> _lanes = new();
    private readonly List<int> _laneOrder = new();
    private readonly System.Windows.Forms.Timer _animTimer;

    private readonly Font _nameFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _metaFont = new("Segoe UI", 9f);
    private readonly Font _pctFont = new("Segoe UI", 8.5f, FontStyle.Bold);

    private static readonly CarSkin[] Skins =
    {
        new CarSkin(Color.FromArgb(228, 87, 46),  Color.FromArgb(249, 194, 47), Color.FromArgb(53, 53, 53), CarStyle.Classic),
        new CarSkin(Color.FromArgb(41, 128, 185), Color.FromArgb(133, 193, 233), Color.FromArgb(45, 45, 45), CarStyle.Sport),
        new CarSkin(Color.FromArgb(39, 174, 96),  Color.FromArgb(174, 214, 84),  Color.FromArgb(40, 40, 40), CarStyle.Hatchback),
        new CarSkin(Color.FromArgb(142, 68, 173), Color.FromArgb(214, 162, 232), Color.FromArgb(50, 50, 50), CarStyle.Sedan),
        new CarSkin(Color.FromArgb(241, 196, 15), Color.FromArgb(252, 243, 207), Color.FromArgb(58, 58, 58), CarStyle.Muscle),
    };

    public ProgressTrack()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(250, 251, 255);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer, true);

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += AnimTimer_Tick;
    }

    /// <summary>Update player progress targets. Movement is smoothed by animation timer.</summary>
    public void UpdatePlayers(List<PlayerProgressDto> players)
    {
        var seen = new HashSet<int>();

        // Keep lane order from server update ordering.
        _laneOrder.Clear();

        foreach (var p in players)
        {
            seen.Add(p.UserId);
            _laneOrder.Add(p.UserId);

            if (!_lanes.TryGetValue(p.UserId, out var lane))
            {
                lane = new LaneState
                {
                    UserId = p.UserId,
                    Username = p.Username,
                    DisplayProgress = Clamp01(p.Progress),
                    TargetProgress = Clamp01(p.Progress),
                    SkinIndex = ResolveSkinIndex(p.UserId, p.Username),
                };
                _lanes[p.UserId] = lane;
            }

            lane.Username = p.Username;
            lane.TargetProgress = Clamp01(p.Progress);
            lane.Wpm = Math.Max(0, p.Wpm);
            lane.IsFinished = p.IsFinished;
            lane.IsAiBot = p.IsAiBot;
            if (lane.IsFinished)
                lane.TargetProgress = 1.0;
        }

        // Remove stale lanes.
        var removed = _lanes.Keys.Where(id => !seen.Contains(id)).ToList();
        foreach (var id in removed)
            _lanes.Remove(id);

        if (_lanes.Count == 0)
        {
            _animTimer.Stop();
            Invalidate();
            return;
        }

        if (!_animTimer.Enabled)
            _animTimer.Start();

        Invalidate();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        var hasMotion = false;

        foreach (var lane in _lanes.Values)
        {
            var delta = lane.TargetProgress - lane.DisplayProgress;
            if (Math.Abs(delta) < 0.0005)
            {
                lane.DisplayProgress = lane.TargetProgress;
                continue;
            }

            // Exponential smoothing + minimum step for visibly smooth motion.
            var step = delta * 0.22;
            if (Math.Abs(step) < 0.0025)
                step = Math.Sign(delta) * 0.0025;

            lane.DisplayProgress = Clamp01(lane.DisplayProgress + step);
            hasMotion = true;
        }

        Invalidate();

        if (!hasMotion)
            _animTimer.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_lanes.Count == 0)
            return;

        var laneCount = _laneOrder.Count;
        if (laneCount <= 0)
            return;

        const int outerMargin = 10;
        const int laneGap = 10;
        const int nameColWidth = 190;

        var availableHeight = Height - outerMargin * 2 - (laneGap * (laneCount - 1));
        var laneHeight = Math.Max(44, availableHeight / laneCount);

        for (var i = 0; i < laneCount; i++)
        {
            var userId = _laneOrder[i];
            if (!_lanes.TryGetValue(userId, out var lane))
                continue;

            var y = outerMargin + i * (laneHeight + laneGap);
            var laneRect = new Rectangle(outerMargin, y, Math.Max(100, Width - outerMargin * 2), laneHeight);
            DrawLane(g, laneRect, nameColWidth, lane, i);
        }
    }

    private void DrawLane(Graphics g, Rectangle laneRect, int nameColWidth, LaneState lane, int laneIndex)
    {
        var skin = Skins[lane.SkinIndex % Skins.Length];

        // Lane card background.
        using (var path = RoundedRect(laneRect, 10))
        using (var laneBrush = new LinearGradientBrush(laneRect,
                   Color.FromArgb(255, 255, 255),
                   Color.FromArgb(244, 248, 255),
                   LinearGradientMode.Vertical))
        using (var borderPen = new Pen(Color.FromArgb(216, 226, 241), 1f))
        {
            g.FillPath(laneBrush, path);
            g.DrawPath(borderPen, path);
        }

        // Left meta area.
        var metaRect = new Rectangle(laneRect.Left + 10, laneRect.Top + 6, Math.Max(140, nameColWidth - 16), laneRect.Height - 12);
        var username = lane.Username;
        if (username.Length > 18)
            username = username[..16] + "..";

        using var textBrush = new SolidBrush(Color.FromArgb(43, 53, 74));
        g.DrawString(username, _nameFont, textBrush, metaRect.Left, metaRect.Top + 1);
        g.DrawString($"{lane.Wpm:F0} WPM", _metaFont, Brushes.DimGray, metaRect.Left, metaRect.Top + 22);

        // Track area.
        var trackRect = new Rectangle(
            laneRect.Left + nameColWidth,
            laneRect.Top + 9,
            Math.Max(80, laneRect.Width - nameColWidth - 12),
            laneRect.Height - 18);

        using (var trackPath = RoundedRect(trackRect, 8))
        using (var trackBrush = new SolidBrush(Color.FromArgb(235, 240, 249)))
        using (var trackPen = new Pen(Color.FromArgb(206, 216, 232), 1f))
        {
            g.FillPath(trackBrush, trackPath);
            g.DrawPath(trackPen, trackPath);
        }

        // Lane stripes.
        using (var stripePen = new Pen(Color.FromArgb(225, 231, 244), 1f))
        {
            stripePen.DashStyle = DashStyle.Dash;
            for (var i = 1; i < 10; i++)
            {
                var x = trackRect.Left + (trackRect.Width * i / 10f);
                g.DrawLine(stripePen, x, trackRect.Top + 2, x, trackRect.Bottom - 2);
            }
        }

        // Finish line.
        var finishX = trackRect.Right - 16;
        DrawFinishLine(g, finishX, trackRect.Top, trackRect.Bottom);

        // Car position.
        var carMinX = trackRect.Left + 10;
        var carMaxX = finishX - 18;
        var carX = carMinX + (carMaxX - carMinX) * (float)Clamp01(lane.DisplayProgress);
        var carY = trackRect.Top + (trackRect.Height - 20) / 2;
        DrawCar(g, new PointF(carX, carY), skin, lane.IsFinished);

        // Progress badge.
        var pct = $"{lane.DisplayProgress * 100:F0}%";
        var pctSize = g.MeasureString(pct, _pctFont);
        var badgeX = Math.Min(trackRect.Right - pctSize.Width - 8, carX + 24);
        var badgeY = trackRect.Top - 1;
        using (var pctBrush = new SolidBrush(SystemColors.ControlText))
        {
            g.DrawString(pct, _pctFont, pctBrush, badgeX, badgeY);
        }

        if (lane.IsFinished)
        {
            g.DrawString("FINISH", _pctFont, Brushes.DarkGreen, trackRect.Right - 58, trackRect.Top - 1);
        }

        // Tiny skin label to make skin differentiation explicit.
        var skinLabel = lane.IsAiBot ? "AI Bot" : $"Skin {lane.SkinIndex + 1}";
        var skinSize = g.MeasureString(skinLabel, _pctFont);
        g.DrawString(skinLabel, _pctFont, Brushes.Gray,
            laneRect.Right - skinSize.Width - 10,
            laneRect.Bottom - skinSize.Height - 4);
    }

    private static void DrawFinishLine(Graphics g, int x, int top, int bottom)
    {
        using var linePen = new Pen(Color.FromArgb(40, 40, 40), 2f);
        g.DrawLine(linePen, x, top + 1, x, bottom - 1);

        const int cell = 4;
        var rows = Math.Max(1, (bottom - top) / cell);
        for (var r = 0; r < rows; r++)
        {
            var isBlack = r % 2 == 0;
            using var brush = new SolidBrush(isBlack ? Color.Black : Color.White);
            g.FillRectangle(brush, x + 2, top + r * cell, cell, cell);
            g.FillRectangle(brush, x + 2 + cell, top + (r + 1) * cell, cell, cell);
        }
    }

    private static void DrawCar(Graphics g, PointF pos, CarSkin skin, bool finished)
    {
        var x = pos.X;
        var y = pos.Y;

        using var bodyBrush = new SolidBrush(skin.BodyColor);
        using var roofBrush = new SolidBrush(skin.RoofColor);
        using var wheelBrush = new SolidBrush(skin.WheelColor);
        using var glassBrush = new SolidBrush(Color.FromArgb(205, 235, 255));
        using var outlinePen = new Pen(Color.FromArgb(65, 65, 65), 1f);

        // Wheels.
        g.FillEllipse(wheelBrush, x + 5, y + 14, 7, 7);
        g.FillEllipse(wheelBrush, x + 25, y + 14, 7, 7);

        // Body styles.
        switch (skin.Style)
        {
            case CarStyle.Sport:
                g.FillRoundedRect(bodyBrush, x + 1, y + 8, 34, 9, 4);
                g.FillRoundedRect(roofBrush, x + 10, y + 4, 15, 7, 4);
                break;
            case CarStyle.Hatchback:
                g.FillRoundedRect(bodyBrush, x + 2, y + 8, 33, 9, 4);
                g.FillRoundedRect(roofBrush, x + 12, y + 4, 13, 8, 3);
                break;
            case CarStyle.Sedan:
                g.FillRoundedRect(bodyBrush, x + 1, y + 8, 34, 9, 4);
                g.FillRoundedRect(roofBrush, x + 9, y + 4, 17, 7, 3);
                break;
            case CarStyle.Muscle:
                g.FillRoundedRect(bodyBrush, x + 1, y + 8, 34, 9, 3);
                g.FillRoundedRect(roofBrush, x + 11, y + 5, 14, 6, 2);
                g.FillRectangle(Brushes.WhiteSmoke, x + 6, y + 9, 3, 2);
                break;
            default:
                g.FillRoundedRect(bodyBrush, x + 1, y + 8, 34, 9, 4);
                g.FillRoundedRect(roofBrush, x + 10, y + 4, 15, 7, 4);
                break;
        }

        // Windows.
        g.FillRectangle(glassBrush, x + 13, y + 6, 5, 3);
        g.FillRectangle(glassBrush, x + 19, y + 6, 5, 3);

        // Outline.
        g.DrawRoundedRect(outlinePen, x + 1, y + 8, 34, 9, 4);

        if (finished)
        {
            using var doneBrush = new SolidBrush(Color.FromArgb(27, 153, 72));
            g.FillEllipse(doneBrush, x + 33, y + 1, 7, 7);
        }
    }

    public void Reset()
    {
        _lanes.Clear();
        _laneOrder.Clear();
        _animTimer.Stop();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Stop();
            _animTimer.Dispose();
            _nameFont.Dispose();
            _metaFont.Dispose();
            _pctFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static int ResolveSkinIndex(int userId, string username)
    {
        var seed = HashCode.Combine(userId, username ?? string.Empty);
        if (seed == int.MinValue) seed = 0;
        return Math.Abs(seed) % Skins.Length;
    }

    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;

        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private readonly record struct CarSkin(Color BodyColor, Color RoofColor, Color WheelColor, CarStyle Style);

    private enum CarStyle
    {
        Classic,
        Sport,
        Hatchback,
        Sedan,
        Muscle,
    }
}

internal static class GraphicsCarExtensions
{
    public static void FillRoundedRect(this Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = RoundedRectF(x, y, width, height, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRect(this Graphics g, Pen pen, float x, float y, float width, float height, float radius)
    {
        using var path = RoundedRectF(x, y, width, height, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectF(float x, float y, float width, float height, float radius)
    {
        var d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
