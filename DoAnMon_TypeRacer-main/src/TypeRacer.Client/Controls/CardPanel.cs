using System.Drawing.Drawing2D;

namespace TypeRacer.Client.Controls;

public class CardPanel : Panel
{
    private int _cornerRadius = 16;
    private Color _borderColor = Color.FromArgb(216, 226, 242);
    private int _borderThickness = 1;

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            UpdateRoundedRegion();
            Invalidate();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = Math.Max(0, value);
            Invalidate();
        }
    }

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Margin = new Padding(0);
        BackColor = Color.White;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        SizeChanged += (_, _) => UpdateRoundedRegion();
        HandleCreated += (_, _) => UpdateRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (BorderThickness <= 0)
            return;

        var rect = ClientRectangle;
        if (rect.Width <= 1 || rect.Height <= 1)
            return;

        rect.Width -= 1;
        rect.Height -= 1;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRect(rect, CornerRadius);
        using var pen = new Pen(BorderColor, BorderThickness);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRoundedRegion()
    {
        if (CornerRadius <= 1 || Width <= 0 || Height <= 0)
        {
            var old = Region;
            Region = null;
            old?.Dispose();
            return;
        }

        using var path = BuildRoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    private static GraphicsPath BuildRoundedRect(Rectangle rect, int radius)
    {
        if (radius <= 1)
        {
            var rectanglePath = new GraphicsPath();
            rectanglePath.AddRectangle(rect);
            return rectanglePath;
        }

        var r = Math.Max(1, radius);
        var diameter = r * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
