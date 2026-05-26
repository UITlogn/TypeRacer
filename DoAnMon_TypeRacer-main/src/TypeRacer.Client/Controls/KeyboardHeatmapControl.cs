using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using TypeRacer.Client.Theme;

namespace TypeRacer.Client.Controls;

public class KeyboardHeatmapControl : Control
{
    private static readonly string[] KeyRows =
    {
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM",
    };

    private static readonly Regex CountRegex = new(@"\((\d+)", RegexOptions.Compiled);

    private readonly Dictionary<char, int> _weakKeys = new();
    private string _focusSummary = "AI chưa có dữ liệu weak-key.";

    public KeyboardHeatmapControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(480, 108);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    }

    public void SetWeakKeys(IEnumerable<string>? topMistypedCharacters, IEnumerable<string>? topMistypedNgrams)
    {
        _weakKeys.Clear();

        if (topMistypedCharacters != null)
        {
            foreach (var item in topMistypedCharacters)
                AddWeakKey(item);
        }

        if (topMistypedNgrams != null)
        {
            foreach (var ngram in topMistypedNgrams)
                AddNgramWeakKeys(ngram);
        }

        _focusSummary = _weakKeys.Count == 0
            ? "AI chưa có dữ liệu weak-key."
            : $"AI heatmap: {_weakKeys.Count} phím/cụm cần luyện.";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? ClientTheme.Surface);

        var width = Math.Max(1, ClientSize.Width);
        var height = Math.Max(1, ClientSize.Height);
        var titleHeight = Math.Min(24, Math.Max(18, height / 5));
        using var titleBrush = new SolidBrush(ClientTheme.TextMuted);
        e.Graphics.DrawString(_focusSummary, Font, titleBrush, new RectangleF(0, 0, width, titleHeight));

        var keyboardBounds = new RectangleF(0, titleHeight + 2, width, height - titleHeight - 2);
        DrawKeyboard(e.Graphics, keyboardBounds);
    }

    private void DrawKeyboard(Graphics g, RectangleF bounds)
    {
        var maxCount = Math.Max(1, _weakKeys.Values.DefaultIfEmpty(0).Max());
        var rowHeight = Math.Max(24f, (bounds.Height - 8f) / 3f);

        for (var rowIndex = 0; rowIndex < KeyRows.Length; rowIndex++)
        {
            var row = KeyRows[rowIndex];
            var gap = 6f;
            var keyWidth = Math.Max(26f, Math.Min(44f, (bounds.Width - gap * (row.Length - 1)) / 11f));
            var rowWidth = row.Length * keyWidth + (row.Length - 1) * gap;
            var rowOffset = rowIndex switch
            {
                1 => keyWidth * 0.55f,
                2 => keyWidth * 1.25f,
                _ => 0f,
            };
            var startX = bounds.Left + Math.Max(0, (bounds.Width - rowWidth) / 2f) + rowOffset;
            var y = bounds.Top + rowIndex * rowHeight;

            for (var i = 0; i < row.Length; i++)
            {
                var key = row[i];
                var x = startX + i * (keyWidth + gap);
                var rect = new RectangleF(x, y, keyWidth, rowHeight - 6f);
                DrawKey(g, rect, key, maxCount);
            }
        }
    }

    private void DrawKey(Graphics g, RectangleF rect, char key, int maxCount)
    {
        _weakKeys.TryGetValue(char.ToLowerInvariant(key), out var count);
        var intensity = Math.Clamp(count / (float)Math.Max(1, maxCount), 0f, 1f);
        var fill = Blend(
            Color.FromArgb(235, 242, 255),
            Color.FromArgb(255, 110, 96),
            intensity);
        var border = intensity > 0
            ? Color.FromArgb(210, 83, 73)
            : Color.FromArgb(202, 212, 232);
        var textColor = intensity > 0.48f
            ? Color.White
            : Color.FromArgb(34, 47, 74);

        using var path = CreateRoundRect(rect, 8f);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border, intensity > 0 ? 2f : 1f);
        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);

        var label = key.ToString();
        using var textBrush = new SolidBrush(textColor);
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(label, Font, textBrush, rect, format);

        if (count <= 0)
            return;

        using var countBrush = new SolidBrush(Color.FromArgb(245, textColor));
        var countRect = new RectangleF(rect.Right - 15f, rect.Top + 2f, 13f, 12f);
        using var smallFont = new Font("Segoe UI", 6.5f, FontStyle.Bold);
        g.DrawString(Math.Min(9, count).ToString(), smallFont, countBrush, countRect, format);
    }

    private void AddWeakKey(string raw)
    {
        var token = ExtractToken(raw);
        if (token.Length != 1)
            return;

        var key = char.ToLowerInvariant(token[0]);
        if (!char.IsLetter(key))
            return;

        _weakKeys[key] = _weakKeys.GetValueOrDefault(key) + ExtractCount(raw);
    }

    private void AddNgramWeakKeys(string raw)
    {
        var token = ExtractToken(raw);
        if (token.Length < 2)
            return;

        var count = Math.Max(1, ExtractCount(raw) / 2);
        foreach (var ch in token)
        {
            var key = char.ToLowerInvariant(ch);
            if (!char.IsLetter(key))
                continue;

            _weakKeys[key] = _weakKeys.GetValueOrDefault(key) + count;
        }
    }

    private static string ExtractToken(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        var openParen = value.IndexOf('(');
        if (openParen > 0)
            value = value[..openParen].Trim();

        return value;
    }

    private static int ExtractCount(string raw)
    {
        var match = CountRegex.Match(raw ?? string.Empty);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            return Math.Clamp(count, 1, 99);

        return 1;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.A + (to.A - from.A) * amount),
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }

    private static GraphicsPath CreateRoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
