using TypeRacer.Client.Controls;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Payloads.Ai;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Luyện tập offline với đoạn văn vừa đua xong.
/// Không gửi dữ liệu lên server, chỉ dùng để luyện lại và cải thiện tốc độ.
/// </summary>
public class PracticeForm : Form
{
    private const float CollapsedTipRow = 0f;
    private const float ExpandedTipRow = 100f;

    private readonly string _passageText;
    private readonly string _language;
    private readonly string _missionTitle;
    private readonly string _missionObjective;
    private readonly string _missionRewardBadge;
    private readonly int _missionDurationSeconds;
    private readonly decimal _missionTargetWpm;
    private readonly decimal _missionTargetAccuracy;

    private TypingTextBox _typingDisplay = null!;
    private TextBox _txtInput = null!;
    private Label _lblWpm = null!;
    private Label _lblAccuracy = null!;
    private Label _lblTime = null!;
    private Label _lblStatus = null!;
    private CheckBox _chkStopOnError = null!;
    private Button _btnFocusMode = null!;
    private GradientPanel _page = null!;
    private TableLayoutPanel _pageLayout = null!;
    private CardPanel _tipCard = null!;

    private System.Windows.Forms.Timer _uiTimer = null!;
    private DateTime _startedAt;
    private bool _isStarted;
    private bool _isFinished;
    private bool _isFocusMode;
    private bool _isApplyingStopOnErrorCorrection;
    private Rectangle _restoreBounds;
    private FormBorderStyle _restoreBorderStyle;
    private FormWindowState _restoreWindowState;

    private bool HasMission => !string.IsNullOrWhiteSpace(_missionTitle);

    public PracticeForm(string passageText, string language)
        : this(passageText, language, null)
    {
    }

    public PracticeForm(string passageText, string language, AiPracticeMissionDto? mission)
    {
        _passageText = TypingTextBox.NormalizeForTyping(passageText);
        _language = NormalizeLanguage(language);
        _missionTitle = (mission?.Title ?? string.Empty).Trim();
        _missionObjective = (mission?.Objective ?? string.Empty).Trim();
        _missionRewardBadge = (mission?.RewardBadge ?? string.Empty).Trim();
        _missionDurationSeconds = Math.Clamp(mission?.DurationSeconds ?? 0, 0, 600);
        _missionTargetWpm = Math.Clamp(mission?.TargetWpm ?? 0m, 0m, 250m);
        _missionTargetAccuracy = Math.Clamp(mission?.TargetAccuracy ?? 0m, 0m, 99.9m);
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = HasMission ? "TypeRacer - AI practice mission" : "Luyện tập lại bài cũ";
        Size = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ClientTheme.BackgroundTop;
        MinimumSize = new Size(720, 520);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        _page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120,
            Padding = new Padding(22),
        };

