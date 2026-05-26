using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Chat;
using TypeRacer.Shared.Payloads.Room;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Phòng chờ: hiển thị danh sách người chơi, nút sẵn sàng, chat, nút bắt đầu (cho host).
/// </summary>
public class RoomForm : Form
{
    private readonly string _roomCode;
    private string _passageLanguage;
    private int _raceDurationSeconds;
    private bool _enableAiMode;
    private bool _hasCustomPassage;
    private readonly bool _isCommunityRoom;
    private string _gameMode;
    private string _aiPracticeDifficulty;
    private bool _isHost;

    private ListBox _lstPlayers = null!;
    private Button _btnReady = null!;
    private Button _btnStart = null!;
    private Button _btnLeave = null!;
    private Label _lblRoomCode = null!;
    private Label _lblStatus = null!;
    private Label _lblLanguage = null!;
    private Label _lblSettings = null!;
    private ChatPanel _chatPanel = null!;
    private Label _lblCommunityCountdown = null!;
    private Label _lblWarmupPrompt = null!;
    private Label _lblWarmupStats = null!;
    private TextBox _txtWarmup = null!;
    private Button _btnWarmupNext = null!;
    private System.Windows.Forms.Timer? _communityCountdownTimer;
    private bool _isReady;
    private bool _closingForRace;
    private bool _returnedToMain;
    private int _communitySecondsUntilNextStart;
    private string _communityRoomStatus;
    private string _warmupPrompt = string.Empty;
    private string _warmupLastText = string.Empty;
    private DateTime _warmupStartedAt = DateTime.MinValue;
    private int _warmupKeystrokes;
    private int _warmupMistakes;
    private int _warmupCurrentStreak;
    private int _warmupBestStreak;
    private bool _warmupCompleted;

