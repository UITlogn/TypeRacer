using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Payloads.Stats;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form hồ sơ cá nhân: thống kê WPM, accuracy, số trận, lịch sử.
/// </summary>
public class ProfileForm : Form
{
    private Label _lblUsername = null!;
    private Label _lblMeta = null!;
    private Label _lblTotalRaces = null!;
    private Label _lblAvgWpm = null!;
    private Label _lblBestWpm = null!;
    private Label _lblAvgAccuracy = null!;
    private DataGridView _dgvHistory = null!;

    public ProfileForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Hồ sơ cá nhân";
        Size = new Size(980, 680);
        MinimumSize = new Size(860, 560);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ClientTheme.BackgroundTop;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var page = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.BackgroundTop,
            EndColor = ClientTheme.BackgroundBottom,
            Angle = 120,
            Padding = new Padding(18),
        };

        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 140,
            CornerRadius = 20,
            Padding = new Padding(18, 14, 18, 12),
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

        _lblUsername = new Label
        {
            Text = AppState.Instance.CurrentUser?.Username ?? "Player",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        _lblMeta = new Label
        {
            Text = "Theo dõi tiến bộ cá nhân theo từng trận đấu.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(205, 220, 248),
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        headerLayout.Controls.Add(_lblUsername, 0, 0);
        headerLayout.Controls.Add(_lblMeta, 0, 1);
        headerCard.Controls.Add(headerLayout);

        var statsCard = ClientTheme.CreateCard(new Padding(14, 12, 14, 12));
        statsCard.Margin = new Padding(0, 0, 0, 14);

        var statsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        statsLayout.Controls.Add(CreateStatBlock("Tổng trận", out _lblTotalRaces), 0, 0);
        statsLayout.Controls.Add(CreateStatBlock("WPM trung bình", out _lblAvgWpm), 1, 0);
        statsLayout.Controls.Add(CreateStatBlock("WPM tốt nhất", out _lblBestWpm), 2, 0);
        statsLayout.Controls.Add(CreateStatBlock("Accuracy TB", out _lblAvgAccuracy), 3, 0);

        statsCard.Controls.Add(statsLayout);

        var historyCard = ClientTheme.CreateCard(new Padding(16));

        var historyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblHistory = new Label
        {
            Text = "Lịch sử trận đấu gần đây",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _dgvHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        ClientTheme.StyleDataGridView(_dgvHistory);
        _dgvHistory.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Ngày", FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "Room", HeaderText = "Phòng", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Pos", HeaderText = "Hạng", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "Wpm", HeaderText = "WPM", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Acc", HeaderText = "Chính xác", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", FillWeight = 16 },
        });

        historyLayout.Controls.Add(lblHistory, 0, 0);
        historyLayout.Controls.Add(_dgvHistory, 0, 1);
        historyCard.Controls.Add(historyLayout);

        pageLayout.Controls.Add(headerCard, 0, 0);
        pageLayout.Controls.Add(statsCard, 0, 1);
        pageLayout.Controls.Add(historyCard, 0, 2);
        page.Controls.Add(pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 720));

        Load += ProfileForm_Load;
    }

    private static Control CreateStatBlock(string title, out Label valueLabel)
    {
        var block = new CardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(247, 251, 255),
            BorderColor = ClientTheme.Border,
            CornerRadius = 14,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(6, 0, 6, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextMuted,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel = new Label
        {
            Text = "--",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        layout.Controls.Add(lblTitle, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        block.Controls.Add(layout);
        return block;
    }

    private async void ProfileForm_Load(object? sender, EventArgs e)
    {
        AppState.Instance.Dispatcher.OnMatchHistoryResponse += OnMatchHistoryResponse;
        AppState.Instance.Dispatcher.OnProfileResponse += OnProfileResponse;

        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.GET_PROFILE, new GetProfileRequest());
            await AppState.Instance.Client.SendAsync(MessageType.GET_MATCH_HISTORY, new GetMatchHistoryRequest { Limit = 20 });
        }
        catch
        {
            _lblMeta.Text = "Không thể tải dữ liệu profile. Vui lòng thử lại.";
        }
    }

    private void OnProfileResponse(NetworkMessage message)
    {
        var response = message.GetPayload<ProfileResponse>();
        if (response?.Success != true || response.User == null)
            return;

        AppState.Instance.CurrentUser = response.User;
        _lblUsername.Text = response.User.Username;
    }

    private void OnMatchHistoryResponse(NetworkMessage message)
    {
        var response = message.GetPayload<MatchHistoryResponse>();
        if (response == null)
            return;

        _dgvHistory.Rows.Clear();
        foreach (var m in response.Matches)
        {
            _dgvHistory.Rows.Add(
                m.PlayedAt.ToString("dd/MM HH:mm"),
                m.RoomCode,
                $"#{m.Position}/{m.TotalPlayers}",
                $"{m.Wpm:F1}",
                $"{m.Accuracy:F1}%",
                m.IsCompleted ? "Hoàn thành" : "Chưa xong"
            );
        }

        if (response.Matches.Count == 0)
        {
            _lblTotalRaces.Text = "0";
            _lblAvgWpm.Text = "0.0";
            _lblBestWpm.Text = "0.0";
            _lblAvgAccuracy.Text = "0.0%";
            _lblMeta.Text = "Chưa có trận nào, hãy bắt đầu race đầu tiên.";
            return;
        }

        var total = response.Matches.Count;
        var avgWpm = response.Matches.Average(x => x.Wpm);
        var bestWpm = response.Matches.Max(x => x.Wpm);
        var avgAcc = response.Matches.Average(x => x.Accuracy);

        _lblTotalRaces.Text = total.ToString();
        _lblAvgWpm.Text = $"{avgWpm:F1}";
        _lblBestWpm.Text = $"{bestWpm:F1}";
        _lblAvgAccuracy.Text = $"{avgAcc:F1}%";
        _lblMeta.Text = $"Cập nhật lúc {DateTime.Now:HH:mm:ss} | {total} trận gần nhất.";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        AppState.Instance.Dispatcher.OnMatchHistoryResponse -= OnMatchHistoryResponse;
        AppState.Instance.Dispatcher.OnProfileResponse -= OnProfileResponse;
        base.OnFormClosed(e);
    }
}
