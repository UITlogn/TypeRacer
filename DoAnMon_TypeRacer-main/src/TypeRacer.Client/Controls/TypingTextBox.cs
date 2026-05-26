using System.Text;

namespace TypeRacer.Client.Controls;

/// <summary>
/// Custom control hiển thị đoạn văn cần gõ.
/// Ký tự đúng → xanh lá, ký tự sai → đỏ, chưa gõ → xám.
/// </summary>
public class TypingTextBox : Control
{
    private string _passageText = string.Empty;
    private string _typedText = string.Empty;
    private int _correctCount;
    private int _wrongCount;

    // Font hiển thị
    private readonly Font _textFont = new("Consolas", 14f, FontStyle.Regular);
    private readonly StringFormat _textFormat;

    public string PassageText
    {
        get => _passageText;
        set
        {
            _passageText = NormalizeUnicode(value);
            UpdateCounts();
            Invalidate();
        }
    }

    public string TypedText
    {
        get => _typedText;
        set { _typedText = NormalizeUnicode(value); UpdateCounts(); Invalidate(); }
    }

    public int CorrectCount => _correctCount;
    public int WrongCount => _wrongCount;
    public int CurrentPosition => _typedText.Length;

    public static string NormalizeForTyping(string? value) => NormalizeUnicode(value);

    public TypingTextBox()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        _textFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        _textFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
    }

    /// <summary>Đếm số ký tự đúng/sai</summary>
    private void UpdateCounts()
    {
        _correctCount = 0;
        _wrongCount = 0;

        for (int i = 0; i < _typedText.Length && i < _passageText.Length; i++)
        {
            if (_typedText[i] == _passageText[i])
                _correctCount++;
            else
                _wrongCount++;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        if (string.IsNullOrEmpty(_passageText)) return;

        float left = Padding.Left + 5;
        float right = Width - Padding.Right - 5;
        float x = left;
        float y = Padding.Top + 5;
        float lineHeight = _textFont.GetHeight(g) + 4;
        float spaceWidth = MeasureCharWidth(g, ' ');

        // Tách passage thành các từ, giữ lại dấu cách
        // Dùng word wrap: nếu từ tiếp theo không vừa dòng → xuống dòng
        int i = 0;
        while (i < _passageText.Length)
        {
            // Tìm từ tiếp theo (bao gồm cả khoảng trắng phía sau)
            int wordStart = i;
            // Bỏ qua khoảng trắng đầu
            while (i < _passageText.Length && _passageText[i] == ' ')
                i++;
            // Đọc ký tự không phải khoảng trắng
            while (i < _passageText.Length && _passageText[i] != ' ')
                i++;

            // Đo chiều rộng cả cụm (spaces + word)
            float wordWidth = 0;
            for (int j = wordStart; j < i; j++)
            {
                wordWidth += MeasureCharWidth(g, _passageText[j]);
            }

            // Nếu không vừa dòng hiện tại → xuống dòng (trừ khi đang ở đầu dòng)
            if (x + wordWidth > right && x > left)
            {
                x = left;
                y += lineHeight;
            }

            // Vẽ từng ký tự trong cụm
            for (int j = wordStart; j < i; j++)
            {
                char c = _passageText[j];

                // Chọn màu
                Brush brush;
                Color bgColor = Color.Transparent;

                if (j < _typedText.Length)
                {
                    if (_typedText[j] == c)
                    {
                        brush = Brushes.Green;
                    }
                    else
                    {
                        brush = Brushes.Red;
                        bgColor = Color.FromArgb(255, 230, 230);
                    }
                }
                else if (j == _typedText.Length)
                {
                    brush = Brushes.Black;
                    bgColor = Color.FromArgb(255, 255, 200);
                }
                else
                {
                    brush = Brushes.Gray;
                }

                // Hiển thị khoảng trắng thật để câu dễ đọc hơn.
                string displayChar = c.ToString();
                float charWidth = c == ' ' ? spaceWidth : MeasureCharWidth(g, c);

                // Vẽ nền
                if (bgColor != Color.Transparent)
                {
                    using var bgBrush = new SolidBrush(bgColor);
                    g.FillRectangle(bgBrush, x, y, charWidth + 1, lineHeight);
                }

                // Vẽ ký tự
                g.DrawString(displayChar, _textFont, brush, x, y, _textFormat);
                x += charWidth;
            }
        }
    }

    private float MeasureCharWidth(Graphics g, char c)
    {
        if (c == ' ')
        {
            return g.MeasureString(" ", _textFont, int.MaxValue, _textFormat).Width;
        }

        return g.MeasureString(c.ToString(), _textFont, int.MaxValue, _textFormat).Width;
    }

    /// <summary>Reset lại trạng thái cho trận mới</summary>
    public void Reset()
    {
        _typedText = string.Empty;
        _correctCount = 0;
        _wrongCount = 0;
        Invalidate();
    }

    private static string NormalizeUnicode(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value
            .Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var buffer = new char[normalized.Length];
        var count = 0;

        foreach (var ch in normalized)
        {
            if (char.IsControl(ch) && ch is not '\n' and not '\t')
                continue;

            buffer[count++] = char.IsWhiteSpace(ch) ? ' ' : ch;
        }

        return count == 0
            ? string.Empty
            : new string(buffer, 0, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textFont.Dispose();
            _textFormat.Dispose();
        }
        base.Dispose(disposing);
    }
}
