using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Game;
using TypeRacer.Shared.Protocol;
using TypeRacer.Shared.Typing;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form đua gõ phím — gameplay chính.
/// Hiển thị đoạn văn, ô nhập, thanh tiến trình, WPM/accuracy realtime.
/// Gửi TYPING_UPDATE mỗi 300ms, nhận PROGRESS_BROADCAST từ server.
/// </summary>
public class RaceForm : Form
{
    private readonly string _roomCode;
    private readonly string _passageText;
    private readonly bool _isHost;
    private readonly string _roomPassageLanguage;
    private readonly string _racePassageLanguage;
    private readonly int _raceDurationSeconds;
    private readonly bool _enableAiMode;
    private readonly bool _hasCustomPassage;
    private readonly string _gameMode;
    private readonly string _aiPracticeDifficulty;
    private readonly int _raceElapsedSeconds;

    // Controls
    private TypingTextBox _typingDisplay = null!;
    private ProgressTrack _progressTrack = null!;
    private TextBox _txtInput = null!;
    private Label _lblWpm = null!;
    private Label _lblRawWpm = null!;
    private Label _lblAccuracy = null!;
    private Label _lblTime = null!;
    private Label _lblCombo = null!;
    private Label _lblChars = null!;
    private Label _lblStatus = null!;

    // Trạng thái đua
    private DateTime _raceStartTime;
    private DateTime _raceEndTime;
    private bool _isFinished;
    private bool _raceFinishSent;
    private bool _isDisqualified;
    private int _backspaceCount;
    private int _bestStreak;
    private readonly List<TypingPerformanceSample> _performanceSamples = new();
    private DateTime _lastPerformanceSampleAt = DateTime.MinValue;
    private System.Windows.Forms.Timer _updateTimer = null!;
    private System.Windows.Forms.Timer _uiTimer = null!;

    // Cờ đánh dấu đóng form vì race kết thúc (không phải user nhấn X)
    private bool _closingForResult;

    public RaceForm(string roomCode, string passageText, bool isHost, string roomPassageLanguage, string racePassageLanguage, int raceDurationSeconds, bool enableAiMode = false, string gameMode = Shared.Constants.DefaultGameMode, string aiPracticeDifficulty = Shared.Constants.DefaultAiPracticeDifficulty, bool hasCustomPassage = false, int raceElapsedSeconds = 0)
    {
        _roomCode = roomCode;
        _passageText = TypingTextBox.NormalizeForTyping(passageText);
        _isHost = isHost;
        _roomPassageLanguage = NormalizeLanguage(roomPassageLanguage);
        _racePassageLanguage = NormalizeLanguage(racePassageLanguage);
        _raceDurationSeconds = Math.Clamp(raceDurationSeconds, Shared.Constants.MinRaceDurationSeconds, Shared.Constants.MaxRaceDurationSeconds);
        _enableAiMode = enableAiMode;
        _hasCustomPassage = hasCustomPassage;
        _gameMode = Shared.Constants.NormalizeGameMode(gameMode);
        _aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(aiPracticeDifficulty);
        _raceElapsedSeconds = Math.Clamp(raceElapsedSeconds, 0, _raceDurationSeconds - 1);
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Đang đua!";
        Size = new Size(1180, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ClientTheme.BackgroundTop;
        MinimumSize = new Size(760, 560);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120f,
            Padding = new Padding(22),
        };

        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var statsCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 135,
            CornerRadius = 22,
            Padding = new Padding(18, 14, 18, 12),
            Margin = new Padding(0, 0, 0, 14),
        };

