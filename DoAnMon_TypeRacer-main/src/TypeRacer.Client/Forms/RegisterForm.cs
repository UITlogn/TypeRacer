using TypeRacer.Client.State;
using TypeRacer.Client.Theme;
using TypeRacer.Shared.Payloads.Auth;
using TypeRacer.Shared.Protocol;

namespace TypeRacer.Client.Forms;

/// <summary>
/// Form dang ky theo bo cuc table de han che lech tren may DPI khac nhau.
/// </summary>
public class RegisterForm : Form
{
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnRegister = null!;
    private Label _lblStatus = null!;

    public RegisterForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "TypeRacer - Dang ky tai khoan";
        Size = new Size(520, 460);
        MinimumSize = new Size(500, 440);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        BackColor = ClientTheme.BackgroundTop;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ClientTheme.BackgroundTop,
            Padding = new Padding(24),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblTitle = new Label
        {
            Text = "Tao tai khoan moi",
            Font = new Font("Segoe UI", 19f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        var lblSubtitle = new Label
        {
            Text = "Username 3-50 ky tu, chi dung a-z, 0-9, _, -",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ClientTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        var formCard = ClientTheme.CreateCard(new Padding(24, 20, 24, 20));
        formCard.Dock = DockStyle.Fill;
        formCard.Margin = new Padding(36, 0, 36, 0);

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Color.Transparent,
        };
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblUser = CreateFieldLabel("Ten dang nhap");
        _txtUsername = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "username",
        };
        ClientTheme.StyleTextBox(_txtUsername);

        var lblPass = CreateFieldLabel("Mat khau");
        _txtPassword = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8),
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "Toi thieu 6 ky tu",
            UseSystemPasswordChar = true,
        };
        ClientTheme.StyleTextBox(_txtPassword);
        _txtPassword.UseSystemPasswordChar = true;

        var lblConfirm = CreateFieldLabel("Xac nhan mat khau");
        _txtConfirmPassword = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 12),
            Font = new Font("Segoe UI", 10f),
            UseSystemPasswordChar = true,
        };
        ClientTheme.StyleTextBox(_txtConfirmPassword);
        _txtConfirmPassword.UseSystemPasswordChar = true;

        _btnRegister = new Button
        {
            Text = "Dang ky",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
        };
        ClientTheme.StyleButton(_btnRegister, ThemeButtonVariant.Success);
        _btnRegister.Click += BtnRegister_Click;

        _lblStatus = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f),
            ForeColor = ClientTheme.Danger,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        formLayout.Controls.Add(lblUser, 0, 0);
        formLayout.Controls.Add(_txtUsername, 0, 1);
        formLayout.Controls.Add(lblPass, 0, 2);
        formLayout.Controls.Add(_txtPassword, 0, 3);
        formLayout.Controls.Add(lblConfirm, 0, 4);
        formLayout.Controls.Add(_txtConfirmPassword, 0, 5);
        formLayout.Controls.Add(_btnRegister, 0, 6);
        formLayout.Controls.Add(_lblStatus, 0, 7);
        formCard.Controls.Add(formLayout);

        root.Controls.Add(lblTitle, 0, 0);
        root.Controls.Add(lblSubtitle, 0, 1);
        root.Controls.Add(formCard, 0, 2);
        Controls.Add(root);

        AcceptButton = _btnRegister;
        Load += (s, e) => AppState.Instance.Dispatcher.OnRegisterResponse += OnRegisterResponse;
        FormClosed += (s, e) => AppState.Instance.Dispatcher.OnRegisterResponse -= OnRegisterResponse;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ClientTheme.TextPrimary,
        };
    }

    private async void BtnRegister_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtUsername.Text) || string.IsNullOrWhiteSpace(_txtPassword.Text))
        {
            _lblStatus.Text = "Vui long nhap day du thong tin.";
            return;
        }

        if (_txtPassword.Text != _txtConfirmPassword.Text)
        {
            _lblStatus.Text = "Mat khau xac nhan khong khop.";
            return;
        }

        if (_txtPassword.Text.Length < 6)
        {
            _lblStatus.Text = "Mat khau toi thieu 6 ky tu.";
            return;
        }

        _btnRegister.Enabled = false;
        _lblStatus.ForeColor = ClientTheme.TextMuted;
        _lblStatus.Text = "Dang dang ky...";

        try
        {
            if (!AppState.Instance.Client.IsConnected)
            {
                _lblStatus.Text = "Dang ket noi server...";
                await AppState.Instance.ConnectAsync();
            }

            await AppState.Instance.Client.SendAsync(MessageType.REGISTER_REQUEST, new RegisterRequest
            {
                Username = _txtUsername.Text.Trim(),
                Password = _txtPassword.Text,
            });
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = ClientTheme.Danger;
            _lblStatus.Text = $"Loi: {ex.Message}";
            _btnRegister.Enabled = true;
        }
    }

    private void OnRegisterResponse(NetworkMessage message)
    {
        var response = message.GetPayload<RegisterResponse>();
        if (response == null)
            return;

        if (response.Success)
        {
            _lblStatus.ForeColor = ClientTheme.Success;
            _lblStatus.Text = "Dang ky thanh cong! Ban co the dang nhap.";
            _btnRegister.Enabled = true;

            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Close();
            };
            timer.Start();
            return;
        }

        _lblStatus.ForeColor = ClientTheme.Danger;
        _lblStatus.Text = response.ErrorMessage ?? "Dang ky that bai.";
        _btnRegister.Enabled = true;
    }
}
