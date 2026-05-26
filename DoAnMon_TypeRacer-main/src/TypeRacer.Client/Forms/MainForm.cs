using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Room;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Màn hình chính sau khi đăng nhập: danh sách phòng, tạo phòng, xem profile, leaderboard.
/// </summary>
public class MainForm : Form
{
    private DataGridView _dgvRooms = null!;
    private Button _btnCreateRoom = null!;
    private Button _btnQuickPlay = null!;
    private Button _btnJoinRoom = null!;
    private Button _btnRefresh = null!;
    private Button _btnLogout = null!;
    private Label _lblWelcome = null!;
    private TextBox _txtRoomCode = null!;
    private ComboBox _cmbLanguage = null!;
    private NumericUpDown _numRaceMinutes = null!;
    private ComboBox _cmbGameMode = null!;
    private ComboBox _cmbAiPracticeDifficulty = null!;
    private CheckBox _chkEnableAiMode = null!;
    private CheckBox _chkCustomText = null!;
    private TextBox _txtCustomPassage = null!;
    private Button _btnSoloPractice = null!;
    private Button _btnFingerPractice = null!;
    private Label _lblModeDescription = null!;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Sảnh chính";
        Size = new Size(1240, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ClientTheme.BackgroundTop;
        MinimumSize = new Size(1180, 780);
        AutoScaleMode = AutoScaleMode.Dpi;

        var page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120f,
            Padding = new Padding(24),
        };

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 560));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 135f,
            CornerRadius = 22,
            Padding = new Padding(24, 22, 24, 22),
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

        var titleWrap = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };
        titleWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        titleWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "TypeRacer Lobby",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 21f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblWelcome = new Label
        {
            Text = $"Xin chào, {AppState.Instance.CurrentUser?.Username ?? "Player"}! Chọn phòng phù hợp để bắt đầu đua.",
            ForeColor = Color.FromArgb(212, 225, 248),
            Font = new Font("Segoe UI", 10.5f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
        };

        _btnLogout = new Button
        {
            Text = "Đăng xuất",
            Size = new Size(126, 44),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(12, 12, 0, 12),
        };
        ClientTheme.StyleButton(_btnLogout, ThemeButtonVariant.Danger);
        _btnLogout.Click += BtnLogout_Click;

        titleWrap.Controls.Add(lblTitle, 0, 0);
        titleWrap.Controls.Add(_lblWelcome, 0, 1);

        headerLayout.Controls.Add(titleWrap, 0, 0);
        headerLayout.Controls.Add(_btnLogout, 1, 0);
        headerCard.Controls.Add(headerLayout);

        var actionCard = ClientTheme.CreateCard(new Padding(18, 14, 18, 14));
        actionCard.Margin = new Padding(0, 0, 0, 14);

        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
        };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblActions = new Label
        {
            Text = "Tạo phòng mới hoặc nhập mã để tham gia nhanh",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var settingsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _btnCreateRoom = new Button
        {
            Text = "Tạo phòng mới",
            Size = new Size(196, 48),
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnCreateRoom, ThemeButtonVariant.Success);
        _btnCreateRoom.Click += BtnCreateRoom_Click;

        var lblLanguage = new Label
        {
            Text = "Ngôn ngữ bài:",
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _cmbLanguage = new ComboBox
        {
            Width = 185,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 6, 14, 6),
        };
        _cmbLanguage.Items.AddRange(new object[]
        {
            "English",
            "Tiếng Việt",
            "Mọi ngôn ngữ",
        });
        _cmbLanguage.SelectedIndex = 1;
        ClientTheme.StyleComboBox(_cmbLanguage);

        var lblRaceTime = new Label
        {
            Text = "Thời gian (phút):",
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _numRaceMinutes = new NumericUpDown
        {
            Width = 82,
            Minimum = 1,
            Maximum = 20,
            Value = 3,
            DecimalPlaces = 0,
            Increment = 1,
            Margin = new Padding(0, 6, 14, 6),
        };

        var lblGameMode = new Label
        {
            Text = "Chế độ:",
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _cmbGameMode = new ComboBox
        {
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 6, 14, 6),
        };
        _cmbGameMode.Items.AddRange(new object[]
        {
            "Classic",
            "Accuracy Challenge",
            "No Backspace",
            "Sudden Death",
            "AI Practice",
        });
        _cmbGameMode.SelectedIndex = 0;
        _cmbGameMode.SelectedIndexChanged += (_, _) =>
        {
            UpdateModeDescription();
            UpdateAiPracticeDifficultyState();
        };
        ClientTheme.StyleComboBox(_cmbGameMode);

        var lblAiPracticeDifficulty = new Label
        {
            Text = "Cấp AI:",
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _cmbAiPracticeDifficulty = new ComboBox
        {
            Width = 165,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 6, 14, 6),
        };
        _cmbAiPracticeDifficulty.Items.AddRange(new object[]
        {
            $"Dễ - {Shared.Constants.AiPracticeEasyRpm} RPM",
            $"Vừa - {Shared.Constants.AiPracticeMediumRpm} RPM",
            $"Khó - {Shared.Constants.AiPracticeHardRpm} RPM",
            $"Ác mộng - {Shared.Constants.AiPracticeNightmareRpm} RPM",
        });
        _cmbAiPracticeDifficulty.SelectedIndex = 0;
        _cmbAiPracticeDifficulty.SelectedIndexChanged += (_, _) => UpdateModeDescription();
        ClientTheme.StyleComboBox(_cmbAiPracticeDifficulty);

        _lblModeDescription = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = ClientTheme.TextMuted,
            Padding = new Padding(2, 8, 0, 4),
        };

        _chkEnableAiMode = new CheckBox
        {
            Text = "AI tạo đề luyện lỗi",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
        };

        settingsFlow.Controls.AddRange(new Control[]
        {
            _btnCreateRoom,
            CreateActionField("Ngôn ngữ bài", _cmbLanguage, 190),
            CreateActionField("Thời gian", _numRaceMinutes, 126),
            CreateActionField("Chế độ", _cmbGameMode, 190),
            CreateActionField("Cấp AI", _cmbAiPracticeDifficulty, 174),
        });

        var quickFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _btnSoloPractice = new Button
        {
            Text = "Luyện solo",
            Size = new Size(156, 48),
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnSoloPractice, ThemeButtonVariant.Accent);
        _btnSoloPractice.Click += BtnSoloPractice_Click;

        _btnFingerPractice = new Button
        {
            Text = "Luyện phím/finger",
            Size = new Size(188, 48),
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnFingerPractice, ThemeButtonVariant.Success);
        _btnFingerPractice.Click += BtnFingerPractice_Click;

        _btnQuickPlay = new Button
        {
            Text = "Quick Play cộng đồng",
            Size = new Size(236, 48),
            Margin = new Padding(0, 0, 12, 6),
        };
        ClientTheme.StyleButton(_btnQuickPlay, ThemeButtonVariant.Primary);
        _btnQuickPlay.Click += BtnQuickPlay_Click;

        var lblJoin = new Label
        {
            Text = "Mã phòng:",
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _txtRoomCode = new TextBox
        {
            Width = 132,
            CharacterCasing = CharacterCasing.Upper,
            MaxLength = 6,
            PlaceholderText = "ABC123",
            Margin = new Padding(0, 6, 10, 6),
        };
        ClientTheme.StyleTextBox(_txtRoomCode);

        _btnJoinRoom = new Button
        {
            Text = "Vào phòng",
            Size = new Size(136, 48),
            Margin = new Padding(0, 0, 10, 6),
        };
        ClientTheme.StyleButton(_btnJoinRoom, ThemeButtonVariant.Primary);
        _btnJoinRoom.Click += BtnJoinRoom_Click;

        _btnRefresh = new Button
        {
            Text = "Làm mới",
            Size = new Size(122, 48),
            Margin = new Padding(0, 0, 0, 6),
        };
        ClientTheme.StyleButton(_btnRefresh, ThemeButtonVariant.Neutral);
        _btnRefresh.Click += BtnRefresh_Click;

        quickFlow.Controls.AddRange(new Control[]
        {
            _chkEnableAiMode,
            _btnQuickPlay,
            _btnSoloPractice,
            _btnFingerPractice,
            CreateActionField("Mã phòng", _txtRoomCode, 132),
            _btnJoinRoom,
            _btnRefresh,
        });

        var customLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _chkCustomText = new CheckBox
        {
            Text = "Text tùy chỉnh",
            Checked = false,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0, 12, 10, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
        };
        _chkCustomText.CheckedChanged += (_, _) => UpdateCustomTextState();

        _txtCustomPassage = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "Dán quote, code ngắn, đoạn tiếng Việt/English để cả phòng đua cùng đoạn này...",
            Enabled = false,
            MaxLength = 1000,
            MinimumSize = new Size(0, 124),
        };
        ClientTheme.StyleTextBox(_txtCustomPassage);

        customLayout.Controls.Add(_chkCustomText, 0, 0);
        customLayout.Controls.Add(_txtCustomPassage, 1, 0);

        actionLayout.Controls.Add(lblActions, 0, 0);
        actionLayout.Controls.Add(settingsFlow, 0, 1);
        actionLayout.Controls.Add(quickFlow, 0, 2);
        actionLayout.Controls.Add(customLayout, 0, 3);
        actionLayout.Controls.Add(_lblModeDescription, 0, 4);
        actionCard.Controls.Add(actionLayout);
        UpdateModeDescription();
        UpdateAiPracticeDifficultyState();
        UpdateCustomTextState();

        var roomListCard = ClientTheme.CreateCard(new Padding(18, 14, 18, 18));
        var roomListLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };
        roomListLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        roomListLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblRoomList = new Label
        {
            Text = "Danh sách phòng đang mở",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _dgvRooms = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        ClientTheme.StyleDataGridView(_dgvRooms);

        _dgvRooms.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "RoomCode", HeaderText = "Mã phòng", FillWeight = 18 },
            new DataGridViewTextBoxColumn { Name = "Host", HeaderText = "Chủ phòng", FillWeight = 36 },
            new DataGridViewTextBoxColumn { Name = "Language", HeaderText = "Ngôn ngữ", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", FillWeight = 26 },
            new DataGridViewTextBoxColumn { Name = "Players", HeaderText = "Người chơi", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Duration", HeaderText = "Thời gian", FillWeight = 16 },
            new DataGridViewTextBoxColumn { Name = "AiMode", HeaderText = "AI", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "TextMode", HeaderText = "Text", FillWeight = 14 },
        });
        _dgvRooms.CellDoubleClick += DgvRooms_CellDoubleClick;

        roomListLayout.Controls.Add(lblRoomList, 0, 0);
        roomListLayout.Controls.Add(_dgvRooms, 0, 1);
        roomListCard.Controls.Add(roomListLayout);

        contentLayout.Controls.Add(headerCard, 0, 0);
        contentLayout.Controls.Add(actionCard, 0, 1);
        contentLayout.Controls.Add(roomListCard, 0, 2);

        page.Controls.Add(contentLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 1180));

        Load += MainForm_Load;
    }

    private static Control CreateActionField(string labelText, Control input, int width)
    {
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0);

        var layout = new TableLayoutPanel
        {
            Width = width,
            Height = 66,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 14, 6),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(input, 0, 1);
        return layout;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var dispatcher = AppState.Instance.Dispatcher;
        dispatcher.OnRoomListResponse += OnRoomListResponse;
        dispatcher.OnCreateRoomResponse += OnCreateRoomResponse;
        dispatcher.OnJoinRoomResponse += OnJoinRoomResponse;
        dispatcher.OnDisconnect += OnDisconnect;

        // Re-enable buttons khi form hiện lại (sau khi quay về từ phòng)
        VisibleChanged += (s2, e2) =>
        {
            if (Visible)
            {
                _btnCreateRoom.Enabled = true;
                _btnQuickPlay.Enabled = true;
                _btnJoinRoom.Enabled = true;
                RefreshRoomList();
            }
        };

        // Tải danh sách phòng
        RefreshRoomList();
    }

    private async void RefreshRoomList()
    {
        try
        {
            await AppState.Instance.Client.SendEmptyAsync(MessageType.ROOM_LIST_REQUEST);
        }
        catch { }
    }

    private void OnRoomListResponse(NetworkMessage message)
    {
        var response = message.GetPayload<RoomListResponse>();
        if (response == null) return;

        _dgvRooms.Rows.Clear();
        foreach (var room in response.Rooms)
        {
            _dgvRooms.Rows.Add(
                room.RoomCode,
                room.HostUsername,
                ToLanguageDisplay(room.PassageLanguage),
                ToGameModeDisplay(room.GameMode, room.AiPracticeDifficulty),
                ToRoomStatusDisplay(room),
                room.CurrentPlayers,
                ToDurationDisplay(room.RaceDurationSeconds),
                room.EnableAiMode ? "Bật" : "Tắt",
                room.HasCustomPassage ? "Custom" : "DB"
            );
        }
    }

    private async void BtnCreateRoom_Click(object? sender, EventArgs e)
    {
        _btnCreateRoom.Enabled = false;
        _btnQuickPlay.Enabled = false;
        _btnJoinRoom.Enabled = false;
        try
        {
            var customPassage = GetCustomPassageForRoom();
            if (_chkCustomText.Checked && string.IsNullOrWhiteSpace(customPassage))
            {
                MessageBox.Show("Text tùy chỉnh cần ít nhất 40 ký tự sau khi chuẩn hóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _btnCreateRoom.Enabled = true;
                _btnQuickPlay.Enabled = true;
                _btnJoinRoom.Enabled = true;
                return;
            }

            await AppState.Instance.Client.SendAsync(MessageType.CREATE_ROOM, new CreateRoomRequest
            {
                PassageLanguage = GetSelectedLanguageCode(),
                RaceDurationSeconds = GetRaceDurationSecondsFromInput(),
                EnableAiMode = _chkEnableAiMode.Checked,
                GameMode = GetSelectedGameModeCode(),
                AiPracticeDifficulty = GetSelectedAiPracticeDifficultyCode(),
                CustomPassageText = customPassage,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnCreateRoom.Enabled = true;
            _btnQuickPlay.Enabled = true;
            _btnJoinRoom.Enabled = true;
        }
    }

    private void OnCreateRoomResponse(NetworkMessage message)
    {
        var response = message.GetPayload<CreateRoomResponse>();
        if (response == null) return;

        if (response.Success && response.RoomCode != null)
        {
            AppState.Instance.CurrentRoomCode = response.RoomCode;
            var room = response.Room;
            var roomLanguage = room?.PassageLanguage ?? GetSelectedLanguageCode();
            var roomDuration = room?.RaceDurationSeconds ?? GetRaceDurationSecondsFromInput();
            var roomAiMode = room?.EnableAiMode ?? _chkEnableAiMode.Checked;
            var roomGameMode = room?.GameMode ?? GetSelectedGameModeCode();
            var roomAiDifficulty = room?.AiPracticeDifficulty ?? GetSelectedAiPracticeDifficultyCode();
            var roomHasCustomText = room?.HasCustomPassage ?? !string.IsNullOrWhiteSpace(GetCustomPassageForRoom());
            var roomForm = new RoomForm(response.RoomCode, isHost: true, roomLanguage, roomDuration, roomAiMode, roomGameMode, roomAiDifficulty, roomHasCustomText);
            roomForm.Show();
            Hide();
        }
        else
        {
            MessageBox.Show(response.ErrorMessage ?? "Không thể tạo phòng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnCreateRoom.Enabled = true;
            _btnQuickPlay.Enabled = true;
            _btnJoinRoom.Enabled = true;
        }
    }

    private void BtnQuickPlay_Click(object? sender, EventArgs e)
    {
        _txtRoomCode.Text = Shared.Constants.CommunityRoomCode;
        BtnJoinRoom_Click(sender, e);
    }

    private async void BtnJoinRoom_Click(object? sender, EventArgs e)
    {
        var roomCode = _txtRoomCode.Text.Trim().ToUpper();
        if (string.IsNullOrEmpty(roomCode))
        {
            MessageBox.Show("Vui lòng nhập mã phòng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _btnCreateRoom.Enabled = false;
        _btnQuickPlay.Enabled = false;
        _btnJoinRoom.Enabled = false;
        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.JOIN_ROOM, new JoinRoomRequest
            {
                RoomCode = roomCode,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnCreateRoom.Enabled = true;
            _btnQuickPlay.Enabled = true;
            _btnJoinRoom.Enabled = true;
        }
    }

    private void OnJoinRoomResponse(NetworkMessage message)
    {
        var response = message.GetPayload<JoinRoomResponse>();
        if (response == null) return;

        if (response.Success && response.Room != null)
        {
            AppState.Instance.CurrentRoomCode = response.Room.RoomCode;
            var isHost = response.Room.HostUserId == AppState.Instance.CurrentUser?.Id;
            if (response.RaceInProgress && response.CurrentRace != null)
            {
                var payload = response.CurrentRace;
                var raceDuration = Math.Max(30, payload.RaceDurationSeconds > 0 ? payload.RaceDurationSeconds : response.Room.RaceDurationSeconds);
                var raceForm = new RaceForm(
                    response.Room.RoomCode,
                    payload.PassageText,
                    isHost,
                    response.Room.PassageLanguage,
                    payload.PassageLanguage,
                    raceDuration,
                    response.Room.EnableAiMode,
                    payload.GameMode,
                    payload.AiPracticeDifficulty,
                    response.Room.HasCustomPassage,
                    payload.RaceElapsedSeconds);
                raceForm.Show();
                Hide();
                return;
            }

            var roomForm = new RoomForm(
                response.Room.RoomCode,
                isHost,
                response.Room.PassageLanguage,
                response.Room.RaceDurationSeconds,
                response.Room.EnableAiMode,
                response.Room.GameMode,
                response.Room.AiPracticeDifficulty,
                response.Room.HasCustomPassage,
                response.Room.SecondsUntilNextStart,
                response.Room.Status);
            roomForm.Show();
            Hide();
        }
        else
        {
            MessageBox.Show(response.ErrorMessage ?? "Không thể vào phòng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnCreateRoom.Enabled = true;
            _btnQuickPlay.Enabled = true;
            _btnJoinRoom.Enabled = true;
        }
    }

    private void DgvRooms_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var roomCode = _dgvRooms.Rows[e.RowIndex].Cells["RoomCode"].Value?.ToString();
        if (!string.IsNullOrEmpty(roomCode))
        {
            _txtRoomCode.Text = roomCode;
            BtnJoinRoom_Click(sender, e);
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e) => RefreshRoomList();

    private void BtnSoloPractice_Click(object? sender, EventArgs e)
    {
        var practiceText = GetCustomPassageForRoom();
        if (_chkCustomText.Checked && string.IsNullOrWhiteSpace(practiceText))
        {
            MessageBox.Show("Text tùy chỉnh cần ít nhất 40 ký tự để luyện solo.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(practiceText))
        {
            practiceText = GetDefaultSoloPracticeText(GetSelectedLanguageCode());
        }

        using var practiceForm = new PracticeForm(practiceText, GetSelectedLanguageCode() == "en" ? "en" : "vi");
        practiceForm.ShowDialog(this);
    }

    private void BtnFingerPractice_Click(object? sender, EventArgs e)
    {
        using var practiceForm = new FingerPracticeForm();
        practiceForm.ShowDialog(this);
    }

    private void BtnLogout_Click(object? sender, EventArgs e)
    {
        AppState.Instance.Logout();

        var loginForm = Application.OpenForms.OfType<LoginForm>().FirstOrDefault();
        if (loginForm != null)
        {
            loginForm.Show();
        }
        else
        {
            new LoginForm().Show();
        }
        Close();
    }

    private void OnDisconnect(NetworkMessage message)
    {
        MessageBox.Show("Mất kết nối tới server.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        BtnLogout_Click(null, EventArgs.Empty);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        var dispatcher = AppState.Instance.Dispatcher;
        dispatcher.OnRoomListResponse -= OnRoomListResponse;
        dispatcher.OnCreateRoomResponse -= OnCreateRoomResponse;
        dispatcher.OnJoinRoomResponse -= OnJoinRoomResponse;
        dispatcher.OnDisconnect -= OnDisconnect;
        base.OnFormClosed(e);
    }

    private string GetSelectedLanguageCode()
    {
        var selected = _cmbLanguage.SelectedItem?.ToString() ?? "English";
        return selected switch
        {
            "Tiếng Việt" => "vi",
            "Mọi ngôn ngữ" => "any",
            _ => "en",
        };
    }

    private string GetSelectedGameModeCode()
    {
        var selected = _cmbGameMode.SelectedItem?.ToString() ?? "Classic";
        return selected switch
        {
            "Accuracy Challenge" => Shared.Constants.GameModeAccuracy,
            "No Backspace" => Shared.Constants.GameModeNoBackspace,
            "Sudden Death" => Shared.Constants.GameModeSuddenDeath,
            "AI Practice" => Shared.Constants.GameModeAiPractice,
            _ => Shared.Constants.GameModeClassic,
        };
    }

    private string GetSelectedAiPracticeDifficultyCode()
    {
        return _cmbAiPracticeDifficulty.SelectedIndex switch
        {
            1 => Shared.Constants.AiPracticeMedium,
            2 => Shared.Constants.AiPracticeHard,
            3 => Shared.Constants.AiPracticeNightmare,
            _ => Shared.Constants.AiPracticeEasy,
        };
    }

    private void UpdateModeDescription()
    {
        _lblModeDescription.Text = GetSelectedGameModeCode() switch
        {
            Shared.Constants.GameModeAccuracy => "Accuracy Challenge: ưu tiên xếp hạng theo độ chính xác, rồi mới tới tốc độ.",
            Shared.Constants.GameModeNoBackspace => "No Backspace: dùng Backspace/Delete là bị loại, phù hợp demo kỷ luật gõ.",
            Shared.Constants.GameModeSuddenDeath => "Sudden Death: sai một ký tự là bị loại ngay, tạo cảm giác thử thách rõ ràng.",
            Shared.Constants.GameModeAiPractice => $"AI Practice: luyện với bot {ToAiPracticeDifficultyDisplay(GetSelectedAiPracticeDifficultyCode())}, RPM tăng theo cấp độ.",
            _ => "Classic: đua tốc độ truyền thống, phù hợp người mới và test nhanh.",
        };
    }

    private void UpdateAiPracticeDifficultyState()
    {
        var isAiPractice = GetSelectedGameModeCode() == Shared.Constants.GameModeAiPractice;
        _cmbAiPracticeDifficulty.Enabled = isAiPractice;
        _cmbAiPracticeDifficulty.BackColor = isAiPractice ? Color.White : Color.FromArgb(238, 242, 248);
    }

    private void UpdateCustomTextState()
    {
        if (_txtCustomPassage == null || _chkCustomText == null)
            return;

        _txtCustomPassage.Enabled = _chkCustomText.Checked;
        _txtCustomPassage.BackColor = _chkCustomText.Checked ? Color.White : Color.FromArgb(238, 242, 248);
    }

    private string GetCustomPassageForRoom()
    {
        if (_chkCustomText == null || !_chkCustomText.Checked || _txtCustomPassage == null)
            return string.Empty;

        var normalized = string.Join(" ", _txtCustomPassage.Text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim();

        if (normalized.Length < 40)
            return string.Empty;

        return normalized.Length > 1000 ? normalized[..1000].TrimEnd() : normalized;
    }

    private static string GetDefaultSoloPracticeText(string language)
    {
        return language == "en"
            ? "Focused solo practice starts with a calm rhythm, clean spacing, and steady recovery after each mistake."
            : "Luyện solo hiệu quả bắt đầu bằng nhịp gõ bình tĩnh, khoảng trắng chuẩn và phục hồi đều sau mỗi lỗi.";
    }

    private static string ToLanguageDisplay(string? code)
    {
        return (code ?? "en").Trim().ToLowerInvariant() switch
        {
            "vi" => "Tiếng Việt",
            "any" => "Mọi ngôn ngữ",
            _ => "English",
        };
    }

    private int GetRaceDurationSecondsFromInput()
    {
        var minutes = (int)_numRaceMinutes.Value;
        return Math.Clamp(minutes * 60, 30, 1200);
    }

    private static string ToDurationDisplay(int durationSeconds)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(durationSeconds / 60m));
        return $"{minutes} phút";
    }

    private static string ToGameModeDisplay(string? mode, string? aiPracticeDifficulty = null)
    {
        return Shared.Constants.NormalizeGameMode(mode) switch
        {
            Shared.Constants.GameModeAccuracy => "Accuracy",
            Shared.Constants.GameModeNoBackspace => "No Backspace",
            Shared.Constants.GameModeSuddenDeath => "Sudden Death",
            Shared.Constants.GameModeAiPractice => $"AI {ToAiPracticeDifficultyDisplay(aiPracticeDifficulty)}",
            _ => "Classic",
        };
    }

    private static string ToRoomStatusDisplay(RoomDto room)
    {
        if (room.IsCommunityRoom)
        {
            if (string.Equals(room.Status, "racing", StringComparison.OrdinalIgnoreCase))
                return "Đang đua - vào được";

            return room.SecondsUntilNextStart > 0
                ? $"Tự start {FormatShortTime(room.SecondsUntilNextStart)}"
                : "Sắp tự start";
        }

        return string.Equals(room.Status, "racing", StringComparison.OrdinalIgnoreCase)
            ? "Đang đua"
            : "Đang chờ";
    }

    private static string FormatShortTime(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:D2}:{seconds % 60:D2}";
    }

    private static string ToAiPracticeDifficultyDisplay(string? difficulty)
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
}