        var statsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        statsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        statsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = $"Đường đua trực tiếp | Phòng {_roomCode}",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{ToGameModeLabel(_gameMode, _aiPracticeDifficulty)}: {ToGameModeHint(_gameMode, _aiPracticeDifficulty)} | Bài: {ToLanguageLabel(_racePassageLanguage)}",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(203, 216, 241),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var statValues = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
        };

        _lblWpm = CreateStatLabel("WPM: 0", Color.FromArgb(134, 255, 181), 126, 16f);
        _lblRawWpm = CreateStatLabel("Raw: 0", Color.FromArgb(172, 232, 255), 112, 13f);
        _lblAccuracy = CreateStatLabel("Chính xác: 100%", Color.FromArgb(255, 220, 143), 178, 13f);
        _lblTime = CreateStatLabel(FormatRemainingTime(_raceDurationSeconds), Color.White, 108, 16f);
        _lblCombo = CreateStatLabel("Combo: 0 / 0", Color.FromArgb(185, 234, 255), 150, 13f);
        _lblChars = CreateStatLabel("Ký tự: 0/0", Color.FromArgb(229, 236, 250), 142, 13f);

        statValues.Controls.Add(_lblWpm);
        statValues.Controls.Add(_lblRawWpm);
        statValues.Controls.Add(_lblAccuracy);
        statValues.Controls.Add(_lblTime);
        statValues.Controls.Add(_lblCombo);
        statValues.Controls.Add(_lblChars);

        statsLayout.Controls.Add(lblTitle, 0, 0);
        statsLayout.Controls.Add(_lblStatus, 0, 1);
        statsLayout.Controls.Add(statValues, 0, 2);
        statsCard.Controls.Add(statsLayout);

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
            Text = "Đoạn văn cần gõ",
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
            PlaceholderText = "Bắt đầu gõ ở đây...",
            ShortcutsEnabled = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _txtInput.TextChanged += TxtInput_TextChanged;
        _txtInput.KeyDown += TxtInput_KeyDown;

        inputLayout.Controls.Add(lblInput, 0, 0);
        inputLayout.Controls.Add(_txtInput, 0, 1);
        inputCard.Controls.Add(inputLayout);

        var trackCard = ClientTheme.CreateCard(new Padding(16));
        var trackLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        trackLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        trackLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTrack = new Label
        {
            Text = "Tiến độ người chơi",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _progressTrack = new ProgressTrack
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(246, 250, 255),
            MinimumSize = new Size(0, 160),
        };
        ClientTheme.ApplyRoundedCorners(_progressTrack, 14);

        trackLayout.Controls.Add(lblTrack, 0, 0);
        trackLayout.Controls.Add(_progressTrack, 0, 1);
        trackCard.Controls.Add(trackLayout);

        pageLayout.Controls.Add(statsCard, 0, 0);
        pageLayout.Controls.Add(passageCard, 0, 1);
        pageLayout.Controls.Add(inputCard, 0, 2);
        pageLayout.Controls.Add(trackCard, 0, 3);

        page.Controls.Add(pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 920));

        // Timers
        // Timer gửi TYPING_UPDATE mỗi 300ms
        _updateTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Timer cập nhật UI thời gian mỗi 100ms
        _uiTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _uiTimer.Tick += UiTimer_Tick;

        Load += RaceForm_Load;
    }

    private void RaceForm_Load(object? sender, EventArgs e)
    {
        var d = AppState.Instance.Dispatcher;
        d.OnProgressBroadcast += OnProgressBroadcast;
        d.OnRaceResult += OnRaceResult;

        // Bắt đầu đua
        _raceStartTime = DateTime.UtcNow.AddSeconds(-_raceElapsedSeconds);
        _raceEndTime = _raceStartTime.AddSeconds(_raceDurationSeconds);
        _updateTimer.Start();
        _uiTimer.Start();
        if (_raceElapsedSeconds > 0)
        {
            _lblStatus.Text = $"{ToGameModeLabel(_gameMode, _aiPracticeDifficulty)}: bạn vào giữa trận, bắt đầu gõ từ đầu và vẫn được tính kết quả.";
            _lblStatus.ForeColor = Color.FromArgb(255, 220, 143);
            _lblTime.Text = FormatRemainingTime(Math.Max(0, _raceDurationSeconds - _raceElapsedSeconds));
        }
        _txtInput.Focus();
    }

    private static Label CreateStatLabel(string text, Color color, int width, float fontSize)
    {
        return new Label
        {
            Text = text,
            Width = width,
            Height = 30,
            Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
            ForeColor = color,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 12, 4),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    // === Xử lý nhập liệu ===

    private void TxtInput_TextChanged(object? sender, EventArgs e)
    {
        if (_isFinished) return;

        var typed = _txtInput.Text;
        _typingDisplay.TypedText = typed;

        // Cập nhật WPM và Accuracy
        UpdateStats();

        var isSuddenDeath = _gameMode == Shared.Constants.GameModeSuddenDeath;
        if (isSuddenDeath &&
            TypingBoundaryComparer.HasCompletedSegmentMismatch(_typingDisplay.PassageText, _typingDisplay.TypedText))
        {
            _isDisqualified = true;
            _lblStatus.Text = "Sudden Death: sai sau khi kết thúc từ nên bị loại.";
            _ = SendRaceFinishAsync(isTimeout: false, isDisqualified: true);
            return;
        }

        if (isSuddenDeath &&
            TypingBoundaryComparer.HasPendingActiveWordMismatch(_typingDisplay.PassageText, _typingDisplay.TypedText))
        {
            _lblStatus.Text = "Sudden Death: đang chờ kết thúc từ để kiểm tra lỗi Telex.";
            _lblStatus.ForeColor = Color.FromArgb(203, 216, 241);
            return;
        }

        // Kiểm tra hoàn thành
        if (_typingDisplay.PassageText.Length > 0 &&
            _typingDisplay.CurrentPosition >= _typingDisplay.PassageText.Length)
        {
            _ = SendRaceFinishAsync(isTimeout: false);
        }
    }

    private void TxtInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
        {
            _backspaceCount++;
            if (_gameMode == Shared.Constants.GameModeNoBackspace)
            {
                e.SuppressKeyPress = true;
                _isDisqualified = true;
                _lblStatus.Text = "No Backspace: dùng phím xóa nên bị loại.";
                _ = SendRaceFinishAsync(isTimeout: false, isDisqualified: true);
                return;
            }
        }

        // Không cho phép Enter, Tab
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
        {
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateStats()
    {
        var elapsed = (DateTime.UtcNow - _raceStartTime).TotalSeconds;
        if (elapsed <= 0) return;

        int correct = _typingDisplay.CorrectCount;
        int wrong = _typingDisplay.WrongCount;
        int total = correct + wrong;

        // WPM = (ký tự đúng / 5) / (giây / 60)
        double wpm = (correct / 5.0) / (elapsed / 60.0);
        double rawWpm = (total / 5.0) / (elapsed / 60.0);

        // Accuracy
        double accuracy = total > 0 ? (double)correct / total * 100.0 : 100.0;
        var currentStreak = CalculateCurrentStreak(_typingDisplay.PassageText, _typingDisplay.TypedText);
        _bestStreak = Math.Max(_bestStreak, currentStreak);

        _lblWpm.Text = $"WPM: {wpm:F0}";
        _lblRawWpm.Text = $"Raw: {rawWpm:F0}";
        _lblAccuracy.Text = $"Chính xác: {accuracy:F1}%";
        _lblCombo.Text = $"Combo: {currentStreak} / {_bestStreak}";
        _lblChars.Text = $"Ký tự: {correct}/{wrong}";

        CapturePerformanceSample(elapsed, wpm, rawWpm, accuracy);
    }

    // === Timer gửi update lên server mỗi 300ms ===
    private async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isFinished) return;

        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.TYPING_UPDATE, new TypingUpdatePayload
            {
                RoomCode = _roomCode,
                CurrentPosition = _typingDisplay.CurrentPosition,
                CorrectChars = _typingDisplay.CorrectCount,
                WrongChars = _typingDisplay.WrongCount,
                TypedText = _typingDisplay.TypedText,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }
        catch { }
    }

    // === Timer cập nhật thời gian trên UI ===
    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (_isFinished)
            return;

        var remaining = _raceEndTime - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _ = SendRaceFinishAsync(isTimeout: true);
            return;
        }

        _lblTime.Text = FormatRemainingTime((int)Math.Ceiling(remaining.TotalSeconds));
    }

    // === Hoàn thành đua ===
    private async Task SendRaceFinishAsync(bool isTimeout, bool isDisqualified = false)
    {
        if (_raceFinishSent)
            return;

        _raceFinishSent = true;
        if (_isFinished) return;
        _isFinished = true;

        _updateTimer.Stop();
        _txtInput.Enabled = false;
        _lblStatus.Text = isTimeout
            ? "Hết giờ! Đang chờ kết quả..."
            : isDisqualified
                ? "Bị loại! Đang chờ kết quả..."
                : "Hoàn thành! Đang chờ kết quả...";
        _lblStatus.ForeColor = isDisqualified
            ? ClientTheme.Danger
            : Color.FromArgb(144, 245, 180);

        int timeTakenMs = Math.Max(1, (int)(DateTime.UtcNow - _raceStartTime).TotalMilliseconds);
        var maxDurationMs = Math.Max(1000, _raceDurationSeconds * 1000);
        if (timeTakenMs > maxDurationMs)
        {
            timeTakenMs = maxDurationMs;
        }

        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.RACE_FINISH, new RaceFinishPayload
            {
                RoomCode = _roomCode,
                CorrectChars = _typingDisplay.CorrectCount,
                WrongChars = _typingDisplay.WrongCount,
                TimeTakenMs = timeTakenMs,
                TypedText = _typingDisplay.TypedText,
                IsTimeout = isTimeout,
                BackspaceCount = _backspaceCount,
                IsDisqualified = isDisqualified || _isDisqualified,
            });
        }
        catch { }

        _uiTimer.Stop();
    }

    // === Xử lý message từ server ===

    private void OnProgressBroadcast(NetworkMessage message)
    {
        var payload = message.GetPayload<ProgressBroadcastPayload>();
        if (payload?.Players == null) return;

        _progressTrack.UpdatePlayers(payload.Players);
    }

    private void OnRaceResult(NetworkMessage message)
    {
        _updateTimer.Stop();
        _uiTimer.Stop();

        var payload = message.GetPayload<RaceResultPayload>();
        if (payload == null) return;

        // Mở form kết quả
        var resultForm = new ResultForm(payload, _passageText, _racePassageLanguage, _raceDurationSeconds, _gameMode, _aiPracticeDifficulty, _performanceSamples);
        resultForm.FormClosed += (s, e) =>
        {
            // Quay về phòng chờ
            var roomForm = new RoomForm(_roomCode, isHost: _isHost, _roomPassageLanguage, _raceDurationSeconds, _enableAiMode, _gameMode, _aiPracticeDifficulty, _hasCustomPassage);
            roomForm.Show();
        };
        resultForm.Show();
        _closingForResult = true;
        Close();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        // Chỉ gửi LEAVE_ROOM khi user đóng bằng nút X giữa trận (KHÔNG phải khi race kết thúc)
        if (!_closingForResult && e.CloseReason == CloseReason.UserClosing && AppState.Instance.CurrentRoomCode != null)
        {
            try
            {
                await AppState.Instance.Client.SendAsync(MessageType.LEAVE_ROOM,
                    new Shared.Payloads.Room.LeaveRoomRequest { RoomCode = _roomCode });
            }
            catch { }
            AppState.Instance.CurrentRoomCode = null;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _uiTimer.Stop();
        _uiTimer.Dispose();

        var d = AppState.Instance.Dispatcher;
        d.OnProgressBroadcast -= OnProgressBroadcast;
        d.OnRaceResult -= OnRaceResult;

        // Nếu user đóng bằng nút X (không phải vì race xong) → show MainForm
        if (!_closingForResult)
        {
            var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (mainForm != null)
                mainForm.Show();
            else
                new MainForm().Show();
        }

        base.OnFormClosed(e);
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            "any" => "any",
            _ => "en",
        };
    }

    private static string ToLanguageLabel(string languageCode)
    {
        return languageCode switch
        {
            "vi" => "Tiếng Việt",
            _ => "English",
        };
    }

    private static string ToGameModeLabel(string gameMode, string? aiPracticeDifficulty = null)
    {
        return Shared.Constants.NormalizeGameMode(gameMode) switch
        {
            Shared.Constants.GameModeAccuracy => "Accuracy Challenge",
            Shared.Constants.GameModeNoBackspace => "No Backspace",
            Shared.Constants.GameModeSuddenDeath => "Sudden Death",
            Shared.Constants.GameModeAiPractice => $"AI Practice {ToAiPracticeDifficultyLabel(aiPracticeDifficulty)}",
            _ => "Classic",
        };
    }

    private static string ToGameModeHint(string gameMode, string? aiPracticeDifficulty = null)
    {
        return Shared.Constants.NormalizeGameMode(gameMode) switch
        {
            Shared.Constants.GameModeAccuracy => "ưu tiên chính xác",
            Shared.Constants.GameModeNoBackspace => "không được xóa",
            Shared.Constants.GameModeSuddenDeath => "sai là loại",
            Shared.Constants.GameModeAiPractice => $"đua luyện tập với bot {ToAiPracticeDifficultyLabel(aiPracticeDifficulty)}",
            _ => "đua tốc độ",
        };
    }

    private static string ToAiPracticeDifficultyLabel(string? difficulty)
    {
        var normalized = Shared.Constants.NormalizeAiPracticeDifficulty(difficulty);
        var rpm = Shared.Constants.GetAiPracticeTargetRpm(normalized);
        var label = normalized switch
        {
            Shared.Constants.AiPracticeMedium => "Vừa",
            Shared.Constants.AiPracticeHard => "Khó",
            Shared.Constants.AiPracticeNightmare => "Ác mộng",
            _ => "Dễ",
        };
        return $"{label} {rpm} RPM";
    }

    private static int CalculateCurrentStreak(string passage, string typed)
    {
        var streak = 0;
        var limit = Math.Min(passage.Length, typed.Length);

        for (var i = limit - 1; i >= 0; i--)
        {
            if (typed[i] != passage[i])
                break;
            streak++;
        }

        return streak;
    }

    private void CapturePerformanceSample(double elapsedSeconds, double wpm, double rawWpm, double accuracy)
    {
        var now = DateTime.UtcNow;
        if (_performanceSamples.Count > 0 && (now - _lastPerformanceSampleAt).TotalMilliseconds < 650)
            return;

        _lastPerformanceSampleAt = now;
        _performanceSamples.Add(new TypingPerformanceSample(
            Math.Clamp(elapsedSeconds, 0, _raceDurationSeconds),
            Math.Max(0, wpm),
            Math.Max(0, rawWpm),
            Math.Clamp(accuracy, 0, 100)));

        if (_performanceSamples.Count > 180)
            _performanceSamples.RemoveAt(0);
    }

    private static string FormatRemainingTime(int totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        var time = TimeSpan.FromSeconds(totalSeconds);
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