        _pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        _pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 222));
        _pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 258));
        _pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        _pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 140,
            CornerRadius = 22,
            Padding = new Padding(18, 14, 18, 12),
            Margin = new Padding(0, 0, 0, 14),
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252));

        var title = new Label
        {
            Text = HasMission
                ? $"AI Mission: {_missionTitle} | {FormatMissionTarget()}"
                : $"Practice mode | Ngôn ngữ: {ToLanguageLabel(_language)}",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        var titleActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(12, 0, 0, 0),
        };
        titleActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        titleActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

        var btnRetryTop = new Button
        {
            Text = "Làm lại",
            Size = new Size(124, 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 3, 8, 3),
        };
        ClientTheme.StyleButton(btnRetryTop, ThemeButtonVariant.Success, compact: true);
        btnRetryTop.Click += (_, _) => ResetPractice();

        var btnCloseTop = new Button
        {
            Text = "Đóng",
            Size = new Size(104, 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 3, 0, 3),
        };
        ClientTheme.StyleButton(btnCloseTop, ThemeButtonVariant.Primary, compact: true);
        btnCloseTop.Click += (_, _) => Close();

        titleActions.Controls.Add(btnRetryTop, 0, 0);
        titleActions.Controls.Add(btnCloseTop, 1, 0);
        titleRow.Controls.Add(title, 0, 0);
        titleRow.Controls.Add(titleActions, 1, 0);

        var statsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _chkStopOnError = new CheckBox
        {
            Text = "Stop lỗi",
            Checked = true,
            AutoSize = false,
            Width = 106,
            Height = 36,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 5, 14, 0),
        };
        _chkStopOnError.CheckedChanged += (_, _) => EnforceStopOnErrorText();

        _btnFocusMode = new Button
        {
            Text = "Focus mode",
            Size = new Size(128, 44),
            Margin = new Padding(0, 3, 16, 0),
        };
        ClientTheme.StyleButton(_btnFocusMode, ThemeButtonVariant.Neutral, compact: true);
        _btnFocusMode.Click += (_, _) => ToggleFocusMode();

        _lblWpm = new Label
        {
            Text = "WPM: 0",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(134, 255, 181),
            AutoSize = true,
            Margin = new Padding(0, 9, 18, 0),
        };

        _lblAccuracy = new Label
        {
            Text = "Chính xác: 100%",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 220, 143),
            AutoSize = true,
            Margin = new Padding(0, 9, 18, 0),
        };

        _lblTime = new Label
        {
            Text = _missionDurationSeconds > 0 ? $"Còn: {FormatClock(_missionDurationSeconds)}" : "00:00",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };

        _lblStatus = new Label
        {
            Text = BuildInitialStatus(),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(202, 219, 249),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        statsFlow.Controls.AddRange(new Control[] { _chkStopOnError, _btnFocusMode, _lblWpm, _lblAccuracy, _lblTime });

        headerLayout.Controls.Add(titleRow, 0, 0);
        headerLayout.Controls.Add(statsFlow, 0, 1);
        headerLayout.Controls.Add(_lblStatus, 0, 2);
        headerCard.Controls.Add(headerLayout);

        var passageCard = ClientTheme.CreateCard(new Padding(16));
        passageCard.Margin = new Padding(0, 0, 0, 14);

        var passageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        passageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        passageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblPassage = new Label
        {
            Text = HasMission ? "Đoạn văn mission AI" : "Đoạn văn luyện tập",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _typingDisplay = new TypingTextBox
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            PassageText = _passageText,
            BackColor = ClientTheme.SurfaceSubtle,
        };
        ClientTheme.ApplyRoundedCorners(_typingDisplay, 14);

        passageLayout.Controls.Add(lblPassage, 0, 0);
        passageLayout.Controls.Add(_typingDisplay, 0, 1);
        passageCard.Controls.Add(passageLayout);

        var inputCard = ClientTheme.CreateCard(new Padding(16, 12, 16, 12));
        inputCard.Margin = new Padding(0, 0, 0, 14);

        var inputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblInput = new Label
        {
            Text = "Nhập văn bản",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _txtInput = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 14f),
            PlaceholderText = HasMission ? "Chơi mission AI này..." : "Luyện lại đoạn văn này...",
            ShortcutsEnabled = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _txtInput.TextChanged += TxtInput_TextChanged;
        _txtInput.KeyDown += TxtInput_KeyDown;
        _txtInput.KeyPress += TxtInput_KeyPress;

        inputLayout.Controls.Add(lblInput, 0, 0);
        inputLayout.Controls.Add(_txtInput, 0, 1);
        inputCard.Controls.Add(inputLayout);

        _tipCard = ClientTheme.CreateCard(new Padding(16));

        var tipLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        tipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        tipLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var lblTipTitle = new Label
        {
            Text = "Gợi ý luyện tập",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var tip = new Label
        {
            Dock = DockStyle.Fill,
            Text = HasMission
                ? BuildMissionTip()
                : "Mẹo: ưu tiên chính xác trước, sau đó tăng tốc dần. " +
                  "Luyện lại cùng một đoạn giúp bạn tạo muscle memory nhanh hơn.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ClientTheme.TextMuted,
        };

        var buttonLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var btnRetry = new Button
        {
            Text = "Làm lại đoạn này",
            Size = new Size(170, 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 2, 0, 2),
        };
        ClientTheme.StyleButton(btnRetry, ThemeButtonVariant.Success);
        btnRetry.Click += (_, _) => ResetPractice();

        var btnClose = new Button
        {
            Text = "Đóng",
            Size = new Size(112, 44),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, 2, 0, 2),
        };
        ClientTheme.StyleButton(btnClose, ThemeButtonVariant.Primary);
        btnClose.Click += (_, _) => Close();

        buttonLayout.Controls.Add(btnRetry, 0, 0);
        buttonLayout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 1, 0);
        buttonLayout.Controls.Add(btnClose, 2, 0);

        tipLayout.Controls.Add(lblTipTitle, 0, 0);
        tipLayout.Controls.Add(tip, 0, 1);
        tipLayout.Controls.Add(buttonLayout, 0, 2);
        _tipCard.Controls.Add(tipLayout);

        _pageLayout.Controls.Add(headerCard, 0, 0);
        _pageLayout.Controls.Add(passageCard, 0, 1);
        _pageLayout.Controls.Add(inputCard, 0, 2);
        _pageLayout.Controls.Add(_tipCard, 0, 3);

        _page.Controls.Add(_pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(_page, 934));

        _uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _uiTimer.Tick += UiTimer_Tick;

        Load += (_, _) => _txtInput.Focus();
    }

    private void TxtInput_TextChanged(object? sender, EventArgs e)
    {
        if (_isFinished || _isApplyingStopOnErrorCorrection) return;

        var typed = _txtInput.Text;
        if (TryApplyStopOnErrorCorrection(typed, out typed) &&
            typed.Length < _typingDisplay.PassageText.Length)
        {
            return;
        }

        _typingDisplay.TypedText = typed;

        if (!_isStarted && typed.Length > 0)
        {
            _isStarted = true;
            _startedAt = DateTime.UtcNow;
            _uiTimer.Start();
            _lblStatus.Text = HasMission ? "Mission đang chạy..." : "Đang luyện tập...";
        }

        UpdateStats();
        UpdateLiveStatus();

        if (_typingDisplay.PassageText.Length > 0 &&
            _typingDisplay.CurrentPosition >= _typingDisplay.PassageText.Length &&
            (!_chkStopOnError.Checked || _typingDisplay.WrongCount == 0))
        {
            var statusColor = Color.FromArgb(144, 245, 180);
            var statusText = HasMission
                ? BuildMissionCompletionStatus(out statusColor)
                : "Hoàn thành luyện tập! Bạn có thể bấm \"Làm lại đoạn này\" để luyện tiếp.";
            FinishPractice(statusText, statusColor);
        }
    }

    private void TxtInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
        }
    }

    private void TxtInput_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!_chkStopOnError.Checked || _isFinished || char.IsControl(e.KeyChar))
            return;

        // Vietnamese IME/Telex needs temporary mismatches inside the active word.
        _lblStatus.Text = "Stop lỗi sẽ kiểm tra sau khi bạn kết thúc từ.";
        _lblStatus.ForeColor = Color.FromArgb(202, 219, 249);
    }

    private bool TryApplyStopOnErrorCorrection(string typed, out string corrected)
    {
        corrected = typed;
        if (!_chkStopOnError.Checked ||
            !TryGetStopOnErrorMismatch(typed, out var mismatchIndex, out var expected))
        {
            return false;
        }

        corrected = typed[..mismatchIndex];
        _isApplyingStopOnErrorCorrection = true;
        _txtInput.Text = corrected;
        _txtInput.SelectionStart = _txtInput.TextLength;
        _isApplyingStopOnErrorCorrection = false;

        _typingDisplay.TypedText = corrected;
        if (!_isStarted && corrected.Length > 0)
        {
            _isStarted = true;
            _startedAt = DateTime.UtcNow;
            _uiTimer.Start();
        }

        UpdateStats();
        _lblStatus.Text = expected == '\0'
            ? "Stop lỗi: bạn đã gõ hết đoạn cần luyện."
            : $"Stop lỗi: cần ký tự '{FormatExpectedChar(expected)}' ở vị trí {mismatchIndex + 1}.";
        _lblStatus.ForeColor = ClientTheme.Danger;
        return true;
    }

    private void EnforceStopOnErrorText()
    {
        if (!_chkStopOnError.Checked || _isFinished || _isApplyingStopOnErrorCorrection)
            return;

        TryApplyStopOnErrorCorrection(_txtInput.Text, out _);
    }

    private bool TryGetStopOnErrorMismatch(string typed, out int mismatchIndex, out char expected)
    {
        var passage = _typingDisplay.PassageText;
        var completedLength = GetStopOnErrorComparableLength(typed, passage);
        var lengthToCompare = Math.Min(completedLength, passage.Length);
        for (var i = 0; i < lengthToCompare; i++)
        {
            if (typed[i] == passage[i])
                continue;

            mismatchIndex = i;
            expected = passage[i];
            return true;
        }

        if (completedLength <= passage.Length)
        {
            mismatchIndex = -1;
            expected = '\0';
            return false;
        }

        mismatchIndex = passage.Length;
        expected = '\0';
        return true;
    }

    private static int GetStopOnErrorComparableLength(string typed, string passage)
    {
        if (string.IsNullOrEmpty(typed))
            return 0;

        if (typed.Length > passage.Length)
            return typed.Length;

        if (IsWordBoundary(typed[^1]))
            return typed.Length;

        if (typed.Length == passage.Length &&
            string.Equals(typed, passage, StringComparison.Ordinal))
        {
            return typed.Length;
        }

        return FindCurrentWordStart(typed);
    }

    private static int FindCurrentWordStart(string typed)
    {
        for (var i = typed.Length - 1; i >= 0; i--)
        {
            if (IsWordBoundary(typed[i]))
                return i + 1;
        }

        return 0;
    }

    private static bool IsWordBoundary(char value)
        => char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isStarted) return;
        var elapsed = DateTime.UtcNow - _startedAt;
        if (_missionDurationSeconds > 0)
        {
            var remaining = _missionDurationSeconds - (int)elapsed.TotalSeconds;
            if (remaining <= 0)
            {
                _lblTime.Text = "00:00";
                FinishPractice("Hết thời gian mission. Bấm \"Làm lại đoạn này\" để retest ngay.", ClientTheme.Danger);
                return;
            }

            _lblTime.Text = $"Còn: {FormatClock(remaining)}";
            return;
        }

        _lblTime.Text = FormatClock((int)elapsed.TotalSeconds);
    }

    private void UpdateStats()
    {
        if (!_isStarted) return;

        var (wpm, accuracy) = CalculateCurrentMetrics();

        _lblWpm.Text = $"WPM: {wpm:F0}";
        _lblAccuracy.Text = $"Chính xác: {accuracy:F1}%";
    }

    private (double Wpm, double Accuracy) CalculateCurrentMetrics()
    {
        var elapsed = Math.Max(0.1, (DateTime.UtcNow - _startedAt).TotalSeconds);
        var correct = _typingDisplay.CorrectCount;
        var wrong = _typingDisplay.WrongCount;
        var total = correct + wrong;

        var wpm = (correct / 5.0) / (elapsed / 60.0);
        var accuracy = total > 0 ? (double)correct / total * 100.0 : 100.0;
        return (wpm, accuracy);
    }

    private void UpdateLiveStatus()
    {
        if (!_isStarted || _isFinished)
            return;

        if (_typingDisplay.WrongCount > 0)
        {
            _lblStatus.Text = $"Có {_typingDisplay.WrongCount} lỗi đang hiện trong bài luyện.";
            _lblStatus.ForeColor = ClientTheme.Danger;
            return;
        }

        _lblStatus.Text = _chkStopOnError.Checked
            ? HasMission ? $"Mission stop-on-error: {FormatMissionTarget()}." : "Đang luyện tập ở chế độ stop-on-error."
            : HasMission ? $"Mission đang chạy: {FormatMissionTarget()}." : "Đang luyện tập...";
        _lblStatus.ForeColor = Color.FromArgb(202, 219, 249);
    }

    private void ResetPractice()
    {
        _isStarted = false;
        _isFinished = false;
        _uiTimer.Stop();

        _txtInput.Enabled = true;
        _txtInput.Text = string.Empty;
        _typingDisplay.TypedText = string.Empty;
        _txtInput.Focus();

        _lblStatus.Text = HasMission ? $"Đã reset mission. {BuildInitialStatus()}" : "Đã reset. Gõ để bắt đầu lại.";
        _lblStatus.ForeColor = Color.FromArgb(202, 219, 249);
        _lblWpm.Text = "WPM: 0";
        _lblAccuracy.Text = "Chính xác: 100%";
        _lblTime.Text = _missionDurationSeconds > 0 ? $"Còn: {FormatClock(_missionDurationSeconds)}" : "00:00";
    }

    private void ToggleFocusMode()
    {
        if (!_isFocusMode)
        {
            _restoreBounds = Bounds;
            _restoreBorderStyle = FormBorderStyle;
            _restoreWindowState = WindowState;

            SuspendLayout();
            _isFocusMode = true;
            _btnFocusMode.Text = "Thoát focus";
            _tipCard.Visible = false;
            _page.Padding = new Padding(12);
            _pageLayout.RowStyles[3].SizeType = SizeType.Absolute;
            _pageLayout.RowStyles[3].Height = CollapsedTipRow;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(true);
            _txtInput.Focus();
            return;
        }

        SuspendLayout();
        _isFocusMode = false;
        _btnFocusMode.Text = "Focus mode";
        _tipCard.Visible = true;
        _page.Padding = new Padding(22);
        _pageLayout.RowStyles[3].SizeType = SizeType.Percent;
        _pageLayout.RowStyles[3].Height = ExpandedTipRow;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = _restoreBorderStyle;
        Bounds = _restoreBounds;
        WindowState = _restoreWindowState;
        ResumeLayout(true);
        _txtInput.Focus();
    }

    private void FinishPractice(string statusText, Color statusColor)
    {
        if (_isFinished)
            return;

        _isFinished = true;
        _uiTimer.Stop();
        _txtInput.Enabled = false;
        _lblStatus.Text = statusText;
        _lblStatus.ForeColor = statusColor;
    }

    private string BuildInitialStatus()
    {
        if (!HasMission)
            return "Gõ để bắt đầu luyện tập.";

        var objective = string.IsNullOrWhiteSpace(_missionObjective)
            ? "Gõ để bắt đầu mission AI."
            : _missionObjective;
        return $"{objective} {FormatMissionTarget()}";
    }

    private string BuildMissionTip()
    {
        var reward = string.IsNullOrWhiteSpace(_missionRewardBadge)
            ? "AI Mission Clear"
            : _missionRewardBadge;
        return $"Mission AI này có timer và target riêng. {BuildInitialStatus()} " +
               $"Nếu qua mục tiêu, ghi badge: {reward}.";
    }

    private string FormatMissionTarget()
    {
        var parts = new List<string>();
        if (_missionDurationSeconds > 0)
            parts.Add($"{FormatClock(_missionDurationSeconds)}");
        if (_missionTargetWpm > 0)
            parts.Add($"{_missionTargetWpm:F1} WPM");
        if (_missionTargetAccuracy > 0)
            parts.Add($"{_missionTargetAccuracy:F1}% accuracy");

        return parts.Count == 0 ? ToLanguageLabel(_language) : string.Join(" | ", parts);
    }

    private string BuildMissionCompletionStatus(out Color statusColor)
    {
        var (wpm, accuracy) = CalculateCurrentMetrics();
        var wpmOk = _missionTargetWpm <= 0 || (decimal)wpm >= _missionTargetWpm;
        var accuracyOk = _missionTargetAccuracy <= 0 || (decimal)accuracy >= _missionTargetAccuracy;
        var reward = string.IsNullOrWhiteSpace(_missionRewardBadge)
            ? "AI Mission Clear"
            : _missionRewardBadge;

        if (wpmOk && accuracyOk)
        {
            statusColor = Color.FromArgb(144, 245, 180);
            return $"Qua mission! Badge: {reward}. Kết quả {wpm:F1} WPM | {accuracy:F1}% accuracy.";
        }

        var missing = new List<string>();
        if (!wpmOk)
            missing.Add($"WPM {wpm:F1}/{_missionTargetWpm:F1}");
        if (!accuracyOk)
            missing.Add($"accuracy {accuracy:F1}/{_missionTargetAccuracy:F1}%");

        statusColor = ClientTheme.Danger;
        return $"Hoàn thành passage nhưng chưa qua target mission ({string.Join(", ", missing)}). Bấm \"Làm lại đoạn này\" để retest.";
    }

    private static string FormatClock(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:D2}:{seconds:D2}";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11)
        {
            ToggleFocusMode();
            return true;
        }

        if (_isFocusMode && keyData == Keys.Escape)
        {
            ToggleFocusMode();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static string FormatExpectedChar(char expected)
    {
        return expected switch
        {
            ' ' => "space",
            '\t' => "tab",
            '\n' => "xuống dòng",
            _ => expected.ToString(),
        };
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _uiTimer.Stop();
        _uiTimer.Dispose();
        base.OnFormClosed(e);
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            _ => "en",
        };
    }

    private static string ToLanguageLabel(string languageCode)
    {
        return languageCode == "vi" ? "Tiếng Việt" : "English";
    }
}
