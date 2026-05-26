using System.Drawing.Drawing2D;

namespace TypeRacer.Client.Controls;

public class GradientPanel : Panel
{
    private Color _startColor = Color.White;
    private Color _endColor = Color.Gainsboro;
    private float _angle = 120f;
    private int _cornerRadius;
    private bool _drawBorder;
    private Color _borderColor = Color.Transparent;

    public Color StartColor
    {
        get => _startColor;
        set
        {
            _startColor = value;
            Invalidate();
        }
    }

    public Color EndColor
    {
        get => _endColor;
        set
        {
            _endColor = value;
            Invalidate();
        }
    }

    public float Angle
    {
        get => _angle;
        set
        {
            _angle = value;
            Invalidate();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            Invalidate();
        }
    }

    public bool DrawBorder
    {
        get => _drawBorder;
        set
        {
            _drawBorder = value;
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

    public GradientPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        rect.Width = Math.Max(1, rect.Width - 1);
        rect.Height = Math.Max(1, rect.Height - 1);

        using var brush = new LinearGradientBrush(rect, StartColor, EndColor, Angle);

        if (CornerRadius <= 1)
        {
            g.FillRectangle(brush, rect);
            if (DrawBorder)
            {
                using var pen = new Pen(BorderColor, 1f);
                g.DrawRectangle(pen, rect);
            }
            return;
        }

        using var path = BuildRoundedRect(rect, CornerRadius);
        g.FillPath(brush, path);

        if (DrawBorder)
        {
            using var pen = new Pen(BorderColor, 1f);
            g.DrawPath(pen, path);
        }
    }

    private static GraphicsPath BuildRoundedRect(Rectangle rect, int radius)
    {
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
