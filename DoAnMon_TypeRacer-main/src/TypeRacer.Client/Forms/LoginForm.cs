using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Payloads.Auth;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form dang nhap dau tien khi mo ung dung.
/// Dung bo cuc dock/table de giam lech khi mo tren may co DPI khac nhau.
/// </summary>
public class LoginForm : Form
{
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private ComboBox _cmbConnectionPreset = null!;
    private TextBox _txtHost = null!;
    private NumericUpDown _numPort = null!;
    private Button _btnLogin = null!;
    private Button _btnRegister = null!;
    private Label _lblStatus = null!;
    private bool _updatingConnectionPreset;
    private bool _loginHandled;

    public LoginForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Dang nhap";
        Size = new Size(560, 560);
        MinimumSize = new Size(520, 540);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        BackColor = ClientTheme.BackgroundTop;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = ClientTheme.BackgroundTop,
            Padding = new Padding(24),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "TypeRacer",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        var lblSubtitle = new Label
        {
            Text = "Dang nhap de bat dau dua go phim",
            Font = new Font("Segoe UI", 10f),
            ForeColor = ClientTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        var connectionCard = ClientTheme.CreateCard(new Padding(18, 16, 18, 16));
        connectionCard.Dock = DockStyle.Fill;
        connectionCard.Margin = new Padding(52, 0, 52, 14);

        var connectionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        connectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var lblPreset = CreateFieldLabel("Ket noi");
        _cmbConnectionPreset = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 9.5f),
        };
        _cmbConnectionPreset.Items.AddRange(new object[]
        {
            "Internet (ngoai Wi-Fi)",
            "Wi-Fi/LAN",
            "Radmin VPN",
            "Tuy chinh",
        });
        ClientTheme.StyleComboBox(_cmbConnectionPreset);
        _cmbConnectionPreset.SelectedIndexChanged += ConnectionPreset_SelectedIndexChanged;

        var lblServer = CreateFieldLabel("Server");
        var hostPortLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };
        hostPortLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        hostPortLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        hostPortLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        _txtHost = new TextBox
        {
            Text = AppState.Instance.ServerHost,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Font = new Font("Segoe UI", 9.5f),
        };
        ClientTheme.StyleTextBox(_txtHost);

        var lblPort = new Label
        {
            Text = "Port",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };

        _numPort = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = AppState.Instance.ServerPort,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
        };
        _numPort.BackColor = ClientTheme.Surface;
        _numPort.ForeColor = ClientTheme.TextPrimary;

        _txtHost.TextChanged += (_, _) => SyncConnectionPresetFromFields();
        _numPort.ValueChanged += (_, _) => SyncConnectionPresetFromFields();

        hostPortLayout.Controls.Add(_txtHost, 0, 0);
        hostPortLayout.Controls.Add(lblPort, 1, 0);
        hostPortLayout.Controls.Add(_numPort, 2, 0);

        connectionLayout.Controls.Add(lblPreset, 0, 0);
        connectionLayout.Controls.Add(_cmbConnectionPreset, 1, 0);
        connectionLayout.Controls.Add(lblServer, 0, 1);
        connectionLayout.Controls.Add(hostPortLayout, 1, 1);
        connectionCard.Controls.Add(connectionLayout);

        var formCard = ClientTheme.CreateCard(new Padding(24, 20, 24, 20));
        formCard.Dock = DockStyle.Fill;
        formCard.Margin = new Padding(52, 0, 52, 0);

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent,
        };
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblUsername = new Label
        {
            Text = "Ten dang nhap",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
        };

        _txtUsername = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
            Font = new Font("Segoe UI", 10.5f),
            PlaceholderText = "Nhap username...",
        };
        ClientTheme.StyleTextBox(_txtUsername);

        var lblPassword = new Label
        {
            Text = "Mat khau",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
        };

        _txtPassword = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 12),
            Font = new Font("Segoe UI", 10.5f),
            PlaceholderText = "Nhap mat khau...",
            UseSystemPasswordChar = true,
        };
        ClientTheme.StyleTextBox(_txtPassword);
        _txtPassword.UseSystemPasswordChar = true;

        var buttonLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 8),
        };
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _btnLogin = new Button
        {
            Text = "Dang nhap",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
        };
        ClientTheme.StyleButton(_btnLogin, ThemeButtonVariant.Primary);
        _btnLogin.Click += BtnLogin_Click;

        _btnRegister = new Button
        {
            Text = "Dang ky",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
        };
        ClientTheme.StyleButton(_btnRegister, ThemeButtonVariant.Success);
        _btnRegister.Click += BtnRegister_Click;

        buttonLayout.Controls.Add(_btnLogin, 0, 0);
        buttonLayout.Controls.Add(_btnRegister, 1, 0);

        _lblStatus = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f),
            ForeColor = ClientTheme.Danger,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        formLayout.Controls.Add(lblUsername, 0, 0);
        formLayout.Controls.Add(_txtUsername, 0, 1);
        formLayout.Controls.Add(lblPassword, 0, 2);
        formLayout.Controls.Add(_txtPassword, 0, 3);
        formLayout.Controls.Add(buttonLayout, 0, 4);
        formLayout.Controls.Add(_lblStatus, 0, 5);
        formCard.Controls.Add(formLayout);

        root.Controls.Add(lblTitle, 0, 0);
        root.Controls.Add(lblSubtitle, 0, 1);
        root.Controls.Add(connectionCard, 0, 2);
        root.Controls.Add(formCard, 0, 3);
        Controls.Add(root);

        SyncConnectionPresetFromFields();
        AcceptButton = _btnLogin;

        Load += (s, e) =>
        {
            AppState.Instance.InitializeDispatcher();
            AppState.Instance.Dispatcher.OnLoginResponse += OnLoginResponse;
        };

        VisibleChanged += (s, e) =>
        {
            if (!Visible)
                return;

            _btnLogin.Enabled = true;
            _btnRegister.Enabled = true;
            _lblStatus.Text = "";
            _loginHandled = false;
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ClientTheme.TextMuted,
        };
    }

    private void ConnectionPreset_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updatingConnectionPreset)
            return;

        switch (_cmbConnectionPreset.SelectedIndex)
        {
            case 0:
                ApplyConnectionPreset(AppState.InternetServerHost, AppState.InternetServerPort);
                break;
            case 1:
                ApplyConnectionPreset(AppState.LanServerHost, AppState.LanServerPort);
                break;
            case 2:
                ApplyConnectionPreset(AppState.RadminVpnHost, AppState.RadminVpnPort);
                break;
        }
    }

    private void ApplyConnectionPreset(string host, int port)
    {
        _updatingConnectionPreset = true;
        try
        {
            _txtHost.Text = host;
            _numPort.Value = port;
        }
        finally
        {
            _updatingConnectionPreset = false;
        }
    }

    private void SyncConnectionPresetFromFields()
    {
        if (_updatingConnectionPreset || _cmbConnectionPreset == null)
            return;

        var host = _txtHost.Text.Trim();
        var port = (int)_numPort.Value;
        var selectedIndex =
            IsConnectionPreset(host, port, AppState.InternetServerHost, AppState.InternetServerPort) ? 0 :
            IsConnectionPreset(host, port, AppState.LanServerHost, AppState.LanServerPort) ? 1 :
            IsConnectionPreset(host, port, AppState.RadminVpnHost, AppState.RadminVpnPort) ? 2 :
            3;

        if (_cmbConnectionPreset.SelectedIndex == selectedIndex)
            return;

        _updatingConnectionPreset = true;
        try
        {
            _cmbConnectionPreset.SelectedIndex = selectedIndex;
        }
        finally
        {
            _updatingConnectionPreset = false;
        }
    }

    private static bool IsConnectionPreset(string host, int port, string presetHost, int presetPort)
    {
        return port == presetPort &&
               string.Equals(host, presetHost, StringComparison.OrdinalIgnoreCase);
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _lblStatus.Text = "Vui long nhap day du thong tin.";
            return;
        }

        _btnLogin.Enabled = false;
        _btnRegister.Enabled = false;
        _lblStatus.ForeColor = ClientTheme.TextMuted;
        _lblStatus.Text = "Dang ket noi...";

        try
        {
            AppState.Instance.ServerHost = _txtHost.Text.Trim();
            AppState.Instance.ServerPort = (int)_numPort.Value;

            await AppState.Instance.ConnectAsync();

            _lblStatus.Text = "Dang dang nhap...";

            await AppState.Instance.Client.SendAsync(MessageType.LOGIN_REQUEST, new LoginRequest
            {
                Username = username,
                Password = password,
            });
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = ClientTheme.Danger;
            _lblStatus.Text = $"Loi ket noi: {ex.Message}";
            _btnLogin.Enabled = true;
            _btnRegister.Enabled = true;
        }
    }

    private void OnLoginResponse(NetworkMessage message)
    {
        var response = message.GetPayload<LoginResponse>();
        if (response == null)
            return;

        if (response.Success)
        {
            if (_loginHandled)
                return;

            _loginHandled = true;
            AppState.Instance.CurrentUser = response.User;
            AppState.Instance.SessionToken = response.SessionToken;

            _lblStatus.ForeColor = ClientTheme.Success;
            _lblStatus.Text = $"Dang nhap thanh cong! Xin chao {response.User?.Username}";

            var mainForm = new MainForm();
            mainForm.FormClosed += (s, e) =>
            {
                if (!Visible)
                    Show();
            };
            mainForm.Show();
            Hide();
            return;
        }

        _lblStatus.ForeColor = ClientTheme.Danger;
        _lblStatus.Text = response.ErrorMessage ?? "Dang nhap that bai.";
        _btnLogin.Enabled = true;
        _btnRegister.Enabled = true;
    }

    private void BtnRegister_Click(object? sender, EventArgs e)
    {
        AppState.Instance.ServerHost = _txtHost.Text.Trim();
        AppState.Instance.ServerPort = (int)_numPort.Value;

        using var registerForm = new RegisterForm();
        registerForm.ShowDialog(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        AppState.Instance.Dispatcher.OnLoginResponse -= OnLoginResponse;
        base.OnFormClosed(e);
        Application.Exit();
    }
}
