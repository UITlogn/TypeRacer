using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Ai;
using TypeRacer.Shared.Payloads.Game;
using TypeRacer.Shared.Payloads.System;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form kết quả sau mỗi trận đua: xếp hạng, WPM, accuracy, thời gian.
/// </summary>
public class ResultForm : Form
{
    private readonly RaceResultPayload _result;
    private readonly RaceResultDto? _myResult;
    private readonly string _passageText;
    private readonly string _passageLanguage;
    private readonly string _gameMode;
    private readonly string _aiPracticeDifficulty;
    private readonly string _myTypedText;
    private readonly int _raceDurationSeconds;
    private readonly List<TypingPerformanceSample> _performanceSamples;
    private readonly DailyChallengeProgressSnapshot? _dailyChallengeProgress;
    private readonly PersonalBestProgressSnapshot? _personalBestProgress;
    private readonly KeyboardMasteryProgressSnapshot? _keyboardMasteryProgress;

    private Button _btnAiAnalyze = null!;
    private Button _btnPractice = null!;
    private Button _btnAiMissionPractice = null!;
    private Button _btnAiPractice = null!;
    private Button _btnExportReport = null!;
    private Button _btnExportData = null!;
    private Button _btnCopyScore = null!;
    private Label _lblAiStatus = null!;
    private TypingCertificateControl _certificateCard = null!;
    private PerformanceTimelineControl _performanceTimeline = null!;
    private AiCoachSnapshotControl _aiSnapshot = null!;
    private RichTextBox _txtAi = null!;
    private KeyboardHeatmapControl _keyboardHeatmap = null!;
    private ListBox _lstAiMissions = null!;
    private ListBox _lstAiPassages = null!;
    private readonly List<AiPracticeMissionDto> _aiMissions = new();
    private readonly List<string> _aiPassages = new();
    private bool _awaitingAi;
    private AiPracticeMissionDto? _selectedAiMission;
    private string? _selectedAiPassage;

