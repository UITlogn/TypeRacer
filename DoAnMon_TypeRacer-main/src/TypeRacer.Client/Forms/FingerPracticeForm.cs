using TypeRacer.Client.Controls;
using TypeRacer.Client.Theme;

namespace TypeRacer.Client.Forms;

public sealed class FingerPracticeForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Random _random = new();
    private readonly List<char> _recentKeys = new();

    private ComboBox _cmbLevel = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Label _lblKey = null!;
    private Label _lblTimer = null!;
    private Label _lblStats = null!;
    private Label _lblFingerHint = null!;
    private Label _lblAiFeedback = null!;

    private string _activeKeys = FingerLevels[0].Keys;
    private char _currentKey;
    private int _remainingSeconds;
    private int _correctCount;
    private int _wrongCount;
    private int _currentStreak;
    private int _bestStreak;
    private bool _isRunning;

    private const int PracticeSeconds = 30;

    private static readonly FingerLevel[] FingerLevels =
    {
        new("Level 1 - home anchors", "fj", "Ngón trỏ trái/phải: f và j."),
        new("Level 2 - index balance", "fjkd", "Thêm k và d để cân bằng hai tay."),
        new("Level 3 - home row", "asdfghjkl;", "Toàn bộ hàng home row."),
        new("Level 4 - lower/index reach", "erdfcvuijkm", "Mở rộng vùng trỏ và ngón giữa xuống/lên."),
        new("Level 5 - outer columns", "qwaszxopl;./", "Nhóm phím ngoài, ngón út và ngón áp út."),
        new("Level 6 - mixed weak keys", "tyghbnqpa;z/", "Nhóm phím dễ lệch tay để luyện phản xạ."),
    };

    public FingerPracticeForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Luyện phím theo finger";
        Size = new Size(860, 620);
        MinimumSize = new Size(720, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ClientTheme.BackgroundTop;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        KeyPreview = true;

        var page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120,
            Padding = new Padding(22),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 135,
            CornerRadius = 20,
            Padding = new Padding(18, 12, 18, 12),
            Margin = new Padding(0, 0, 0, 14),
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Luyện phản xạ từng ký tự theo nhóm finger",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        _lblFingerHint = new Label
        {
            Text = FingerLevels[0].Hint,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(214, 226, 246),
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        headerLayout.Controls.Add(title, 0, 0);
        headerLayout.Controls.Add(_lblFingerHint, 0, 1);
        header.Controls.Add(headerLayout);

        var drillCard = ClientTheme.CreateCard(new Padding(18));
        drillCard.Margin = new Padding(0, 0, 0, 14);
        var drillLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        drillLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        drillLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controlsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _cmbLevel = new ComboBox
        {
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 12, 6),
        };
        _cmbLevel.Items.AddRange(FingerLevels.Select(l => l.Name).Cast<object>().ToArray());
        _cmbLevel.SelectedIndex = 0;
        _cmbLevel.SelectedIndexChanged += (_, _) => SelectLevel();
        ClientTheme.StyleComboBox(_cmbLevel);

        _btnStart = new Button
        {
            Text = "Bắt đầu 30s",
            Size = new Size(134, 44),
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnStart, ThemeButtonVariant.Success);
        _btnStart.Click += (_, _) => StartPractice();

        _btnStop = new Button
        {
            Text = "Dừng",
            Size = new Size(112, 44),
            Enabled = false,
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnStop, ThemeButtonVariant.Danger);
        _btnStop.Click += (_, _) => FinishPractice(manualStop: true);

        _lblTimer = new Label
        {
            Text = "30s",
            Width = 88,
            Height = 44,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = ClientTheme.Primary,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 12, 6),
        };

        controlsFlow.Controls.Add(_cmbLevel);
        controlsFlow.Controls.Add(_btnStart);
        controlsFlow.Controls.Add(_btnStop);
        controlsFlow.Controls.Add(_lblTimer);

        _lblKey = new Label
        {
            Text = "F",
            Dock = DockStyle.Fill,
            BackColor = ClientTheme.SurfaceSubtle,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Consolas", 96f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0),
        };

        drillLayout.Controls.Add(controlsFlow, 0, 0);
        drillLayout.Controls.Add(_lblKey, 0, 1);
        drillCard.Controls.Add(drillLayout);

        var statsCard = ClientTheme.CreateCard(new Padding(18, 14, 18, 14));
        statsCard.Margin = new Padding(0, 0, 0, 14);
        _lblStats = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Sẵn sàng: chọn level, bấm Bắt đầu, rồi gõ đúng ký tự lớn ở giữa màn hình.",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        statsCard.Controls.Add(_lblStats);

        var feedbackCard = ClientTheme.CreateCard(new Padding(18));
        _lblAiFeedback = new Label
        {
            Dock = DockStyle.Fill,
            Text = "AI nhận xét sẽ xuất hiện sau phiên 30 giây.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ClientTheme.TextMuted,
            TextAlign = ContentAlignment.TopLeft,
        };
        feedbackCard.Controls.Add(_lblAiFeedback);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(drillCard, 0, 1);
        layout.Controls.Add(statsCard, 0, 2);
        layout.Controls.Add(feedbackCard, 0, 3);
        page.Controls.Add(layout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 620));

        _timer.Tick += Timer_Tick;
        KeyPress += FingerPracticeForm_KeyPress;
        SelectLevel();
    }

    private void SelectLevel()
    {
        var level = FingerLevels[Math.Max(0, _cmbLevel.SelectedIndex)];
        _activeKeys = level.Keys;
        _lblFingerHint.Text = $"{level.Hint} Nhóm phím: {level.Keys}";
        _lblKey.Text = level.Keys[0].ToString().ToUpperInvariant();
        _lblAiFeedback.Text = "AI nhận xét sẽ xuất hiện sau phiên 30 giây.";
    }

    private void StartPractice()
    {
        _remainingSeconds = PracticeSeconds;
        _correctCount = 0;
        _wrongCount = 0;
        _currentStreak = 0;
        _bestStreak = 0;
        _recentKeys.Clear();
        _isRunning = true;
        _cmbLevel.Enabled = false;
        _btnStart.Enabled = false;
        _btnStop.Enabled = true;
        _lblAiFeedback.Text = "Đang luyện: nhìn ký tự lớn, gõ dứt khoát bằng đúng nhóm finger.";
        NextKey();
        UpdateStats();
        _timer.Start();
        Focus();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isRunning)
            return;

        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            FinishPractice(manualStop: false);
            return;
        }

        UpdateStats();
    }

    private void FingerPracticeForm_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!_isRunning || char.IsControl(e.KeyChar))
            return;

        var typed = char.ToLowerInvariant(e.KeyChar);
        if (typed == _currentKey)
        {
            _correctCount++;
            _currentStreak++;
            _bestStreak = Math.Max(_bestStreak, _currentStreak);
            NextKey();
        }
        else
        {
            _wrongCount++;
            _currentStreak = 0;
            _lblKey.ForeColor = ClientTheme.Danger;
        }

        UpdateStats();
        e.Handled = true;
    }

    private void NextKey()
    {
        if (string.IsNullOrEmpty(_activeKeys))
            _activeKeys = FingerLevels[0].Keys;

        char next;
        var attempts = 0;
        do
        {
            next = char.ToLowerInvariant(_activeKeys[_random.Next(_activeKeys.Length)]);
            attempts++;
        } while (_recentKeys.Count > 0 && _recentKeys[^1] == next && attempts < 8);

        _currentKey = next;
        _recentKeys.Add(next);
        if (_recentKeys.Count > 18)
            _recentKeys.RemoveAt(0);

        _lblKey.ForeColor = ClientTheme.TextPrimary;
        _lblKey.Text = next.ToString().ToUpperInvariant();
    }

    private void UpdateStats()
    {
        var total = _correctCount + _wrongCount;
        var accuracy = total > 0 ? _correctCount * 100.0 / total : 100.0;
        _lblTimer.Text = $"{Math.Max(0, _remainingSeconds)}s";
        _lblStats.Text =
            $"Đúng: {_correctCount}   Sai: {_wrongCount}   Accuracy: {accuracy:F1}%   " +
            $"Streak: {_currentStreak}/{_bestStreak}   Speed: {_correctCount * 2} ký tự/phút";
    }

    private void FinishPractice(bool manualStop)
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _timer.Stop();
        _cmbLevel.Enabled = true;
        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        _remainingSeconds = Math.Max(0, _remainingSeconds);
        UpdateStats();
        _lblAiFeedback.Text = BuildAiFeedback(manualStop);
    }

    private string BuildAiFeedback(bool manualStop)
    {
        var total = _correctCount + _wrongCount;
        var accuracy = total > 0 ? _correctCount * 100.0 / total : 100.0;
        var speed = _correctCount * 2;
        var level = FingerLevels[Math.Max(0, _cmbLevel.SelectedIndex)];
        var status = manualStop ? "Phiên dừng sớm." : "Phiên 30 giây đã hoàn tất.";

        var focus = accuracy < 88
            ? "AI nhận xét: giảm tốc, nhìn kỹ ký tự trước khi bấm; ưu tiên đúng hơn nhanh."
            : speed < 70
                ? "AI nhận xét: độ chính xác ổn, hãy tăng nhịp bằng cách giữ cổ tay cố định và dùng đúng ngón."
                : "AI nhận xét: phản xạ tốt; có thể tăng level hoặc luyện nhóm phím yếu hơn.";

        var weakHint = _wrongCount > 0
            ? "Nếu sai nhiều ở ký tự ngoài rìa, hãy luyện lại level trước 2-3 lượt để khóa cơ tay."
            : "Không có lỗi trong phiên này, giữ nhịp và chuyển level cao hơn.";

        return $"{status}\nLevel: {level.Name} ({level.Keys})\n" +
               $"Kết quả: {_correctCount} ký tự đúng, {_wrongCount} lỗi, accuracy {accuracy:F1}%, best streak {_bestStreak}, tốc độ {speed} ký tự/phút.\n" +
               $"{focus}\n{weakHint}";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private sealed record FingerLevel(string Name, string Keys, string Hint);
}