    public RoomForm(
        string roomCode,
        bool isHost,
        string passageLanguage = "en",
        int raceDurationSeconds = Shared.Constants.DefaultRaceDurationSeconds,
        bool enableAiMode = false,
        string gameMode = Shared.Constants.DefaultGameMode,
        string aiPracticeDifficulty = Shared.Constants.DefaultAiPracticeDifficulty,
        bool hasCustomPassage = false,
        int initialSecondsUntilNextStart = 0,
        string? initialRoomStatus = null)
    {
        _roomCode = roomCode;
        _isHost = isHost;
        _passageLanguage = NormalizeLanguage(passageLanguage);
        _raceDurationSeconds = Math.Clamp(raceDurationSeconds, Shared.Constants.MinRaceDurationSeconds, Shared.Constants.MaxRaceDurationSeconds);
        _enableAiMode = enableAiMode;
        _hasCustomPassage = hasCustomPassage;
        _isCommunityRoom = string.Equals(roomCode, Shared.Constants.CommunityRoomCode, StringComparison.OrdinalIgnoreCase);
        _gameMode = Shared.Constants.NormalizeGameMode(gameMode);
        _aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(aiPracticeDifficulty);
        _communitySecondsUntilNextStart = Math.Max(0, initialSecondsUntilNextStart);
        _communityRoomStatus = (initialRoomStatus ?? "waiting").Trim().ToLowerInvariant();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"TypeRacer - Phòng {_roomCode}";
        Size = new Size(1140, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ClientTheme.BackgroundTop;
        MinimumSize = new Size(1040, 720);
        AutoScaleMode = AutoScaleMode.Dpi;

        var page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120,
            Padding = new Padding(22),
        };

        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 140,
            CornerRadius = 22,
            Padding = new Padding(22, 18, 22, 18),
            Margin = new Padding(0, 0, 0, 14),
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var infoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblRoomCode = new Label
        {
            Text = $"Phòng {_roomCode}",
            Font = new Font("Segoe UI", 21f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblLanguage = new Label
        {
            Text = $"Ngôn ngữ: {ToLanguageLabel(_passageLanguage)}",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(198, 219, 255),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblSettings = new Label
        {
            Text = BuildRoomSettingsText(_passageLanguage, _raceDurationSeconds, _enableAiMode, _gameMode, _aiPracticeDifficulty, _hasCustomPassage),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(198, 219, 255),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            Margin = new Padding(0, 4, 0, 0),
        };

        _lblStatus = new Label
        {
            Text = "Đang chờ người chơi sẵn sàng...",
            Font = new Font("Segoe UI", 10f),
            ForeColor = ClientTheme.Accent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
        };

        _btnLeave = new Button
        {
            Text = "Rời phòng",
            Size = new Size(126, 44),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(12, 18, 0, 0),
        };
        ClientTheme.StyleButton(_btnLeave, ThemeButtonVariant.Danger);
        _btnLeave.Click += BtnLeave_Click;

        infoLayout.Controls.Add(_lblRoomCode, 0, 0);
        infoLayout.Controls.Add(_lblLanguage, 0, 1);
        infoLayout.Controls.Add(_lblSettings, 0, 2);
        infoLayout.Controls.Add(_lblStatus, 0, 3);

        headerLayout.Controls.Add(infoLayout, 0, 0);
        headerLayout.Controls.Add(_btnLeave, 1, 0);
        headerCard.Controls.Add(headerLayout);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var playersCard = ClientTheme.CreateCard(new Padding(16));
        playersCard.Margin = new Padding(0, 0, 14, 0);

        var playersLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        playersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        playersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        playersLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblPlayers = new Label
        {
            Text = "Người chơi trong phòng",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lstPlayers = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
        };
        ClientTheme.StyleListBox(_lstPlayers);

        var buttonLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _btnReady = new Button
        {
            Text = _isCommunityRoom ? "Auto start" : "Sẵn sàng",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 6, 0),
            Enabled = !_isCommunityRoom,
        };
        ClientTheme.StyleButton(_btnReady, ThemeButtonVariant.Success);
        _btnReady.Click += BtnReady_Click;

        _btnStart = new Button
        {
            Text = "Bắt đầu",
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 8, 0, 0),
            Visible = _isHost && !_isCommunityRoom,
        };
        ClientTheme.StyleButton(_btnStart, ThemeButtonVariant.Primary);
        _btnStart.Click += BtnStart_Click;

        buttonLayout.Controls.Add(_btnReady, 0, 0);
        buttonLayout.Controls.Add(_btnStart, 1, 0);

        playersLayout.Controls.Add(lblPlayers, 0, 0);
        playersLayout.Controls.Add(buttonLayout, 0, 1);
        playersLayout.Controls.Add(_lstPlayers, 0, 2);
        playersCard.Controls.Add(playersLayout);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 208));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var warmupCard = BuildWarmupCard();

        var chatCard = ClientTheme.CreateCard(new Padding(16));
        var chatLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblChat = new Label
        {
            Text = "Chat trong phòng",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _chatPanel = new ChatPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = ClientTheme.Surface,
        };
        _chatPanel.MessageSent += ChatPanel_MessageSent;
        ClientTheme.StyleChatPanel(_chatPanel);

        chatLayout.Controls.Add(lblChat, 0, 0);
        chatLayout.Controls.Add(_chatPanel, 0, 1);
        chatCard.Controls.Add(chatLayout);
        rightLayout.Controls.Add(warmupCard, 0, 0);
        rightLayout.Controls.Add(chatCard, 0, 1);

        contentLayout.Controls.Add(playersCard, 0, 0);
        contentLayout.Controls.Add(rightLayout, 1, 0);