    public ResultForm(RaceResultPayload result, string passageText, string passageLanguage, int raceDurationSeconds = Shared.Constants.DefaultRaceDurationSeconds, string gameMode = Shared.Constants.DefaultGameMode, string aiPracticeDifficulty = Shared.Constants.DefaultAiPracticeDifficulty, IEnumerable<TypingPerformanceSample>? performanceSamples = null)
    {
        _result = result;
        _passageText = passageText ?? string.Empty;
        _passageLanguage = NormalizeLanguage(passageLanguage);
        _gameMode = Shared.Constants.NormalizeGameMode(gameMode);
        _aiPracticeDifficulty = Shared.Constants.NormalizeAiPracticeDifficulty(aiPracticeDifficulty);
        _raceDurationSeconds = Math.Clamp(raceDurationSeconds, Shared.Constants.MinRaceDurationSeconds, Shared.Constants.MaxRaceDurationSeconds);
        _performanceSamples = performanceSamples?.ToList() ?? new List<TypingPerformanceSample>();
        var myUserId = AppState.Instance.CurrentUser?.Id ?? 0;
        _myResult = _result.Results.FirstOrDefault(r => r.UserId == myUserId);
        _myTypedText = _myResult?.TypedText ?? string.Empty;
        if (_myResult != null)
        {
            _dailyChallengeProgress = DailyChallengeProgressStore.RecordRace(
                _myResult.UserId,
                _myResult.Username,
                _result.RaceId,
                _myResult,
                Math.Max(1, _result.Results.Count));
            _personalBestProgress = PersonalBestProgressStore.RecordRace(
                _myResult.UserId,
                _myResult.Username,
                _gameMode,
                _result.RaceId,
                _myResult);
            _keyboardMasteryProgress = KeyboardMasteryProgressStore.RecordRace(
                _myResult.UserId,
                _myResult.Username,
                _result.RaceId,
                _myResult);
        }
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Kết quả";
        Size = new Size(1120, 800);
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
            Angle = 120,
            Padding = new Padding(22),
        };

        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            BackColor = Color.Transparent,
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 540));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 740));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 140,
            CornerRadius = 22,
            Padding = new Padding(20, 14, 20, 12),
            Margin = new Padding(0, 0, 0, 14),
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "Kết quả trận đua",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var badgePreview = _myResult?.Achievements.Count > 0
            ? string.Join(" • ", _myResult.Achievements)
            : "chưa có badge";

        var lblSubtitle = new Label
        {
            Text = $"Chế độ: {ToGameModeLabel(_gameMode, _aiPracticeDifficulty)} | Badge: {badgePreview} | AI Coach sẵn sàng phân tích lỗi",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(202, 219, 249),
            Font = new Font("Segoe UI", 10.5f),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        headerLayout.Controls.Add(lblTitle, 0, 0);
        headerLayout.Controls.Add(lblSubtitle, 0, 1);
        headerCard.Controls.Add(headerLayout);

        var summaryCard = ClientTheme.CreateCard(new Padding(16));
        summaryCard.Margin = new Padding(0, 0, 0, 14);

        if (_myResult != null)
        {
            var posText = _myResult.Position switch
            {
                1 => "Hạng 1 - Chiến thắng!",
                2 => "Hạng 2",
                3 => "Hạng 3",
                _ => $"Hạng {_myResult.Position}",
            };

            var posColor = _myResult.Position switch
            {
                1 => Color.FromArgb(241, 196, 15),
                2 => Color.FromArgb(149, 165, 166),
                3 => Color.FromArgb(205, 127, 50),
                _ => ClientTheme.TextPrimary,
            };

            var summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                BackColor = Color.Transparent,
            };
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblRank = new Label
            {
                Text = posText,
                ForeColor = posColor,
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var lblRankSub = new Label
            {
                Text = "Vị trí của bạn",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
            };

            var timeSpan = TimeSpan.FromMilliseconds(_myResult.TimeTakenMs);
            var lblWpm = new Label
            {
                Text = $"{_myResult.Wpm:F1}",
                ForeColor = ClientTheme.TextPrimary,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblWpmSub = new Label
            {
                Text = "WPM",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
            };

            var lblAcc = new Label
            {
                Text = $"{_myResult.Accuracy:F1}%",
                ForeColor = ClientTheme.TextPrimary,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblAccSub = new Label
            {
                Text = "Chính xác",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
            };

            var lblTime = new Label
            {
                Text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}",
                ForeColor = ClientTheme.TextPrimary,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblTimeSub = new Label
            {
                Text = "Thời gian",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
            };

            var lblStreak = new Label
            {
                Text = $"{_myResult.BestStreak}",
                ForeColor = ClientTheme.TextPrimary,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblStreakSub = new Label
            {
                Text = "Combo tốt nhất",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
            };

            var lblScore = new Label
            {
                Text = $"{_myResult.ConsistencyScore:F1}",
                ForeColor = ClientTheme.TextPrimary,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblScoreSub = new Label
            {
                Text = "Ổn định",
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
            };

            summaryLayout.Controls.Add(lblRank, 0, 0);
            summaryLayout.Controls.Add(lblRankSub, 0, 1);
            summaryLayout.Controls.Add(lblWpm, 1, 0);
            summaryLayout.Controls.Add(lblWpmSub, 1, 1);
            summaryLayout.Controls.Add(lblAcc, 2, 0);
            summaryLayout.Controls.Add(lblAccSub, 2, 1);
            summaryLayout.Controls.Add(lblTime, 3, 0);
            summaryLayout.Controls.Add(lblTimeSub, 3, 1);
            summaryLayout.Controls.Add(lblStreak, 4, 0);
            summaryLayout.Controls.Add(lblStreakSub, 4, 1);
            summaryLayout.Controls.Add(lblScore, 5, 0);
            summaryLayout.Controls.Add(lblScoreSub, 5, 1);

            summaryCard.Controls.Add(summaryLayout);
        }
        else
        {
            var lblNoStats = new Label
            {
                Text = "Không tìm thấy dữ liệu kết quả của người chơi hiện tại.",
                Dock = DockStyle.Fill,
                ForeColor = ClientTheme.TextMuted,
                Font = new Font("Segoe UI", 11f),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            summaryCard.Controls.Add(lblNoStats);
        }

        var progressCards = BuildProgressCards();
        progressCards.Margin = new Padding(0, 0, 0, 14);

        var boardCard = ClientTheme.CreateCard(new Padding(16));
        boardCard.Margin = new Padding(0, 0, 0, 14);

        var certificateShell = ClientTheme.CreateCard(new Padding(10));
        certificateShell.Margin = new Padding(0, 0, 0, 14);
        _certificateCard = new TypingCertificateControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        _certificateCard.SetData(_myResult, _result.RoomCode, _result.RaceId, Math.Max(1, _result.Results.Count));
        certificateShell.Controls.Add(_certificateCard);

        var performanceCard = ClientTheme.CreateCard(new Padding(10));
        performanceCard.Margin = new Padding(0, 0, 0, 14);
        _performanceTimeline = new PerformanceTimelineControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        _performanceTimeline.SetData(_performanceSamples, _myResult, _raceDurationSeconds);
        performanceCard.Controls.Add(_performanceTimeline);

        var boardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        boardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        boardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblBoard = new Label
        {
            Text = "Bảng xếp hạng vòng đua",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        ClientTheme.StyleDataGridView(dgv);

        dgv.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Pos", HeaderText = "Hạng", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Người chơi", FillWeight = 24 },
            new DataGridViewTextBoxColumn { Name = "Wpm", HeaderText = "WPM", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Acc", HeaderText = "Chính xác", FillWeight = 16 },
            new DataGridViewTextBoxColumn { Name = "Combo", HeaderText = "Combo", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "Ổn định", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Thời gian", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Badges", HeaderText = "Badge", FillWeight = 26 },
        });

        foreach (var r in _result.Results.OrderBy(x => x.Position))
        {
            var ts = TimeSpan.FromMilliseconds(r.TimeTakenMs);
            dgv.Rows.Add(
                $"#{r.Position}",
                r.IsAiBot ? $"{r.Username} (AI)" : r.Username,
                $"{r.Wpm:F1}",
                $"{r.Accuracy:F1}%",
                r.BestStreak,
                $"{r.ConsistencyScore:F1}",
                $"{ts.Minutes:D2}:{ts.Seconds:D2}",
                r.IsDisqualified ? "Bị loại" : r.IsCompleted ? "Hoàn thành" : "Chưa xong",
                r.Achievements.Count > 0 ? string.Join(", ", r.Achievements) : "-"
            );
        }

        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.Cells["Name"].Value?.ToString() == AppState.Instance.CurrentUser?.Username)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 255);
                row.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            }
            else if (row.Cells["Name"].Value?.ToString()?.Contains("(AI)", StringComparison.OrdinalIgnoreCase) == true)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(248, 244, 255);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(73, 45, 112);
            }
        }

        boardLayout.Controls.Add(lblBoard, 0, 0);
        boardLayout.Controls.Add(dgv, 0, 1);
        boardCard.Controls.Add(boardLayout);

        var aiCard = ClientTheme.CreateCard(new Padding(16));
        aiCard.Margin = new Padding(0, 0, 0, 14);

        var aiLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Color.Transparent,
        };
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        aiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var aiHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        aiHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        aiHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblAiTitle = new Label
        {
            Text = "AI Coach",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _btnAiAnalyze = new Button
        {
            Text = "Phân tích AI",
            Size = new Size(132, 44),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(10, 0, 0, 0),
        };
        ClientTheme.StyleButton(_btnAiAnalyze, ThemeButtonVariant.Primary, compact: true);
        _btnAiAnalyze.Click += BtnAiAnalyze_Click;

        _lblAiStatus = new Label
        {
            Text = "Nhấn \"Phân tích AI\" để nhận gợi ý cá nhân hóa.",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        _txtAi = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
        ClientTheme.StyleRichTextBox(_txtAi);

        _aiSnapshot = new AiCoachSnapshotControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
        };

        _keyboardHeatmap = new KeyboardHeatmapControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
        };

        var lblAiPassages = new Label
        {
            Text = "Bài luyện AI tạo",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var lblAiMissions = new Label
        {
            Text = "AI practice missions",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lstAiMissions = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
        };
        ClientTheme.StyleListBox(_lstAiMissions);
        _lstAiMissions.SelectedIndexChanged += (_, _) =>
        {
            var index = _lstAiMissions.SelectedIndex;
            _selectedAiMission = index >= 0 && index < _aiMissions.Count ? _aiMissions[index] : null;
            _btnAiMissionPractice.Enabled = _selectedAiMission != null;
        };

        _lstAiPassages = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
        };
        ClientTheme.StyleListBox(_lstAiPassages);
        _lstAiPassages.SelectedIndexChanged += (_, _) =>
        {
            _selectedAiPassage = _lstAiPassages.SelectedItem?.ToString();
            _btnAiPractice.Enabled = !string.IsNullOrWhiteSpace(_selectedAiPassage);
        };

        aiHeader.Controls.Add(lblAiTitle, 0, 0);
        aiHeader.Controls.Add(_btnAiAnalyze, 1, 0);

        aiLayout.Controls.Add(aiHeader, 0, 0);
        aiLayout.Controls.Add(_lblAiStatus, 0, 1);
        aiLayout.Controls.Add(_aiSnapshot, 0, 2);
        aiLayout.Controls.Add(_keyboardHeatmap, 0, 3);
        aiLayout.Controls.Add(_txtAi, 0, 4);
        aiLayout.Controls.Add(lblAiMissions, 0, 5);
        aiLayout.Controls.Add(_lstAiMissions, 0, 6);
        aiLayout.Controls.Add(lblAiPassages, 0, 7);
        aiLayout.Controls.Add(_lstAiPassages, 0, 8);
        aiCard.Controls.Add(aiLayout);

        var actionBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
        };

        var actionLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
        };

        _btnPractice = new Button
        {
            Text = "Luyện lại bài vừa đua",
            Size = new Size(210, 44),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnPractice, ThemeButtonVariant.Success);
        _btnPractice.Click += BtnPractice_Click;

        _btnAiMissionPractice = new Button
        {
            Text = "Chơi mission AI",
            Size = new Size(174, 44),
            Anchor = AnchorStyles.Left,
            Enabled = false,
            Visible = false,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnAiMissionPractice, ThemeButtonVariant.Accent, compact: true);
        _btnAiMissionPractice.Click += BtnAiMissionPractice_Click;

        _btnAiPractice = new Button
        {
            Text = "Luyện bài AI đã chọn",
            Size = new Size(190, 44),
            Anchor = AnchorStyles.Left,
            Enabled = false,
            Visible = false,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnAiPractice, ThemeButtonVariant.Neutral, compact: true);
        _btnAiPractice.Click += BtnAiPractice_Click;

        _btnExportReport = new Button
        {
            Text = "Xuất report",
            Size = new Size(154, 44),
            Anchor = AnchorStyles.Left,
            Enabled = _myResult != null,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnExportReport, ThemeButtonVariant.Accent, compact: true);
        _btnExportReport.Click += BtnExportReport_Click;

        _btnExportData = new Button
        {
            Text = "Xuất data",
            Size = new Size(144, 44),
            Anchor = AnchorStyles.Left,
            Enabled = _myResult != null,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnExportData, ThemeButtonVariant.Neutral, compact: true);
        _btnExportData.Click += BtnExportData_Click;

        _btnCopyScore = new Button
        {
            Text = "Copy score",
            Size = new Size(158, 44),
            Anchor = AnchorStyles.Left,
            Enabled = _myResult != null,
            Margin = new Padding(0, 0, 10, 8),
        };
        ClientTheme.StyleButton(_btnCopyScore, ThemeButtonVariant.Neutral, compact: true);
        _btnCopyScore.Click += BtnCopyScore_Click;

        var btnClose = new Button
        {
            Text = "Quay lại phòng",
            Size = new Size(166, 44),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 8),
        };
        ClientTheme.StyleButton(btnClose, ThemeButtonVariant.Primary);
        btnClose.Click += (s, e) => Close();

        actionLayout.Controls.Add(_btnPractice);
        actionLayout.Controls.Add(_btnAiMissionPractice);
        actionLayout.Controls.Add(_btnAiPractice);
        actionLayout.Controls.Add(_btnExportReport);
        actionLayout.Controls.Add(_btnExportData);
        actionLayout.Controls.Add(_btnCopyScore);
        actionLayout.Controls.Add(btnClose);
        actionBar.Controls.Add(actionLayout);

        pageLayout.Controls.Add(headerCard, 0, 0);
        pageLayout.Controls.Add(summaryCard, 0, 1);
        pageLayout.Controls.Add(progressCards, 0, 2);
        pageLayout.Controls.Add(certificateShell, 0, 3);
        pageLayout.Controls.Add(performanceCard, 0, 4);
        pageLayout.Controls.Add(boardCard, 0, 5);
        pageLayout.Controls.Add(aiCard, 0, 6);
        pageLayout.Controls.Add(actionBar, 0, 7);

        page.Controls.Add(pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 2280));

        Load += ResultForm_Load;
        FormClosed += ResultForm_FormClosed;
    }

    private Control BuildProgressCards()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

        var dailyChallengeCard = BuildDailyChallengeCard();
        dailyChallengeCard.Margin = new Padding(0, 0, 0, 8);

        var personalBestCard = BuildPersonalBestCard();
        personalBestCard.Margin = new Padding(0, 0, 0, 8);

        var keyboardMasteryCard = BuildKeyboardMasteryCard();
        keyboardMasteryCard.Margin = new Padding(0);

        layout.Controls.Add(dailyChallengeCard, 0, 0);
        layout.Controls.Add(personalBestCard, 0, 1);
        layout.Controls.Add(keyboardMasteryCard, 0, 2);
        return layout;
    }

    private Control BuildDailyChallengeCard()
    {
        var card = ClientTheme.CreateCard(new Padding(16));
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));

        var titleBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 14, 0),
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Daily challenge",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var subtitle = new Label
        {
            Text = BuildDailyChallengeSubtitle(),
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.25f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };

        titleBlock.Controls.Add(title, 0, 0);
        titleBlock.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(titleBlock, 0, 0);

        layout.Controls.Add(CreateDailyChallengeCell(
            "Race hôm nay",
            _dailyChallengeProgress?.RacesToday ?? 0,
            _dailyChallengeProgress?.RaceTarget ?? 3), 1, 0);
        layout.Controls.Add(CreateDailyChallengeCell(
            "Accuracy >= 95%",
            _dailyChallengeProgress?.AccuracyRacesToday ?? 0,
            _dailyChallengeProgress?.AccuracyTarget ?? 2), 2, 0);
        layout.Controls.Add(CreateDailyChallengeCell(
            "Top 3 / win",
            _dailyChallengeProgress?.PodiumsToday ?? 0,
            _dailyChallengeProgress?.PodiumTarget ?? 1), 3, 0);

        card.Controls.Add(layout);
        return card;
    }

    private string BuildDailyChallengeSubtitle()
    {
        if (_dailyChallengeProgress == null)
            return "Không có dữ liệu người chơi để cập nhật thử thách hôm nay.";

        var counted = _dailyChallengeProgress.WasNewRace
            ? "đã cộng race này"
            : "race này đã được tính trước đó";
        return $"{_dailyChallengeProgress.Date:yyyy-MM-dd} | {counted} | {_dailyChallengeProgress.Badge} | {_dailyChallengeProgress.CompletedChallengeCount}/3 mục";
    }

    private static Control CreateDailyChallengeCell(string title, int current, int target)
    {
        var done = current >= target;
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = done ? Color.FromArgb(239, 253, 244) : ClientTheme.SurfaceSubtle,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(10, 6, 10, 6),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var progressLabel = new Label
        {
            Text = $"{Math.Min(current, target)}/{target}",
            Dock = DockStyle.Fill,
            ForeColor = done ? ClientTheme.Success : ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var statusLabel = new Label
        {
            Text = done ? "Xong" : $"Còn {Math.Max(0, target - current)}",
            Dock = DockStyle.Fill,
            ForeColor = done ? ClientTheme.Success : ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.TopLeft,
        };

        panel.Controls.Add(titleLabel, 0, 0);
        panel.Controls.Add(progressLabel, 0, 1);
        panel.Controls.Add(statusLabel, 0, 2);
        ClientTheme.ApplyRoundedCorners(panel, 8);
        return panel;
    }

    private Control BuildPersonalBestCard()
    {
        var card = ClientTheme.CreateCard(new Padding(16));
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));

        var titleBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 14, 0),
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Personal best",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var subtitle = new Label
        {
            Text = BuildPersonalBestSubtitle(),
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.25f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };

        titleBlock.Controls.Add(title, 0, 0);
        titleBlock.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(titleBlock, 0, 0);

        if (_personalBestProgress == null)
        {
            layout.Controls.Add(CreatePersonalBestCell("WPM", "-", false, "Chưa có dữ liệu"), 1, 0);
            layout.Controls.Add(CreatePersonalBestCell("Accuracy", "-", false, "Chưa có dữ liệu"), 2, 0);
            layout.Controls.Add(CreatePersonalBestCell("Ổn định", "-", false, "Chưa có dữ liệu"), 3, 0);
            layout.Controls.Add(CreatePersonalBestCell("Combo", "-", false, "Chưa có dữ liệu"), 4, 0);
        }
        else
        {
            layout.Controls.Add(CreatePersonalBestCell(
                "WPM",
                $"{_personalBestProgress.BestWpm:F1}",
                _personalBestProgress.IsNewBestWpm,
                FormatPbDelta(_personalBestProgress.BestWpm, _personalBestProgress.PreviousBestWpm, " WPM")), 1, 0);
            layout.Controls.Add(CreatePersonalBestCell(
                "Accuracy",
                $"{_personalBestProgress.BestAccuracy:F1}%",
                _personalBestProgress.IsNewBestAccuracy,
                FormatPbDelta(_personalBestProgress.BestAccuracy, _personalBestProgress.PreviousBestAccuracy, "%")), 2, 0);
            layout.Controls.Add(CreatePersonalBestCell(
                "Ổn định",
                $"{_personalBestProgress.BestConsistency:F1}",
                _personalBestProgress.IsNewBestConsistency,
                FormatPbDelta(_personalBestProgress.BestConsistency, _personalBestProgress.PreviousBestConsistency)), 3, 0);
            layout.Controls.Add(CreatePersonalBestCell(
                "Combo",
                _personalBestProgress.BestStreak.ToString(),
                _personalBestProgress.IsNewBestStreak,
                FormatPbDelta(_personalBestProgress.BestStreak, _personalBestProgress.PreviousBestStreak)), 4, 0);
        }

        card.Controls.Add(layout);
        return card;
    }

    private string BuildPersonalBestSubtitle()
    {
        if (_personalBestProgress == null)
            return "Không có dữ liệu người chơi để cập nhật personal best.";

        var counted = _personalBestProgress.WasNewRace
            ? "đã cộng race này"
            : "race này đã được tính trước đó";
        var modeLabel = ToGameModeLabel(_personalBestProgress.Mode, _aiPracticeDifficulty);
        return $"{modeLabel} | {counted} | tổng {_personalBestProgress.TotalRaces} race";
    }

    private static Control CreatePersonalBestCell(string title, string value, bool isNewBest, string note)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = isNewBest ? Color.FromArgb(239, 253, 244) : ClientTheme.SurfaceSubtle,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(10, 6, 10, 6),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = isNewBest ? ClientTheme.Success : ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var noteLabel = new Label
        {
            Text = isNewBest ? $"PB mới | {note}" : note,
            Dock = DockStyle.Fill,
            ForeColor = isNewBest ? ClientTheme.Success : ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };

        panel.Controls.Add(titleLabel, 0, 0);
        panel.Controls.Add(valueLabel, 0, 1);
        panel.Controls.Add(noteLabel, 0, 2);
        ClientTheme.ApplyRoundedCorners(panel, 8);
        return panel;
    }

    private Control BuildKeyboardMasteryCard()
    {
        var card = ClientTheme.CreateCard(new Padding(16));
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));

        var titleBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 14, 0),
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Keyboard mastery",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var subtitle = new Label
        {
            Text = BuildKeyboardMasterySubtitle(),
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.25f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };

        titleBlock.Controls.Add(title, 0, 0);
        titleBlock.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(titleBlock, 0, 0);

        if (_keyboardMasteryProgress == null)
        {
            layout.Controls.Add(CreateKeyboardMasteryCell("Mastered", "-", false, "Chưa có dữ liệu"), 1, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell("Coverage", "-", false, "Chưa có dữ liệu"), 2, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell("Accuracy", "-", false, "Chưa có dữ liệu"), 3, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell("Review", "-", false, "Chưa có dữ liệu"), 4, 0);
        }
        else
        {
            layout.Controls.Add(CreateKeyboardMasteryCell(
                "Mastered",
                $"{_keyboardMasteryProgress.MasteredKeyCount}/{_keyboardMasteryProgress.MasteryTargetKeyCount}",
                _keyboardMasteryProgress.MasteredKeyCount >= _keyboardMasteryProgress.MasteryTargetKeyCount,
                ">=96% acc, đủ mẫu"), 1, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell(
                "Coverage",
                $"{_keyboardMasteryProgress.CoveragePercent:F0}%",
                _keyboardMasteryProgress.CoveragePercent >= 80m,
                $"{_keyboardMasteryProgress.TrackedKeyCount} key tracked"), 2, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell(
                "Accuracy",
                $"{_keyboardMasteryProgress.AverageAccuracy:F1}%",
                _keyboardMasteryProgress.AverageAccuracy >= 95m,
                "raw key accuracy"), 3, 0);
            layout.Controls.Add(CreateKeyboardMasteryCell(
                "Review",
                _keyboardMasteryProgress.NeedsReviewCount == 0
                    ? "Clear"
                    : string.Join(", ", _keyboardMasteryProgress.NeedsReviewKeys.Take(4)),
                _keyboardMasteryProgress.NeedsReviewCount == 0,
                _keyboardMasteryProgress.WeakestKeys.Count > 0
                    ? $"Weak: {string.Join(", ", _keyboardMasteryProgress.WeakestKeys.Take(3))}"
                    : "Không có key yếu"), 4, 0);
        }

        card.Controls.Add(layout);
        return card;
    }

    private string BuildKeyboardMasterySubtitle()
    {
        if (_keyboardMasteryProgress == null)
            return "Không có dữ liệu người chơi để cập nhật keyboard mastery.";

        var counted = _keyboardMasteryProgress.WasNewRace
            ? "đã cộng race này"
            : "race này đã được tính trước đó";
        var strongest = _keyboardMasteryProgress.StrongestKeys.Count > 0
            ? $"strong: {string.Join(", ", _keyboardMasteryProgress.StrongestKeys.Take(3))}"
            : "đang gom mẫu";
        return $"TypingClub/keybr style | {counted} | tổng {_keyboardMasteryProgress.TotalRaces} race | {strongest}";
    }

    private static Control CreateKeyboardMasteryCell(string title, string value, bool isStrong, string note)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = isStrong ? Color.FromArgb(239, 253, 244) : ClientTheme.SurfaceSubtle,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(10, 6, 10, 6),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = isStrong ? ClientTheme.Success : ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", value.Length > 14 ? 11.5f : 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var noteLabel = new Label
        {
            Text = note,
            Dock = DockStyle.Fill,
            ForeColor = isStrong ? ClientTheme.Success : ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };

        panel.Controls.Add(titleLabel, 0, 0);
        panel.Controls.Add(valueLabel, 0, 1);
        panel.Controls.Add(noteLabel, 0, 2);
        ClientTheme.ApplyRoundedCorners(panel, 8);
        return panel;
    }

    private static string FormatPbDelta(decimal current, decimal previous, string suffix = "")
    {
        if (previous <= 0m && current > 0m)
            return "PB đầu tiên";

        var delta = current - previous;
        if (delta > 0m)
            return $"+{delta:F1}{suffix}";
        if (delta < 0m)
            return $"{delta:F1}{suffix}";
        return "Giữ PB";
    }

    private static string FormatPbDelta(int current, int previous)
    {
        if (previous <= 0 && current > 0)
            return "PB đầu tiên";

        var delta = current - previous;
        if (delta > 0)
            return $"+{delta}";
        if (delta < 0)
            return delta.ToString();
        return "Giữ PB";
    }

    private void ResultForm_Load(object? sender, EventArgs e)
    {
        var dispatcher = AppState.Instance.Dispatcher;
        dispatcher.OnAiCoachResponse += OnAiCoachResponse;
        dispatcher.OnError += OnError;
    }

    private void ResultForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        var dispatcher = AppState.Instance.Dispatcher;
        dispatcher.OnAiCoachResponse -= OnAiCoachResponse;
        dispatcher.OnError -= OnError;
    }

    private async void BtnAiAnalyze_Click(object? sender, EventArgs e)
    {
        if (_awaitingAi)
            return;

        if (_myResult == null)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = "Không tìm thấy dữ liệu người chơi hiện tại.";
            return;
        }

        _awaitingAi = true;
        _btnAiAnalyze.Enabled = false;
        _selectedAiMission = null;
        _selectedAiPassage = null;
        _aiMissions.Clear();
        _aiPassages.Clear();
        _aiSnapshot.SetLoading();
        _keyboardHeatmap.SetWeakKeys(Array.Empty<string>(), Array.Empty<string>());
        _lstAiMissions.Items.Clear();
        _lstAiPassages.Items.Clear();
        _btnAiMissionPractice.Visible = false;
        _btnAiMissionPractice.Enabled = false;
        _btnAiPractice.Visible = false;
        _btnAiPractice.Enabled = false;
        _lblAiStatus.ForeColor = ClientTheme.TextMuted;
        _lblAiStatus.Text = "AI đang phân tích trận đấu...";
        _txtAi.Text = string.Empty;

        try
        {
            var request = new GetAiCoachRequest
            {
                RoomCode = _result.RoomCode,
                RaceId = _result.RaceId,
                UserId = _myResult.UserId,
                Username = _myResult.Username,
                Position = _myResult.Position,
                TotalPlayers = Math.Max(1, _result.Results.Count),
                Wpm = _myResult.Wpm,
                Accuracy = _myResult.Accuracy,
                CharsCorrect = _myResult.CharsCorrect,
                CharsWrong = _myResult.CharsWrong,
                TimeTakenMs = _myResult.TimeTakenMs,
                IsCompleted = _myResult.IsCompleted,
                Language = _passageLanguage,
                TypedText = Truncate(_myTypedText, 1200),
                PassageText = _passageText.Length <= 420
                    ? _passageText
                    : _passageText[..420],
            };

            await AppState.Instance.Client.SendAsync(MessageType.GET_AI_COACH, request);
        }
        catch (Exception ex)
        {
            _awaitingAi = false;
            _btnAiAnalyze.Enabled = true;
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = $"Không gửi được yêu cầu AI: {ex.Message}";
        }
    }

    private void OnAiCoachResponse(NetworkMessage message)
    {
        if (!_awaitingAi)
            return;

        _awaitingAi = false;
        _btnAiAnalyze.Enabled = true;

        var payload = message.GetPayload<AiCoachResponse>();
        if (payload == null)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = "AI trả về dữ liệu không hợp lệ.";
            _aiSnapshot.SetError(_lblAiStatus.Text);
            return;
        }

        if (!payload.Success)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = string.IsNullOrWhiteSpace(payload.ErrorMessage)
                ? "AI không thể phân tích lúc này."
                : payload.ErrorMessage;
            _aiSnapshot.SetError(_lblAiStatus.Text);
            return;
        }

        if (_myResult != null && payload.UserId != 0 && payload.UserId != _myResult.UserId)
            return;

        _aiSnapshot.SetData(payload, _myResult);
        _keyboardHeatmap.SetWeakKeys(payload.TopMistypedCharacters, payload.TopMistypedNgrams);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(payload.CoachText))
        {
            sb.AppendLine(payload.CoachText.Trim());
            sb.AppendLine();
        }

        if (payload.Tips.Count > 0)
        {
            sb.AppendLine("Gợi ý chính:");
            for (var i = 0; i < payload.Tips.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.Tips[i]}");
            }
            sb.AppendLine();
        }

        if (payload.ActionPlan.Count > 0)
        {
            sb.AppendLine("Kế hoạch luyện tập trận tới:");
            for (var i = 0; i < payload.ActionPlan.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.ActionPlan[i]}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(payload.TrainingTitle) ||
            !string.IsNullOrWhiteSpace(payload.RecommendedGameMode) ||
            !string.IsNullOrWhiteSpace(payload.RecommendedDifficulty) ||
            payload.RecommendedTargetRpm > 0)
        {
            sb.AppendLine("Gói luyện AI đề xuất:");
            if (!string.IsNullOrWhiteSpace(payload.TrainingTitle))
                sb.AppendLine($"- Giáo án: {payload.TrainingTitle}");
            if (!string.IsNullOrWhiteSpace(payload.RecommendedGameMode))
                sb.AppendLine($"- Chế độ: {ToGameModeLabel(payload.RecommendedGameMode, payload.RecommendedDifficulty)}");
            if (!string.IsNullOrWhiteSpace(payload.RecommendedDifficulty))
                sb.AppendLine($"- Cấp AI: {ToAiPracticeDifficultyLabel(payload.RecommendedDifficulty)}");
            if (payload.RecommendedTargetRpm > 0)
                sb.AppendLine($"- RPM mục tiêu: {payload.RecommendedTargetRpm}");
            sb.AppendLine();
        }

        if (payload.MistakeFingerprint.Count > 0 ||
            payload.AdaptiveRaceStrategy.Count > 0 ||
            payload.PersonalizationScore > 0 ||
            !string.IsNullOrWhiteSpace(payload.TrainingPackSignature))
        {
            sb.AppendLine("AI typing fingerprint:");
            if (payload.PersonalizationScore > 0)
                sb.AppendLine($"- Personalization score: {payload.PersonalizationScore:F1}/100");
            if (!string.IsNullOrWhiteSpace(payload.TrainingPackSignature))
                sb.AppendLine($"- Training pack signature: {payload.TrainingPackSignature}");
            for (var i = 0; i < payload.MistakeFingerprint.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.MistakeFingerprint[i]}");
            }

            if (payload.AdaptiveRaceStrategy.Count > 0)
            {
                sb.AppendLine("Adaptive race strategy:");
                for (var i = 0; i < payload.AdaptiveRaceStrategy.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {payload.AdaptiveRaceStrategy[i]}");
                }
            }
            sb.AppendLine();
        }

        if (payload.AiEvidenceTrail.Count > 0 ||
            payload.GeneratedPassageAudit.Count > 0 ||
            payload.AiConfidenceScore > 0 ||
            payload.PassageNoveltyScore > 0 ||
            payload.WeakspotCoverageScore > 0)
        {
            sb.AppendLine("AI evidence/originality audit:");
            if (payload.AiConfidenceScore > 0)
                sb.AppendLine($"- Confidence: {payload.AiConfidenceScore:F1}/100");
            if (payload.PassageNoveltyScore > 0)
                sb.AppendLine($"- Passage novelty: {payload.PassageNoveltyScore:F1}/100");
            if (payload.WeakspotCoverageScore > 0)
                sb.AppendLine($"- Weakspot coverage: {payload.WeakspotCoverageScore:F1}/100");
            for (var i = 0; i < payload.AiEvidenceTrail.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.AiEvidenceTrail[i]}");
            }

            if (payload.GeneratedPassageAudit.Count > 0)
            {
                sb.AppendLine("Generated passage audit:");
                for (var i = 0; i < payload.GeneratedPassageAudit.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {payload.GeneratedPassageAudit[i]}");
                }
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(payload.DailyChallengeTitle) ||
            !string.IsNullOrWhiteSpace(payload.DailyChallengeGoal) ||
            !string.IsNullOrWhiteSpace(payload.DailyChallengeReward) ||
            payload.PracticeWords.Count > 0)
        {
            sb.AppendLine("Daily challenge cá nhân:");
            if (!string.IsNullOrWhiteSpace(payload.DailyChallengeTitle))
                sb.AppendLine($"- Tên thử thách: {payload.DailyChallengeTitle}");
            if (!string.IsNullOrWhiteSpace(payload.DailyChallengeGoal))
                sb.AppendLine($"- Mục tiêu: {payload.DailyChallengeGoal}");
            if (!string.IsNullOrWhiteSpace(payload.DailyChallengeReward))
                sb.AppendLine($"- Reward: {payload.DailyChallengeReward}");
            if (payload.PracticeWords.Count > 0)
                sb.AppendLine($"- Practice words: {string.Join(", ", payload.PracticeWords.Take(10))}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(payload.ProblemKeyStoryPassage) ||
            payload.ProblemKeyStoryKeys.Count > 0 ||
            !string.IsNullOrWhiteSpace(payload.ProblemKeyStoryTitle))
        {
            sb.AppendLine("TypeAI problem-key story:");
            if (!string.IsNullOrWhiteSpace(payload.ProblemKeyStoryTitle))
                sb.AppendLine($"- Title: {payload.ProblemKeyStoryTitle}");
            if (!string.IsNullOrWhiteSpace(payload.ProblemKeyStoryTopic))
                sb.AppendLine($"- Topic: {payload.ProblemKeyStoryTopic}");
            if (payload.ProblemKeyStoryKeys.Count > 0)
                sb.AppendLine($"- Problem keys: {string.Join(", ", payload.ProblemKeyStoryKeys.Take(6))}");
            if (!string.IsNullOrWhiteSpace(payload.ProblemKeyStoryPassage))
                sb.AppendLine($"- Story passage: {payload.ProblemKeyStoryPassage}");
            sb.AppendLine();
        }

        if (payload.MistakeHeatmap.Count > 0)
        {
            sb.AppendLine("AI weakspot heatmap:");
            for (var i = 0; i < payload.MistakeHeatmap.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.MistakeHeatmap[i]}");
            }
            sb.AppendLine();
        }

        if (payload.AdaptiveMicroLessons.Count > 0)
        {
            sb.AppendLine("Micro-lesson thích nghi:");
            for (var i = 0; i < payload.AdaptiveMicroLessons.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.AdaptiveMicroLessons[i]}");
            }
            sb.AppendLine();
        }

        if (payload.NextSessionChecklist.Count > 0)
        {
            sb.AppendLine("Checklist buổi kế tiếp:");
            for (var i = 0; i < payload.NextSessionChecklist.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.NextSessionChecklist[i]}");
            }
            sb.AppendLine();
        }

        if (payload.GhostRacePlan.Count > 0 ||
            payload.GhostTargetWpm > 0 ||
            payload.GhostTargetAccuracy > 0 ||
            !string.IsNullOrWhiteSpace(payload.GhostRewardBadge))
        {
            sb.AppendLine("AI ghost rival:");
            if (payload.GhostTargetWpm > 0 || payload.GhostTargetAccuracy > 0)
                sb.AppendLine($"- Ghost target: {payload.GhostTargetWpm:F1} WPM | {payload.GhostTargetAccuracy:F1}% accuracy");
            if (!string.IsNullOrWhiteSpace(payload.GhostRewardBadge))
                sb.AppendLine($"- Badge nếu thắng ghost: {payload.GhostRewardBadge}");
            for (var i = 0; i < payload.GhostRacePlan.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.GhostRacePlan[i]}");
            }
            sb.AppendLine();
        }

        if (payload.FingerDiagnostics.Count > 0)
        {
            sb.AppendLine("AI finger diagnostics:");
            for (var i = 0; i < payload.FingerDiagnostics.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.FingerDiagnostics[i]}");
            }
            sb.AppendLine();
        }

        if (payload.ProgressPrediction.Count > 0)
        {
            sb.AppendLine("Progress prediction:");
            for (var i = 0; i < payload.ProgressPrediction.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.ProgressPrediction[i]}");
            }
            sb.AppendLine();
        }

        if (payload.LessonLadder.Count > 0)
        {
            sb.AppendLine("Lesson ladder:");
            for (var i = 0; i < payload.LessonLadder.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.LessonLadder[i]}");
            }
            sb.AppendLine();
        }

        if (payload.AttemptReplayCues.Count > 0)
        {
            sb.AppendLine("Attempt replay cues:");
            for (var i = 0; i < payload.AttemptReplayCues.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.AttemptReplayCues[i]}");
            }
            sb.AppendLine();
        }

        if (payload.WeakKeyDrills.Count > 0)
        {
            sb.AppendLine("Weak-key drill deck:");
            for (var i = 0; i < payload.WeakKeyDrills.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.WeakKeyDrills[i]}");
            }
            sb.AppendLine();
        }

        if (payload.NgramDrills.Count > 0)
        {
            sb.AppendLine("Adaptive n-gram drills:");
            for (var i = 0; i < payload.NgramDrills.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.NgramDrills[i]}");
            }
            sb.AppendLine();
        }

        if (payload.SpacedRepetitionPlan.Count > 0)
        {
            sb.AppendLine("Spaced repetition plan:");
            for (var i = 0; i < payload.SpacedRepetitionPlan.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.SpacedRepetitionPlan[i]}");
            }
            sb.AppendLine();
        }

        if (payload.MasteryCheckpoints.Count > 0)
        {
            sb.AppendLine("Mastery checkpoints:");
            for (var i = 0; i < payload.MasteryCheckpoints.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.MasteryCheckpoints[i]}");
            }
            sb.AppendLine();
        }

        if (payload.PracticeMissions.Count > 0)
        {
            var missions = payload.PracticeMissions
                .Where(IsUsableMission)
                .Take(6)
                .ToList();

            if (missions.Count > 0)
            {
                sb.AppendLine("AI practice missions có thể chơi ngay:");
                for (var i = 0; i < missions.Count; i++)
                {
                    var mission = missions[i];
                    sb.AppendLine($"{i + 1}. {mission.Title} | {FormatMissionClock(mission.DurationSeconds)} | {mission.TargetWpm:F1} WPM | {mission.TargetAccuracy:F1}%");
                    if (!string.IsNullOrWhiteSpace(mission.Objective))
                        sb.AppendLine($"   - {mission.Objective}");
                }
                sb.AppendLine();

                _aiMissions.Clear();
                _aiMissions.AddRange(missions);
                _lstAiMissions.BeginUpdate();
                _lstAiMissions.Items.Clear();
                foreach (var mission in missions)
                    _lstAiMissions.Items.Add(FormatMissionListItem(mission));
                _lstAiMissions.EndUpdate();
                _lstAiMissions.SelectedIndex = 0;
                _selectedAiMission = missions[0];
                _btnAiMissionPractice.Visible = true;
                _btnAiMissionPractice.Enabled = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.FocusArea) ||
            !string.IsNullOrWhiteSpace(payload.SkillTier) ||
            payload.EstimatedNextWpm > 0)
        {
            sb.AppendLine("Phân tích nhanh:");
            if (!string.IsNullOrWhiteSpace(payload.FocusArea))
                sb.AppendLine($"- Trọng tâm: {payload.FocusArea}");
            if (!string.IsNullOrWhiteSpace(payload.SkillTier))
                sb.AppendLine($"- Mức hiện tại: {payload.SkillTier}");
            if (payload.EstimatedNextWpm > 0)
                sb.AppendLine($"- WPM mục tiêu gần: ~{payload.EstimatedNextWpm:F1}");
            sb.AppendLine();
        }

        if (payload.MistakeDensity > 0m ||
            payload.TopMistypedCharacters.Count > 0 ||
            payload.TopMistypedWords.Count > 0 ||
            payload.TopMistypedNgrams.Count > 0)
        {
            sb.AppendLine("Phân tích lỗi chi tiết:");
            if (payload.MistakeDensity > 0m)
                sb.AppendLine($"- Mật độ lỗi: {payload.MistakeDensity * 100m:F1}%");
            if (payload.TopMistypedCharacters.Count > 0)
                sb.AppendLine($"- Ký tự sai nhiều nhất: {string.Join(", ", payload.TopMistypedCharacters)}");
            if (payload.TopMistypedWords.Count > 0)
                sb.AppendLine($"- Từ sai nhiều nhất: {string.Join(", ", payload.TopMistypedWords)}");
            if (payload.TopMistypedNgrams.Count > 0)
                sb.AppendLine($"- N-gram sai nhiều nhất: {string.Join(", ", payload.TopMistypedNgrams)}");
            sb.AppendLine();
        }

        if (payload.RecentRaceCount > 0)
        {
            sb.AppendLine("Xu hướng gần đây:");
            sb.AppendLine($"- Số race tham chiếu: {payload.RecentRaceCount} (hoàn thành: {payload.RecentCompletedCount})");
            sb.AppendLine($"- Delta WPM: {payload.RecentWpmTrend:+0.0;-0.0;0}");
            sb.AppendLine($"- Delta accuracy: {payload.RecentAccuracyTrend:+0.0;-0.0;0}%");
            sb.AppendLine();
        }

        if (payload.PersonalizedDrills.Count > 0)
        {
            sb.AppendLine("Drill cá nhân hóa:");
            for (var i = 0; i < payload.PersonalizedDrills.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {payload.PersonalizedDrills[i]}");
            }
            sb.AppendLine();
        }

        if (payload.SuggestedPassages.Count > 0)
        {
            var passages = payload.SuggestedPassages
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Concat(new[] { payload.ProblemKeyStoryPassage.Trim() })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            sb.AppendLine("Đoạn văn AI gợi ý để luyện:");
            for (var i = 0; i < passages.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {passages[i]}");
            }

            if (passages.Count > 0)
            {
                _aiPassages.Clear();
                _aiPassages.AddRange(passages);
                _lstAiPassages.BeginUpdate();
                _lstAiPassages.Items.Clear();
                foreach (var passage in passages)
                    _lstAiPassages.Items.Add(passage);
                _lstAiPassages.EndUpdate();
                _lstAiPassages.SelectedIndex = 0;
                _selectedAiPassage = passages[0];
                _btnAiPractice.Visible = true;
                _btnAiPractice.Enabled = true;
            }
        }

        if (_selectedAiPassage == null)
        {
            _btnAiPractice.Visible = false;
            _btnAiPractice.Enabled = false;
        }

        if (_selectedAiMission == null)
        {
            _btnAiMissionPractice.Visible = false;
            _btnAiMissionPractice.Enabled = false;
        }

        if (sb.Length == 0)
            sb.Append("AI đã phản hồi nhưng chưa có gợi ý cụ thể.");

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"Nguồn phân tích: {payload.Provider} | {payload.Model}" +
                      (payload.IsFallback ? " | fallback" : " | AI thật") +
                      $" | lỗi tạm: {payload.MistakeSampleCount} | bài luyện: {payload.GeneratedPassageCount}" +
                      $" | DNA: {payload.PersonalizationScore:F1}/100 | confidence: {payload.AiConfidenceScore:F1}/100" +
                      $" | pack: {payload.TrainingPackSignature}");

        _txtAi.Text = sb.ToString();
        _lblAiStatus.ForeColor = ClientTheme.Success;
        _lblAiStatus.Text = payload.IsFallback
            ? "AI fail sau retry, đã dùng bank bài luyện dự phòng."
            : $"Đã nhận gói luyện AI thật từ {payload.Provider}/{payload.Model}.";
    }

    private void OnError(NetworkMessage message)
    {
        if (!_awaitingAi)
            return;

        var payload = message.GetPayload<ErrorPayload>();
        _awaitingAi = false;
        _btnAiAnalyze.Enabled = true;
        _lblAiStatus.ForeColor = ClientTheme.Danger;
        _lblAiStatus.Text = payload?.Message ?? "Yêu cầu AI thất bại.";
    }

    private void BtnPractice_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_passageText))
        {
            MessageBox.Show("Không có dữ liệu đoạn văn để luyện tập.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var practiceForm = new PracticeForm(_passageText, _passageLanguage);
        practiceForm.ShowDialog(this);
    }

    private void BtnAiMissionPractice_Click(object? sender, EventArgs e)
    {
        if (_selectedAiMission == null || string.IsNullOrWhiteSpace(_selectedAiMission.Passage))
        {
            MessageBox.Show("Chưa có mission AI hợp lệ.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var practiceForm = new PracticeForm(_selectedAiMission.Passage, _passageLanguage, _selectedAiMission);
        practiceForm.ShowDialog(this);
    }

    private void BtnAiPractice_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedAiPassage))
        {
            MessageBox.Show("Chưa có gợi ý luyện tập từ AI.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var practiceForm = new PracticeForm(_selectedAiPassage, _passageLanguage);
        practiceForm.ShowDialog(this);
    }

    private void BtnExportReport_Click(object? sender, EventArgs e)
    {
        if (_myResult == null)
        {
            MessageBox.Show("Không có dữ liệu kết quả để xuất report.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var report = BuildExportReport();
            var defaultName = BuildReportFileName();
            using var dialog = new SaveFileDialog
            {
                Title = "Xuất TypeRacer report",
                Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AddExtension = true,
                DefaultExt = "txt",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllText(dialog.FileName, report, Encoding.UTF8);
            _lblAiStatus.ForeColor = ClientTheme.Success;
            _lblAiStatus.Text = $"Đã xuất report: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = $"Không xuất được report: {ex.Message}";
        }
    }

    private void BtnExportData_Click(object? sender, EventArgs e)
    {
        if (_myResult == null)
        {
            MessageBox.Show("Không có dữ liệu kết quả để xuất analytics.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var defaultName = BuildAnalyticsFileName("json");
            using var dialog = new SaveFileDialog
            {
                Title = "Xuất TypeRacer analytics",
                Filter = "JSON analytics (*.json)|*.json|CSV analytics (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AddExtension = true,
                DefaultExt = "json",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var useCsv = dialog.FilterIndex == 2 ||
                         Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
            var content = useCsv ? BuildAnalyticsCsv() : BuildAnalyticsJson();
            File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
            _lblAiStatus.ForeColor = ClientTheme.Success;
            _lblAiStatus.Text = $"Đã xuất analytics: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = $"Không xuất được analytics: {ex.Message}";
        }
    }

    private void BtnCopyScore_Click(object? sender, EventArgs e)
    {
        if (_myResult == null)
        {
            MessageBox.Show("Không có dữ liệu kết quả để copy score.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Clipboard.SetText(BuildShareScoreCard());
            _lblAiStatus.ForeColor = ClientTheme.Success;
            _lblAiStatus.Text = "Đã copy share score vào clipboard.";
        }
        catch (Exception ex)
        {
            _lblAiStatus.ForeColor = ClientTheme.Danger;
            _lblAiStatus.Text = $"Không copy được score: {ex.Message}";
        }
    }

    private string BuildExportReport()
    {
        var reportBody = BuildExportReportBody();
        var signature = ComputeReportSignature(reportBody);
        return reportBody +
               Environment.NewLine +
               $"Verification SHA-256: {signature}" + Environment.NewLine;
    }

    private string BuildShareScoreCard()
    {
        var body = BuildShareScoreBody();
        var signature = ComputeReportSignature(body)[..16];
        return $"{body}{Environment.NewLine}Verify: {signature}";
    }

    private string BuildShareScoreBody()
    {
        if (_myResult == null)
            return "TypeRacer score unavailable";

        var mode = ToGameModeLabel(_gameMode, _aiPracticeDifficulty);
        var status = _myResult.IsDisqualified
            ? "Disqualified"
            : _myResult.IsCompleted ? "Finished" : "Timed out";
        var badges = _myResult.Achievements.Count > 0
            ? string.Join(", ", _myResult.Achievements.Take(3))
            : "No badge";

        return "TypeRacer Score Card" + Environment.NewLine +
               $"{_myResult.Username} | {_myResult.Wpm:F1} WPM | {_myResult.Accuracy:F1}% accuracy" + Environment.NewLine +
               $"Rank #{_myResult.Position}/{Math.Max(1, _result.Results.Count)} | {mode} | {status}" + Environment.NewLine +
               $"Best streak {_myResult.BestStreak} | Consistency {_myResult.ConsistencyScore:F1} | {badges}";
    }

    private string BuildExportReportBody()
    {
        var sb = new StringBuilder();
        var user = _myResult?.Username ?? "unknown";
        var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var mode = ToGameModeLabel(_gameMode, _aiPracticeDifficulty);

        sb.AppendLine("TypeRacer Race Report");
        sb.AppendLine($"Generated at: {generatedAt}");
        sb.AppendLine($"Room: {_result.RoomCode}");
        sb.AppendLine($"RaceId: {_result.RaceId}");
        sb.AppendLine($"Player: {user}");
        sb.AppendLine($"Mode: {mode}");

        if (_myResult != null)
        {
            sb.AppendLine($"Rank: {_myResult.Position}/{Math.Max(1, _result.Results.Count)}");
            sb.AppendLine($"WPM: {_myResult.Wpm:F1}");
            sb.AppendLine($"Accuracy: {_myResult.Accuracy:F1}%");
            sb.AppendLine($"Correct/Wrong: {_myResult.CharsCorrect}/{_myResult.CharsWrong}");
            sb.AppendLine($"Best streak: {_myResult.BestStreak}");
            sb.AppendLine($"Consistency: {_myResult.ConsistencyScore:F1}");
            sb.AppendLine($"Status: {(_myResult.IsDisqualified ? "Bị loại" : _myResult.IsCompleted ? "Hoàn thành" : "Chưa xong")}");
            sb.AppendLine($"Badges: {(_myResult.Achievements.Count > 0 ? string.Join(", ", _myResult.Achievements) : "-")}");
        }

        if (_dailyChallengeProgress != null)
        {
            sb.AppendLine();
            sb.AppendLine("Daily Challenge Progress:");
            sb.AppendLine($"Date: {_dailyChallengeProgress.Date:yyyy-MM-dd}");
            sb.AppendLine($"Badge: {_dailyChallengeProgress.Badge}");
            sb.AppendLine($"Race counted now: {(_dailyChallengeProgress.WasNewRace ? "yes" : "already counted")}");
            sb.AppendLine($"Races: {_dailyChallengeProgress.RacesToday}/{_dailyChallengeProgress.RaceTarget}");
            sb.AppendLine($"Accuracy >= 95%: {_dailyChallengeProgress.AccuracyRacesToday}/{_dailyChallengeProgress.AccuracyTarget}");
            sb.AppendLine($"Top 3 / win: {_dailyChallengeProgress.PodiumsToday}/{_dailyChallengeProgress.PodiumTarget}");
        }

        if (_personalBestProgress != null)
        {
            sb.AppendLine();
            sb.AppendLine("Personal Best Progress:");
            sb.AppendLine($"Mode: {ToGameModeLabel(_personalBestProgress.Mode, _aiPracticeDifficulty)}");
            sb.AppendLine($"Race counted now: {(_personalBestProgress.WasNewRace ? "yes" : "already counted")}");
            sb.AppendLine($"Total races in mode: {_personalBestProgress.TotalRaces}");
            sb.AppendLine($"Best WPM: {_personalBestProgress.BestWpm:F1} ({FormatPbDelta(_personalBestProgress.BestWpm, _personalBestProgress.PreviousBestWpm, " WPM")})");
            sb.AppendLine($"Best accuracy: {_personalBestProgress.BestAccuracy:F1}% ({FormatPbDelta(_personalBestProgress.BestAccuracy, _personalBestProgress.PreviousBestAccuracy, "%")})");
            sb.AppendLine($"Best consistency: {_personalBestProgress.BestConsistency:F1} ({FormatPbDelta(_personalBestProgress.BestConsistency, _personalBestProgress.PreviousBestConsistency)})");
            sb.AppendLine($"Best streak: {_personalBestProgress.BestStreak} ({FormatPbDelta(_personalBestProgress.BestStreak, _personalBestProgress.PreviousBestStreak)})");
        }

        if (_keyboardMasteryProgress != null)
        {
            sb.AppendLine();
            sb.AppendLine("Keyboard Mastery Progress:");
            sb.AppendLine($"Race counted now: {(_keyboardMasteryProgress.WasNewRace ? "yes" : "already counted")}");
            sb.AppendLine($"Total races: {_keyboardMasteryProgress.TotalRaces}");
            sb.AppendLine($"Tracked keys: {_keyboardMasteryProgress.TrackedKeyCount}");
            sb.AppendLine($"Mastered keys: {_keyboardMasteryProgress.MasteredKeyCount}/{_keyboardMasteryProgress.MasteryTargetKeyCount}");
            sb.AppendLine($"Coverage: {_keyboardMasteryProgress.CoveragePercent:F1}%");
            sb.AppendLine($"Average key accuracy: {_keyboardMasteryProgress.AverageAccuracy:F1}%");
            sb.AppendLine($"Needs review: {(_keyboardMasteryProgress.NeedsReviewKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.NeedsReviewKeys) : "none")}");
            sb.AppendLine($"Strongest keys: {(_keyboardMasteryProgress.StrongestKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.StrongestKeys) : "-")}");
            sb.AppendLine($"Weakest keys: {(_keyboardMasteryProgress.WeakestKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.WeakestKeys) : "-")}");
        }

        sb.AppendLine();
        sb.AppendLine("Leaderboard:");
        foreach (var result in _result.Results.OrderBy(x => x.Position))
        {
            sb.AppendLine($"#{result.Position} {result.Username} | {result.Wpm:F1} WPM | {result.Accuracy:F1}% | {result.TimeTakenMs}ms");
        }

        sb.AppendLine();
        sb.AppendLine("Passage:");
        sb.AppendLine(_passageText);

        if (!string.IsNullOrWhiteSpace(_txtAi.Text))
        {
            sb.AppendLine();
            sb.AppendLine("AI Coach Report:");
            sb.AppendLine(_txtAi.Text.Trim());
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("AI Coach Report: chưa chạy phân tích AI trong màn này.");
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildAnalyticsJson()
    {
        var payload = BuildAnalyticsPayload();
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        var export = new
        {
            Payload = payload,
            VerificationSha256 = ComputeReportSignature(payloadJson),
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });
    }

    private string BuildAnalyticsCsv()
    {
        var sb = new StringBuilder();
        var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var mode = ToGameModeLabel(_gameMode, _aiPracticeDifficulty);

        sb.AppendLine("section,key,value");
        AppendCsv(sb, "meta", "generated_at", generatedAt);
        AppendCsv(sb, "meta", "room_code", _result.RoomCode);
        AppendCsv(sb, "meta", "race_id", _result.RaceId.ToString());
        AppendCsv(sb, "meta", "mode", mode);
        if (_myResult != null)
        {
            AppendCsv(sb, "player", "username", _myResult.Username);
            AppendCsv(sb, "player", "rank", $"{_myResult.Position}/{Math.Max(1, _result.Results.Count)}");
            AppendCsv(sb, "player", "wpm", $"{_myResult.Wpm:F1}");
            AppendCsv(sb, "player", "accuracy", $"{_myResult.Accuracy:F1}");
            AppendCsv(sb, "player", "best_streak", _myResult.BestStreak.ToString());
            AppendCsv(sb, "player", "consistency", $"{_myResult.ConsistencyScore:F1}");
        }

        if (_dailyChallengeProgress != null)
        {
            AppendCsv(sb, "daily_challenge", "date", _dailyChallengeProgress.Date.ToString("yyyy-MM-dd"));
            AppendCsv(sb, "daily_challenge", "badge", _dailyChallengeProgress.Badge);
            AppendCsv(sb, "daily_challenge", "race_counted_now", _dailyChallengeProgress.WasNewRace ? "yes" : "already_counted");
            AppendCsv(sb, "daily_challenge", "races", $"{_dailyChallengeProgress.RacesToday}/{_dailyChallengeProgress.RaceTarget}");
            AppendCsv(sb, "daily_challenge", "accuracy_95", $"{_dailyChallengeProgress.AccuracyRacesToday}/{_dailyChallengeProgress.AccuracyTarget}");
            AppendCsv(sb, "daily_challenge", "top3", $"{_dailyChallengeProgress.PodiumsToday}/{_dailyChallengeProgress.PodiumTarget}");
        }

        if (_personalBestProgress != null)
        {
            AppendCsv(sb, "personal_best", "mode", ToGameModeLabel(_personalBestProgress.Mode, _aiPracticeDifficulty));
            AppendCsv(sb, "personal_best", "race_counted_now", _personalBestProgress.WasNewRace ? "yes" : "already_counted");
            AppendCsv(sb, "personal_best", "total_races", _personalBestProgress.TotalRaces.ToString());
            AppendCsv(sb, "personal_best", "best_wpm", $"{_personalBestProgress.BestWpm:F1}");
            AppendCsv(sb, "personal_best", "best_wpm_delta", FormatPbDelta(_personalBestProgress.BestWpm, _personalBestProgress.PreviousBestWpm, " WPM"));
            AppendCsv(sb, "personal_best", "best_accuracy", $"{_personalBestProgress.BestAccuracy:F1}%");
            AppendCsv(sb, "personal_best", "best_accuracy_delta", FormatPbDelta(_personalBestProgress.BestAccuracy, _personalBestProgress.PreviousBestAccuracy, "%"));
            AppendCsv(sb, "personal_best", "best_consistency", $"{_personalBestProgress.BestConsistency:F1}");
            AppendCsv(sb, "personal_best", "best_consistency_delta", FormatPbDelta(_personalBestProgress.BestConsistency, _personalBestProgress.PreviousBestConsistency));
            AppendCsv(sb, "personal_best", "best_streak", _personalBestProgress.BestStreak.ToString());
            AppendCsv(sb, "personal_best", "best_streak_delta", FormatPbDelta(_personalBestProgress.BestStreak, _personalBestProgress.PreviousBestStreak));
        }

        if (_keyboardMasteryProgress != null)
        {
            AppendCsv(sb, "keyboard_mastery", "race_counted_now", _keyboardMasteryProgress.WasNewRace ? "yes" : "already_counted");
            AppendCsv(sb, "keyboard_mastery", "total_races", _keyboardMasteryProgress.TotalRaces.ToString());
            AppendCsv(sb, "keyboard_mastery", "tracked_keys", _keyboardMasteryProgress.TrackedKeyCount.ToString());
            AppendCsv(sb, "keyboard_mastery", "mastered_keys", $"{_keyboardMasteryProgress.MasteredKeyCount}/{_keyboardMasteryProgress.MasteryTargetKeyCount}");
            AppendCsv(sb, "keyboard_mastery", "coverage_percent", $"{_keyboardMasteryProgress.CoveragePercent:F1}");
            AppendCsv(sb, "keyboard_mastery", "average_accuracy", $"{_keyboardMasteryProgress.AverageAccuracy:F1}");
            AppendCsv(sb, "keyboard_mastery", "needs_review", _keyboardMasteryProgress.NeedsReviewKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.NeedsReviewKeys) : "none");
            AppendCsv(sb, "keyboard_mastery", "strongest_keys", _keyboardMasteryProgress.StrongestKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.StrongestKeys) : "-");
            AppendCsv(sb, "keyboard_mastery", "weakest_keys", _keyboardMasteryProgress.WeakestKeys.Count > 0 ? string.Join(", ", _keyboardMasteryProgress.WeakestKeys) : "-");
        }

        foreach (var result in _result.Results.OrderBy(x => x.Position))
        {
            var prefix = $"#{result.Position}:{result.Username}";
            AppendCsv(sb, "leaderboard", $"{prefix}:wpm", $"{result.Wpm:F1}");
            AppendCsv(sb, "leaderboard", $"{prefix}:accuracy", $"{result.Accuracy:F1}");
            AppendCsv(sb, "leaderboard", $"{prefix}:status", result.IsDisqualified ? "disqualified" : result.IsCompleted ? "completed" : "timeout");
        }

        for (var i = 0; i < _performanceSamples.Count; i++)
        {
            var sample = _performanceSamples[i];
            AppendCsv(sb, "timeline", $"{i}:seconds", $"{sample.Seconds:F1}");
            AppendCsv(sb, "timeline", $"{i}:wpm", $"{sample.Wpm:F1}");
            AppendCsv(sb, "timeline", $"{i}:raw_wpm", $"{sample.RawWpm:F1}");
            AppendCsv(sb, "timeline", $"{i}:accuracy", $"{sample.Accuracy:F1}");
        }

        if (!string.IsNullOrWhiteSpace(_txtAi.Text))
            AppendCsv(sb, "ai", "coach_report", _txtAi.Text.Trim());

        var body = sb.ToString().TrimEnd();
        var signature = ComputeReportSignature(body);
        return body + Environment.NewLine + $"verification_sha256,,{EscapeCsv(signature)}" + Environment.NewLine;
    }

    private object BuildAnalyticsPayload()
    {
        var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var mode = ToGameModeLabel(_gameMode, _aiPracticeDifficulty);
        var leaderboard = _result.Results
            .OrderBy(x => x.Position)
            .Select(x => new
            {
                x.Position,
                x.UserId,
                x.Username,
                x.Wpm,
                x.Accuracy,
                x.BestStreak,
                x.ConsistencyScore,
                x.TimeTakenMs,
                x.IsCompleted,
                x.IsDisqualified,
                x.IsAiBot,
                x.Achievements,
            })
            .ToList();
        var timeline = _performanceSamples
            .Select(x => new
            {
                x.Seconds,
                x.Wpm,
                x.RawWpm,
                x.Accuracy,
            })
            .ToList();

        return new
        {
            GeneratedAt = generatedAt,
            RoomCode = _result.RoomCode,
            RaceId = _result.RaceId,
            Mode = mode,
            Player = _myResult,
            DailyChallengeProgress = _dailyChallengeProgress,
            PersonalBestProgress = _personalBestProgress,
            KeyboardMasteryProgress = _keyboardMasteryProgress,
            Leaderboard = leaderboard,
            PerformanceTimeline = timeline,
            Passage = _passageText,
            AiCoachReport = string.IsNullOrWhiteSpace(_txtAi.Text)
                ? "AI Coach Report: chưa chạy phân tích AI trong màn này."
                : _txtAi.Text.Trim(),
        };
    }

    private string BuildReportFileName()
    {
        var user = SanitizeFileName(_myResult?.Username ?? "player");
        var room = SanitizeFileName(_result.RoomCode);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"TypeRacer_{room}_{user}_{stamp}.txt";
    }

    private string BuildAnalyticsFileName(string extension)
    {
        var user = SanitizeFileName(_myResult?.Username ?? "player");
        var room = SanitizeFileName(_result.RoomCode);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"TypeRacer_Analytics_{room}_{user}_{stamp}.{extension.TrimStart('.')}";
    }

    private static string ComputeReportSignature(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static void AppendCsv(StringBuilder sb, string section, string key, string value)
        => sb.AppendLine($"{EscapeCsv(section)},{EscapeCsv(key)},{EscapeCsv(value)}");

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string((value ?? string.Empty)
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray()).Trim('_', ' ');

        return string.IsNullOrWhiteSpace(clean) ? "report" : clean;
    }

    private static bool IsUsableMission(AiPracticeMissionDto mission)
        => !string.IsNullOrWhiteSpace(mission.Title) &&
           !string.IsNullOrWhiteSpace(mission.Passage) &&
           mission.Passage.Trim().Length >= 40;

    private static string FormatMissionListItem(AiPracticeMissionDto mission)
    {
        var reward = string.IsNullOrWhiteSpace(mission.RewardBadge)
            ? "AI badge"
            : mission.RewardBadge;

        return $"{mission.Title} | {FormatMissionClock(mission.DurationSeconds)} | {mission.TargetWpm:F1} WPM | {mission.TargetAccuracy:F1}% | {reward}";
    }

    private static string FormatMissionClock(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        return $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

    private static string NormalizeLanguage(string? rawLanguage)
    {
        var code = (rawLanguage ?? "en").Trim().ToLowerInvariant();
        return code switch
        {
            "vi" => "vi",
            "en" => "en",
            _ => "en",
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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
