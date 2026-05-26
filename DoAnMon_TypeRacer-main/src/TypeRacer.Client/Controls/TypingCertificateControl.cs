using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;

namespace TypeRacer.Client.Controls;

public class TypingCertificateControl : Control
{
    private string _tier = "Practice Certificate";
    private string _summary = "Hoàn thành một race để tạo chứng nhận.";
    private string _proof = "Verification: pending";
    private string _rank = "-";
    private string _time = "-";
    private decimal _wpm;
    private decimal _accuracy;
    private Color _accent = Color.FromArgb(37, 99, 235);

    public TypingCertificateControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(540, 122);
        Font = new Font("Segoe UI", 9f);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
    }

    public void SetData(RaceResultDto? result, string roomCode, int raceId, int totalPlayers)
    {
        if (result == null)
        {
            Invalidate();
            return;
        }

        _wpm = result.Wpm;
        _accuracy = result.Accuracy;
        _rank = $"#{result.Position}/{Math.Max(1, totalPlayers)}";
        _time = FormatTime(result.TimeTakenMs);
        (_tier, _accent) = ResolveTier(result.Wpm, result.Accuracy, result.IsCompleted, result.IsDisqualified);
        _summary = BuildSummary(result);
        _proof = BuildVerification(roomCode, raceId, result);
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
        using (var bg = new LinearGradientBrush(bounds, Color.White, Color.FromArgb(245, 249, 255), LinearGradientMode.Vertical))
        using (var border = new Pen(Color.FromArgb(208, 220, 236), 1f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        using (var accentBrush = new SolidBrush(_accent))
        {
            g.FillRectangle(accentBrush, bounds.Left, bounds.Top, 6, bounds.Height);
        }

        var inner = RectangleF.Inflate(bounds, -18, -14);
        DrawCertificateHeader(g, inner);
        DrawMetrics(g, inner);
        DrawProof(g, inner);
    }

    private void DrawCertificateHeader(Graphics g, RectangleF inner)
    {
        var titleRect = new RectangleF(inner.Left, inner.Top, Math.Max(220, inner.Width * 0.42f), inner.Height);
        using var labelBrush = new SolidBrush(ClientTheme.TextMuted);
        using var titleBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var titleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 9f);
        using var lineLimit = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit };

        g.DrawString("Typing certificate", labelFont, labelBrush, titleRect.Left, titleRect.Top);
        g.DrawString(_tier, titleFont, titleBrush, new RectangleF(titleRect.Left, titleRect.Top + 20, titleRect.Width, 26), lineLimit);
        g.DrawString(_summary, bodyFont, labelBrush, new RectangleF(titleRect.Left, titleRect.Top + 52, titleRect.Width, 40), lineLimit);
    }

    private void DrawMetrics(Graphics g, RectangleF inner)
    {
        var rightStart = inner.Left + Math.Max(250, inner.Width * 0.45f);
        var available = inner.Right - rightStart;
        if (available < 250)
            return;

        var chipGap = 8f;
        var chipWidth = (available - chipGap * 3f) / 4f;
        var y = inner.Top + 10;
        DrawMetricChip(g, new RectangleF(rightStart, y, chipWidth, 54), "WPM", $"{_wpm:F1}", Color.FromArgb(37, 99, 235));
        DrawMetricChip(g, new RectangleF(rightStart + (chipWidth + chipGap), y, chipWidth, 54), "Accuracy", $"{_accuracy:F1}%", Color.FromArgb(22, 163, 74));
        DrawMetricChip(g, new RectangleF(rightStart + (chipWidth + chipGap) * 2f, y, chipWidth, 54), "Rank", _rank, Color.FromArgb(217, 119, 6));
        DrawMetricChip(g, new RectangleF(rightStart + (chipWidth + chipGap) * 3f, y, chipWidth, 54), "Time", _time, Color.FromArgb(124, 58, 237));
    }

    private void DrawMetricChip(Graphics g, RectangleF rect, string label, string value, Color accent)
    {
        using (var path = CreateRoundRect(rect, 8f))
        using (var bg = new SolidBrush(Color.FromArgb(240, 245, 252)))
        {
            g.FillPath(bg, path);
        }

        using var accentBrush = new SolidBrush(Color.FromArgb(54, accent));
        using var labelBrush = new SolidBrush(ClientTheme.TextMuted);
        using var valueBrush = new SolidBrush(ClientTheme.TextPrimary);
        using var labelFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var valueFont = new Font("Segoe UI", 11f, FontStyle.Bold);

        g.FillRectangle(accentBrush, rect.Left, rect.Top, rect.Width, 4);
        g.DrawString(label, labelFont, labelBrush, rect.Left + 8, rect.Top + 10);
        g.DrawString(value, valueFont, valueBrush, new RectangleF(rect.Left + 8, rect.Top + 28, rect.Width - 16, 20));
    }

    private void DrawProof(Graphics g, RectangleF inner)
    {
        using var proofBrush = new SolidBrush(ClientTheme.TextMuted);
        using var proofFont = new Font("Consolas", 8.3f, FontStyle.Bold);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Far,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(_proof, proofFont, proofBrush, inner, format);
    }

    private static (string Tier, Color Accent) ResolveTier(decimal wpm, decimal accuracy, bool completed, bool disqualified)
    {
        if (!completed || disqualified || accuracy < 90m)
            return ("Practice Run", Color.FromArgb(100, 116, 139));
        if (wpm >= 90m && accuracy >= 98m)
            return ("Diamond Certificate", Color.FromArgb(37, 99, 235));
        if (wpm >= 70m && accuracy >= 97m)
            return ("Platinum Certificate", Color.FromArgb(124, 58, 237));
        if (wpm >= 50m && accuracy >= 95m)
            return ("Gold Certificate", Color.FromArgb(217, 119, 6));
        if (wpm >= 35m && accuracy >= 93m)
            return ("Silver Certificate", Color.FromArgb(71, 85, 105));
        return ("Bronze Certificate", Color.FromArgb(180, 83, 9));
    }

    private static string BuildSummary(RaceResultDto result)
    {
        if (result.IsDisqualified)
            return "Không cấp chứng nhận vì người chơi bị loại bởi luật mode.";
        if (!result.IsCompleted)
            return "Bài test chưa hoàn thành, vẫn dùng được để luyện tiếp.";
        return "Chứng nhận tốc độ/chính xác có mã xác minh cho report demo.";
    }

    private static string BuildVerification(string roomCode, int raceId, RaceResultDto result)
    {
        var raw = $"{roomCode}|{raceId}|{result.UserId}|{result.Username}|{result.Wpm:F1}|{result.Accuracy:F1}|{result.TimeTakenMs}|{result.CharsCorrect}|{result.CharsWrong}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
        return $"Verification: {hash}";
    }

    private static string FormatTime(int timeTakenMs)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, timeTakenMs));
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
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
