using TypeRacer.Client.Controls;

namespace TypeRacer.Client.Theme;

public enum ThemeButtonVariant
{
    Primary,
    Success,
    Danger,
    Neutral,
    Accent,
}

public static class ClientTheme
{
    public static readonly Color BackgroundTop = Color.FromArgb(248, 250, 252);
    public static readonly Color BackgroundBottom = Color.FromArgb(232, 238, 247);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceSubtle = Color.FromArgb(243, 246, 250);
    public static readonly Color Border = Color.FromArgb(207, 216, 229);
    public static readonly Color TextPrimary = Color.FromArgb(24, 32, 47);
    public static readonly Color TextMuted = Color.FromArgb(91, 103, 123);
    public static readonly Color HeaderStart = Color.FromArgb(20, 29, 47);
    public static readonly Color HeaderEnd = Color.FromArgb(34, 76, 132);
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Accent = Color.FromArgb(217, 119, 6);

    public static CardPanel CreateCard(Padding padding)
    {
        return new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = padding,
            BackColor = Surface,
            BorderColor = Border,
            CornerRadius = 10,
            Margin = new Padding(0),
        };
    }

    public static Panel CreateScrollablePageHost(Control content, int minContentHeight)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = BackgroundTop,
        };

        content.Dock = DockStyle.None;
        content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        content.Location = Point.Empty;
        host.Controls.Add(content);

        void ResizeContent()
        {
            var width = Math.Max(320, host.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            content.Width = width;
            content.Height = Math.Max(minContentHeight, host.ClientSize.Height);
        }

        host.Resize += (_, _) => ResizeContent();
        content.HandleCreated += (_, _) => ResizeContent();
        ResizeContent();

        return host;
    }

    public static void StyleButton(Button button, ThemeButtonVariant variant, bool compact = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.ForeColor = Color.White;
        button.AutoEllipsis = true;
        button.TextAlign = ContentAlignment.MiddleCenter;
        var minHeight = 44;
        var minWidth = compact ? 118 : 132;
        button.MinimumSize = new Size(
            Math.Max(button.MinimumSize.Width, minWidth),
            Math.Max(button.MinimumSize.Height, minHeight));
        if (button.Width > 0 && button.Width < button.MinimumSize.Width)
            button.Width = button.MinimumSize.Width;
        if (button.Height > 0 && button.Height < button.MinimumSize.Height)
            button.Height = button.MinimumSize.Height;
        button.Font = compact
            ? new Font("Segoe UI", 9.5f, FontStyle.Bold)
            : new Font("Segoe UI", 10f, FontStyle.Bold);
        button.Padding = compact ? new Padding(8, 0, 8, 0) : new Padding(12, 0, 12, 0);
        SetButtonVariant(button, variant);
        ApplyRoundedCorners(button, 8);
    }

    public static void SetButtonVariant(Button button, ThemeButtonVariant variant)
    {
        var palette = ResolvePalette(variant);
        button.BackColor = palette.Base;
        button.FlatAppearance.MouseOverBackColor = palette.Hover;
        button.FlatAppearance.MouseDownBackColor = palette.Pressed;
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.Font = new Font("Segoe UI", 10.5f);
        textBox.BackColor = Surface;
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.Font = new Font("Segoe UI", 10.5f);
        comboBox.BackColor = Surface;
        comboBox.ForeColor = TextPrimary;
        comboBox.FlatStyle = FlatStyle.Flat;
    }

    public static void StyleListBox(ListBox listBox)
    {
        listBox.BorderStyle = BorderStyle.None;
        listBox.BackColor = SurfaceSubtle;
        listBox.ForeColor = TextPrimary;
        listBox.Font = new Font("Segoe UI", 10f);
    }

    public static void StyleRichTextBox(RichTextBox richTextBox)
    {
        richTextBox.BorderStyle = BorderStyle.None;
        richTextBox.BackColor = SurfaceSubtle;
        richTextBox.ForeColor = TextPrimary;
        richTextBox.Font = new Font("Segoe UI", 9.75f);
    }

    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.Font = new Font("Segoe UI", 10f);
        grid.RowTemplate.Height = 38;

        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(236, 243, 255),
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(236, 243, 255),
            SelectionForeColor = TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(221, 233, 255),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 0, 8, 0),
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(249, 252, 255),
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(221, 233, 255),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 0, 8, 0),
        };
    }

    public static void StyleChatPanel(Control chatRoot)
    {
        chatRoot.BackColor = Surface;
        StyleControlTree(chatRoot);
    }

    public static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Dock = DockStyle.Top,
            Height = 30,
            Margin = new Padding(0, 0, 0, 10),
        };
    }

    public static void ApplyRoundedCorners(Control control, int radius)
    {
        if (radius <= 0)
            return;

        void UpdateRegion()
        {
            if (control.Width <= 0 || control.Height <= 0)
                return;

            using var path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius);
            var old = control.Region;
            control.Region = new Region(path);
            old?.Dispose();
        }

        control.HandleCreated += (_, _) => UpdateRegion();
        control.SizeChanged += (_, _) => UpdateRegion();

        if (control.IsHandleCreated)
            UpdateRegion();
    }

    private static void StyleControlTree(Control control)
    {
        foreach (Control child in control.Controls)
        {
            switch (child)
            {
                case ListBox listBox:
                    StyleListBox(listBox);
                    break;
                case TextBox textBox:
                    StyleTextBox(textBox);
                    textBox.Font = new Font("Segoe UI", 9.75f);
                    break;
                case Button button:
                    StyleButton(button, ThemeButtonVariant.Primary, compact: true);
                    break;
                case Panel panel:
                    panel.BackColor = Surface;
                    break;
            }

            if (child.HasChildren)
                StyleControlTree(child);
        }
    }

    private static ButtonPalette ResolvePalette(ThemeButtonVariant variant)
    {
        return variant switch
        {
            ThemeButtonVariant.Success => BuildPalette(Success),
            ThemeButtonVariant.Danger => BuildPalette(Danger),
            ThemeButtonVariant.Neutral => BuildPalette(Color.FromArgb(110, 126, 156)),
            ThemeButtonVariant.Accent => BuildPalette(Accent),
            _ => BuildPalette(Primary),
        };
    }

    private static ButtonPalette BuildPalette(Color baseColor)
    {
        return new ButtonPalette(
            baseColor,
            Shift(baseColor, -14),
            Shift(baseColor, -22));
    }

    private static Color Shift(Color color, int delta)
    {
        return Color.FromArgb(
            color.A,
            Clamp(color.R + delta),
            Clamp(color.G + delta),
            Clamp(color.B + delta));
    }

    private static int Clamp(int channel) => Math.Max(0, Math.Min(255, channel));

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var r = Math.Max(1, radius);
        var d = r * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private readonly record struct ButtonPalette(Color Base, Color Hover, Color Pressed);
}
