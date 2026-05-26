using TypeRacer.Client.Controls;
using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Payloads.Stats;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form bảng xếp hạng: hiển thị top người chơi theo WPM/điểm xếp hạng.
/// </summary>
public class LeaderboardForm : Form
{
    private DataGridView _dgv = null!;
    private ComboBox _cmbSortBy = null!;
    private Label _lblMeta = null!;

    public LeaderboardForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Bảng xếp hạng";
        Size = new Size(980, 640);
        MinimumSize = new Size(820, 520);
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
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerCard = new GradientPanel
        {
            Dock = DockStyle.Fill,
            StartColor = ClientTheme.HeaderStart,
            EndColor = ClientTheme.HeaderEnd,
            Angle = 138,
            CornerRadius = 20,
            Padding = new Padding(18, 14, 18, 12),
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

        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        titleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        titleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "Bảng xếp hạng TypeRacer",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 19f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _lblMeta = new Label
        {
            Text = "Top 20 người chơi theo bộ lọc hiện tại.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(205, 220, 248),
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        titleLayout.Controls.Add(lblTitle, 0, 0);
        titleLayout.Controls.Add(_lblMeta, 0, 1);

        var filterBar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            WrapContents = true,
            AutoScroll = true,
            Margin = new Padding(0, 8, 0, 0),
            Anchor = AnchorStyles.Right,
        };

        var lblSort = new Label
        {
            Text = "Sắp xếp:",
            ForeColor = Color.FromArgb(218, 230, 252),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
        };

        _cmbSortBy = new ComboBox
        {
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 10, 0),
        };
        _cmbSortBy.Items.AddRange(new object[]
        {
            "WPM trung bình",
            "Số trận thắng",
        });
        _cmbSortBy.SelectedIndex = 0;
        _cmbSortBy.SelectedIndexChanged += CmbSortBy_Changed;
        ClientTheme.StyleComboBox(_cmbSortBy);

        var btnRefresh = new Button
        {
            Text = "Làm mới",
            Size = new Size(104, 44),
            Margin = new Padding(0),
        };
        ClientTheme.StyleButton(btnRefresh, ThemeButtonVariant.Accent, compact: true);
        btnRefresh.Click += (_, _) => RequestLeaderboard();

        filterBar.Controls.Add(lblSort);
        filterBar.Controls.Add(_cmbSortBy);
        filterBar.Controls.Add(btnRefresh);

        headerLayout.Controls.Add(titleLayout, 0, 0);
        headerLayout.Controls.Add(filterBar, 1, 0);
        headerCard.Controls.Add(headerLayout);

        var boardCard = ClientTheme.CreateCard(new Padding(16));

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
            Text = "Top Players",
            Dock = DockStyle.Fill,
            ForeColor = ClientTheme.TextPrimary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
        };
        ClientTheme.StyleDataGridView(_dgv);
        _dgv.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Rank", HeaderText = "#", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Người chơi", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "AvgWpm", HeaderText = "WPM TB", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "BestWpm", HeaderText = "WPM Max", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Races", HeaderText = "Tổng trận", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Wins", HeaderText = "Thắng", FillWeight = 15 },
        });

        boardLayout.Controls.Add(lblBoard, 0, 0);
        boardLayout.Controls.Add(_dgv, 0, 1);
        boardCard.Controls.Add(boardLayout);

        pageLayout.Controls.Add(headerCard, 0, 0);
        pageLayout.Controls.Add(boardCard, 0, 1);
        page.Controls.Add(pageLayout);
        Controls.Add(ClientTheme.CreateScrollablePageHost(page, 700));

        Load += LeaderboardForm_Load;
    }

    private void LeaderboardForm_Load(object? sender, EventArgs e)
    {
        AppState.Instance.Dispatcher.OnLeaderboardResponse += OnLeaderboardResponse;
        RequestLeaderboard();
    }

    private async void RequestLeaderboard()
    {
        var sortBy = _cmbSortBy.SelectedIndex switch
        {
            1 => "total_wins",
            _ => "avg_wpm",
        };

        try
        {
            await AppState.Instance.Client.SendAsync(MessageType.GET_LEADERBOARD, new GetLeaderboardRequest
            {
                SortBy = sortBy,
                Top = 20,
            });
        }
        catch
        {
            _lblMeta.Text = "Không thể tải leaderboard. Kiểm tra kết nối server.";
        }
    }

    private void OnLeaderboardResponse(NetworkMessage message)
    {
        var response = message.GetPayload<LeaderboardResponse>();
        if (response == null)
            return;

        _dgv.Rows.Clear();
        foreach (var entry in response.Entries)
        {
            var row = _dgv.Rows.Add(
                $"#{entry.Rank}",
                entry.Username,
                $"{entry.AvgWpm:F1}",
                $"{entry.BestWpm:F1}",
                entry.TotalRaces,
                entry.TotalWins
            );

            if (entry.UserId == AppState.Instance.CurrentUser?.Id)
            {
                _dgv.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
                _dgv.Rows[row].DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            }
        }

        var sortText = _cmbSortBy.SelectedIndex == 1 ? "Số trận thắng" : "WPM trung bình";
        _lblMeta.Text = $"Top {response.Entries.Count} người chơi | Sắp xếp theo {sortText} | Cập nhật {DateTime.Now:HH:mm:ss}";
    }

    private void CmbSortBy_Changed(object? sender, EventArgs e)
    {
        RequestLeaderboard();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        AppState.Instance.Dispatcher.OnLeaderboardResponse -= OnLeaderboardResponse;
        base.OnFormClosed(e);
    }
}