        pageLayout.Controls.Add(headerCard, 0, 0);
        pageLayout.Controls.Add(contentLayout, 0, 1);
        page.Controls.Add(pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 940));

        InitializeCommunityCountdownState();
        ResetWarmupPrompt();

        Load += RoomForm_Load;
    }

    private Control BuildWarmupCard()
    {
        var warmupCard = ClientTheme.CreateCard(new Padding(16));
        warmupCard.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));

        var lblWarmupTitle = new Label
        {
            Text = "Warm-up phòng chờ",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _btnWarmupNext = new Button
        {
            Text = "Đổi câu",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 2, 0, 2),
        };
        ClientTheme.StyleButton(_btnWarmupNext, ThemeButtonVariant.Accent, compact: true);
        _btnWarmupNext.Click += (_, _) => ResetWarmupPrompt();

        titleRow.Controls.Add(lblWarmupTitle, 0, 0);
        titleRow.Controls.Add(_btnWarmupNext, 1, 0);

        _lblCommunityCountdown = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(154, 52, 18),
            BackColor = Color.FromArgb(255, 247, 237),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = _isCommunityRoom,
            Margin = new Padding(0, 0, 0, 8),
        };

        _lblWarmupPrompt = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.25f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            BackColor = ClientTheme.SurfaceSubtle,
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 0, 8),
        };

        _txtWarmup = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
        };
        ClientTheme.StyleTextBox(_txtWarmup);
        _txtWarmup.TextChanged += TxtWarmup_TextChanged;

        _lblWarmupStats = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = ClientTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        layout.Controls.Add(titleRow, 0, 0);
        layout.Controls.Add(_lblCommunityCountdown, 0, 1);
        layout.Controls.Add(_lblWarmupPrompt, 0, 2);
        layout.Controls.Add(_txtWarmup, 0, 3);
        layout.Controls.Add(_lblWarmupStats, 0, 4);
        warmupCard.Controls.Add(layout);
        return warmupCard;
    }

    private void RoomForm_Load(object? sender, EventArgs e)
    {
        var d = AppState.Instance.Dispatcher;
        d.OnRoomUpdate += OnRoomUpdate;
        d.OnPlayerJoined += OnPlayerJoined;
        d.OnPlayerLeft += OnPlayerLeft;
        d.OnChatBroadcast += OnChatBroadcast;
        d.OnRaceCountdown += OnRaceCountdown;
        d.OnRaceStart += OnRaceStart;
        d.OnError += OnError;

        // Gửi PLAYER_READY (false) để server broadcast ROOM_UPDATE → cập nhật player list
        _ = RequestRoomUpdateAsync();
    }

    private async Task RequestRoomUpdateAsync()
    {
        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.PLAYER_READY, new PlayerReadyRequest
            {
                RoomCode = _roomCode,
                IsReady = false,
            });
        }
        catch { }
    }

    // === Xử lý sự kiện giao diện ===

    private async void BtnReady_Click(object? sender, EventArgs e)
    {
        _isReady = !_isReady;
        _btnReady.Text = _isReady ? "Hủy sẵn sàng" : "Sẵn sàng";
        ClientTheme.SetButtonVariant(_btnReady, _isReady ? ThemeButtonVariant.Danger : ThemeButtonVariant.Success);

        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.PLAYER_READY, new PlayerReadyRequest
            {
                RoomCode = _roomCode,
                IsReady = _isReady,
            });
        }
        catch { }
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        try
        {
            // Gửi RACE_START để host yêu cầu bắt đầu
            await AppState.Instance.Client.SendEmptyAsync(MessageType.RACE_START);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnLeave_Click(object? sender, EventArgs e)
    {
        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.LEAVE_ROOM, new Shared.Payloads.Room.LeaveRoomRequest
            {
                RoomCode = _roomCode,
            });
        }
        catch { }

        AppState.Instance.CurrentRoomCode = null;
        ReturnToMainForm();
    }

    private async void ChatPanel_MessageSent(string text)
    {
        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.CHAT_SEND, new Shared.Payloads.Chat.ChatSendPayload
            {
                RoomCode = _roomCode,
                Content = text,
            });
        }
        catch { }
    }

    // === Xử lý message từ server ===

    private void OnRoomUpdate(NetworkMessage message)
    {
        var payload = message.GetPayload<RoomUpdatePayload>();
        if (payload == null) return;

        if (payload.Room != null)
        {
            var oldLanguage = _passageLanguage;
            var oldGameMode = _gameMode;
            var oldDifficulty = _aiPracticeDifficulty;
            var oldAiMode = _enableAiMode;
            SetLanguageLabel(payload.Room.PassageLanguage);
            _raceDurationSeconds = Math.Clamp(payload.Room.RaceDurationSeconds, Shared.Constants.MinRaceDurationSeconds, Shared.Constants.MaxRaceDurationSeconds);
            _enableAiMode = payload.Room.EnableAiMode;
            _hasCustomPassage = payload.Room.HasCustomPassage;
            _gameMode = Shared.Constants.NormalizeGameMode(payload.Room.GameMode);
            _aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(payload.Room.AiPracticeDifficulty);
            _lblSettings.Text = BuildRoomSettingsText(_passageLanguage, _raceDurationSeconds, _enableAiMode, _gameMode, _aiPracticeDifficulty, _hasCustomPassage);
            if (_isCommunityRoom && payload.Room.SecondsUntilNextStart > 0)
            {
                _lblStatus.Text = $"Quick Play cộng đồng: tự start sau {FormatShortTime(payload.Room.SecondsUntilNextStart)}. Ai vào giữa trận vẫn chơi từ đầu.";
                _lblStatus.ForeColor = ClientTheme.Accent;
            }
            if (oldLanguage != _passageLanguage ||
                oldGameMode != _gameMode ||
                oldDifficulty != _aiPracticeDifficulty ||
                oldAiMode != _enableAiMode)
            {
                ResetWarmupPrompt();
            }

            if (_isCommunityRoom)
            {
                _communityRoomStatus = (payload.Room.Status ?? "waiting").Trim().ToLowerInvariant();
                _communitySecondsUntilNextStart = Math.Max(0, payload.Room.SecondsUntilNextStart);
                UpdateCommunityStatusLabel();
            }
        }
        UpdatePlayerList(payload.Players);
    }

    private void OnPlayerJoined(NetworkMessage message)
    {
        var payload = message.GetPayload<PlayerJoinedPayload>();
        if (payload?.Player == null) return;

        _chatPanel.AddSystemMessage($"{payload.Player.Username} đã vào phòng");
    }

    private void OnPlayerLeft(NetworkMessage message)
    {
        var payload = message.GetPayload<PlayerLeftPayload>();
        if (payload == null) return;

        _chatPanel.AddSystemMessage($"{payload.Username} đã rời phòng");

        // Nếu mình được chuyển thành host
        if (payload.NewHostUserId.HasValue &&
            payload.NewHostUserId.Value == AppState.Instance.CurrentUser?.Id)
        {
            _isHost = true;
            _btnStart.Visible = !_isCommunityRoom;
            _chatPanel.AddSystemMessage("Bạn là host mới!");
        }
    }

    private void OnChatBroadcast(NetworkMessage message)
    {
        var payload = message.GetPayload<ChatBroadcastPayload>();
        if (payload?.Message != null)
        {
            _chatPanel.AddMessage(payload.Message);
        }
    }

    private void OnRaceCountdown(NetworkMessage message)
    {
        var payload = message.GetPayload<Shared.Payloads.Game.RaceCountdownPayload>();
        if (payload == null) return;

        StopCommunityCountdown();
        _lblStatus.Text = _isCommunityRoom
            ? $"Quick Play tự động bắt đầu sau {payload.SecondsRemaining}..."
            : $"Bắt đầu sau {payload.SecondsRemaining}...";
        _lblStatus.ForeColor = ClientTheme.Accent;
        _btnReady.Enabled = false;
        _btnStart.Enabled = false;
        _txtWarmup.Enabled = false;
        _btnWarmupNext.Enabled = false;
    }

    private void OnRaceStart(NetworkMessage message)
    {
        var payload = message.GetPayload<Shared.Payloads.Game.RaceStartPayload>();
        if (payload == null) return;

        // Mở form đua
        StopCommunityCountdown();
        _closingForRace = true;
        var raceDuration = Math.Max(30, payload.RaceDurationSeconds > 0 ? payload.RaceDurationSeconds : _raceDurationSeconds);
        var gameMode = Shared.Constants.NormalizeGameMode(payload.GameMode);
        var aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(payload.AiPracticeDifficulty);
        var raceForm = new RaceForm(_roomCode, payload.PassageText, _isHost, _passageLanguage, payload.PassageLanguage, raceDuration, _enableAiMode, gameMode, aiPracticeDifficulty, _hasCustomPassage, payload.RaceElapsedSeconds);
        raceForm.Show();
        Close(); // Đóng hẳn thay vì Hide — RaceForm sẽ tạo RoomForm mới khi xong
    }

    private void UpdatePlayerList(List<RoomPlayerDto> players)
    {
        _lstPlayers.Items.Clear();
        foreach (var p in players)
        {
            var status = p.IsReady ? " [Sẵn sàng]" : "";
            var host = p.IsHost ? " (Host)" : "";
            _lstPlayers.Items.Add($"{p.Username}{host}{status}");
        }
    }

    private void ReturnToMainForm()
    {
        _returnedToMain = true;
        var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (mainForm != null)
            mainForm.Show();
        else
            new MainForm().Show();
        Close();
    }

    private void OnError(NetworkMessage message)
    {
        var payload = message.GetPayload<Shared.Payloads.System.ErrorPayload>();
        if (payload != null)
        {
            _lblStatus.Text = payload.Message;
            _lblStatus.ForeColor = ClientTheme.Danger;
            _btnReady.Enabled = !_isCommunityRoom;
            _btnStart.Enabled = !_isCommunityRoom;
            _txtWarmup.Enabled = !_warmupCompleted;
            _btnWarmupNext.Enabled = true;
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        // Chỉ gửi LEAVE_ROOM khi user đóng bằng nút X (không phải khi race bắt đầu)
        StopCommunityCountdown();
        if (!_closingForRace && e.CloseReason == CloseReason.UserClosing && AppState.Instance.CurrentRoomCode != null)
        {
            try
            {
                await AppState.Instance.Client.SendAsync(MessageType.LEAVE_ROOM, new Shared.Payloads.Room.LeaveRoomRequest
                {
                    RoomCode = _roomCode,
                });
            }
            catch { }
            AppState.Instance.CurrentRoomCode = null;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopCommunityCountdown();
        var d = AppState.Instance.Dispatcher;
        d.OnRoomUpdate -= OnRoomUpdate;
        d.OnPlayerJoined -= OnPlayerJoined;
        d.OnPlayerLeft -= OnPlayerLeft;
        d.OnChatBroadcast -= OnChatBroadcast;
        d.OnRaceCountdown -= OnRaceCountdown;
        d.OnRaceStart -= OnRaceStart;
        d.OnError -= OnError;

        // Nếu đóng bằng nút X (không phải vì race bắt đầu hay leave button) → show MainForm
        if (!_closingForRace && !_returnedToMain)
        {
            var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (mainForm != null)
                mainForm.Show();
            else
                new MainForm().Show();
        }

        base.OnFormClosed(e);
    }

    private void SetLanguageLabel(string? languageCode)
    {
        _passageLanguage = NormalizeLanguage(languageCode);
        _lblLanguage.Text = $"Ngôn ngữ: {ToLanguageLabel(_passageLanguage)}";
        _lblSettings.Text = BuildRoomSettingsText(_passageLanguage, _raceDurationSeconds, _enableAiMode, _gameMode, _aiPracticeDifficulty, _hasCustomPassage);
    }

    private static string BuildRoomSettingsText(string passageLanguage, int durationSeconds, bool enableAiMode, string gameMode, string aiPracticeDifficulty, bool hasCustomPassage)
    {
        var seconds = Math.Clamp(durationSeconds, Shared.Constants.MinRaceDurationSeconds, Shared.Constants.MaxRaceDurationSeconds);
        var minutes = (int)Math.Ceiling(seconds / 60m);
        var normalizedMode = Shared.Constants.NormalizeGameMode(gameMode);
        var aiBotText = normalizedMode == Shared.Constants.GameModeAiPractice
            ? $" • Bot {ToAiPracticeDifficultyLabel(aiPracticeDifficulty)}"
            : string.Empty;
        var textMode = hasCustomPassage ? "Custom text" : ToLanguageLabel(passageLanguage);
        return $"{minutes} phút • {ToGameModeLabel(normalizedMode)}{aiBotText} • {textMode} • AI đề {(enableAiMode ? "BẬT" : "TẮT")}";
    }

    private void InitializeCommunityCountdownState()
    {
        if (!_isCommunityRoom)
            return;

        _communityCountdownTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000,
        };
        _communityCountdownTimer.Tick += (_, _) =>
        {
            if (_communitySecondsUntilNextStart > 0)
                _communitySecondsUntilNextStart--;
            UpdateCommunityStatusLabel();
        };
        UpdateCommunityStatusLabel();
    }

    private void UpdateCommunityStatusLabel()
    {
        if (!_isCommunityRoom)
            return;

        if (_communityRoomStatus == "racing")
        {
            StopCommunityCountdown();
            if (_lblCommunityCountdown != null)
            {
                _lblCommunityCountdown.Visible = true;
                _lblCommunityCountdown.Text = "QUICK PLAY: Tran dang dien ra. Nguoi moi van vao duoc va go tu dau.";
            }
            _lblStatus.Text = "Quick Play cong dong: tran dang dien ra, nguoi moi van vao duoc va go tu dau.";
            _lblStatus.ForeColor = ClientTheme.Accent;
            return;
        }

        if (_communitySecondsUntilNextStart > 0)
        {
            if (_communityCountdownTimer is { Enabled: false })
                _communityCountdownTimer.Start();

            if (_lblCommunityCountdown != null)
            {
                _lblCommunityCountdown.Visible = true;
                _lblCommunityCountdown.Text = $"QUICK PLAY: Tu dong bat dau sau {FormatShortTime(_communitySecondsUntilNextStart)}";
            }
            _lblStatus.Text = $"Quick Play cong dong: con {FormatShortTime(_communitySecondsUntilNextStart)} nua se tu bat dau.";
            _lblStatus.ForeColor = ClientTheme.Accent;
            return;
        }

        StopCommunityCountdown();
        if (_lblCommunityCountdown != null)
        {
            _lblCommunityCountdown.Visible = true;
            _lblCommunityCountdown.Text = "QUICK PLAY: Sap tu dong bat dau. Hay giu cua so phong cho.";
        }
        _lblStatus.Text = "Quick Play cong dong: sap tu bat dau, ban co the doi ngay trong phong.";
        _lblStatus.ForeColor = ClientTheme.Accent;
    }

    private void StopCommunityCountdown()
    {
        _communityCountdownTimer?.Stop();
    }

    private static string FormatShortTime(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:D2}:{seconds % 60:D2}";
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
            "any" => "Mọi ngôn ngữ",
            _ => "English",
        };
    }

    private static string ToGameModeLabel(string gameMode)
    {
        return Shared.Constants.NormalizeGameMode(gameMode) switch
        {
            Shared.Constants.GameModeAccuracy => "Accuracy Challenge",
            Shared.Constants.GameModeNoBackspace => "No Backspace",
            Shared.Constants.GameModeSuddenDeath => "Sudden Death",
            Shared.Constants.GameModeAiPractice => "AI Practice",
            _ => "Classic",
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

    private void ResetWarmupPrompt()
    {
        var prompts = BuildWarmupPromptBank();
        _warmupPrompt = prompts[Random.Shared.Next(prompts.Count)];
        _warmupLastText = string.Empty;
        _warmupStartedAt = DateTime.MinValue;
        _warmupKeystrokes = 0;
        _warmupMistakes = 0;
        _warmupCurrentStreak = 0;
        _warmupCompleted = false;

        _lblWarmupPrompt.Text = _warmupPrompt;
        _txtWarmup.TextChanged -= TxtWarmup_TextChanged;
        _txtWarmup.Clear();
        _txtWarmup.MaxLength = Math.Max(1, _warmupPrompt.Length);
        _txtWarmup.Enabled = true;
        _txtWarmup.BackColor = ClientTheme.Surface;
        _txtWarmup.TextChanged += TxtWarmup_TextChanged;
        _btnWarmupNext.Enabled = true;
        UpdateWarmupStats();
    }

    private void TxtWarmup_TextChanged(object? sender, EventArgs e)
    {
        var typed = _txtWarmup.Text;
        if (_warmupStartedAt == DateTime.MinValue && typed.Length > 0)
            _warmupStartedAt = DateTime.UtcNow;

        if (typed.Length > _warmupLastText.Length)
        {
            for (var i = _warmupLastText.Length; i < typed.Length; i++)
            {
                _warmupKeystrokes++;
                var expected = i < _warmupPrompt.Length ? _warmupPrompt[i] : '\0';
                if (typed[i] == expected)
                {
                    _warmupCurrentStreak++;
                    _warmupBestStreak = Math.Max(_warmupBestStreak, _warmupCurrentStreak);
                }
                else
                {
                    _warmupMistakes++;
                    _warmupCurrentStreak = 0;
                }
            }
        }

        _warmupLastText = typed;
        _txtWarmup.BackColor = _warmupPrompt.StartsWith(typed, StringComparison.Ordinal)
            ? ClientTheme.Surface
            : Color.FromArgb(255, 241, 242);

        if (!_warmupCompleted && typed == _warmupPrompt)
        {
            _warmupCompleted = true;
            _txtWarmup.Enabled = false;
            _txtWarmup.BackColor = Color.FromArgb(239, 253, 244);
        }

        UpdateWarmupStats();
    }

    private void UpdateWarmupStats()
    {
        var correctPrefix = CountCorrectPrefix(_txtWarmup.Text, _warmupPrompt);
        var elapsedSeconds = _warmupStartedAt == DateTime.MinValue
            ? 0
            : Math.Max(1, (DateTime.UtcNow - _warmupStartedAt).TotalSeconds);
        var rawWpm = elapsedSeconds <= 0
            ? 0
            : (_txtWarmup.TextLength / 5d) / (elapsedSeconds / 60d);
        var accuracy = _warmupKeystrokes == 0
            ? 100
            : Math.Max(0, (_warmupKeystrokes - _warmupMistakes) * 100d / _warmupKeystrokes);
        var status = _warmupCompleted
            ? "Hoàn thành"
            : $"Tiến độ {correctPrefix}/{_warmupPrompt.Length}";

        _lblWarmupStats.Text =
            $"{status} • Warm-up accuracy {accuracy:0}% • Accuracy streak {_warmupCurrentStreak}/{_warmupBestStreak} • Raw {rawWpm:0} WPM • lỗi đã ghi {_warmupMistakes}";
    }

    private List<string> BuildWarmupPromptBank()
    {
        var prompts = _passageLanguage == "vi"
            ? new List<string>
            {
                "gõ chắc từng phím rồi tăng tốc nhẹ",
                "giữ nhịp đều khi gặp dấu câu và chữ hoa",
                "đọc trước ba từ để không vội sửa lỗi",
                "ưu tiên chính xác trước khi bứt tốc",
                "tay trở về hàng cơ sở sau mỗi cụm chữ",
                "đừng bỏ qua khoảng trắng giữa các từ ngắn",
            }
            : new List<string>
            {
                "type steady words before chasing speed",
                "scan three words ahead and keep rhythm",
                "accuracy first then speed will follow",
                "reset your fingers to the home row",
                "watch spaces commas and capital letters",
                "slow down once then finish the line clean",
            };

        if (_gameMode == Shared.Constants.GameModeNoBackspace)
            prompts.Add(_passageLanguage == "vi" ? "no backspace cần chắc từng ký tự" : "no backspace rewards clean first presses");
        if (_gameMode == Shared.Constants.GameModeSuddenDeath)
            prompts.Add(_passageLanguage == "vi" ? "sudden death không tha lỗi đầu tiên" : "sudden death punishes the first mistake");
        if (_gameMode == Shared.Constants.GameModeAccuracy)
            prompts.Add(_passageLanguage == "vi" ? "accuracy challenge tính từng phím sai" : "accuracy challenge counts every slip");
        if (_gameMode == Shared.Constants.GameModeAiPractice || _enableAiMode)
            prompts.Add(_passageLanguage == "vi" ? "AI sẽ biến lỗi thật thành bài luyện mới" : "AI turns real mistakes into a new drill");

        return prompts;
    }

    private static int CountCorrectPrefix(string typed, string target)
    {
        var limit = Math.Min(typed.Length, target.Length);
        for (var i = 0; i < limit; i++)
        {
            if (typed[i] != target[i])
                return i;
        }
        return limit;
    }
}
