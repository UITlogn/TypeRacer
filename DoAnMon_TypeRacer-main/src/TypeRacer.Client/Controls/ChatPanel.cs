using TypeRacer.Shared.Models;
using TypeRacer.Client.Theme;

namespace TypeRacer.Client.Controls;

/// <summary>
/// Panel chat: hiển thị danh sách tin nhắn và ô nhập tin nhắn.
/// </summary>
public class ChatPanel : UserControl
{
    private readonly ListBox _messageList;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;

    /// <summary>Sự kiện khi người dùng gửi tin nhắn</summary>
    public event Action<string>? MessageSent;

    public ChatPanel()
    {
        // Danh sách tin nhắn
        _messageList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = SelectionMode.None,
            IntegralHeight = false,
        };

        // Panel nhập tin nhắn (dưới cùng)
        var inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(2, 3, 2, 3),
        };

        _inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            PlaceholderText = "Nhập tin nhắn...",
        };
        _inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        };

        _sendButton = new Button
        {
            Dock = DockStyle.Right,
            Text = "Gửi",
            Width = 72,
            MinimumSize = new Size(68, 44),
        };
        ClientTheme.StyleButton(_sendButton, ThemeButtonVariant.Primary, compact: true);
        _sendButton.Click += (s, e) => SendMessage();

        inputPanel.Controls.Add(_inputBox);
        inputPanel.Controls.Add(_sendButton);

        Controls.Add(_messageList);
        Controls.Add(inputPanel);
    }

    /// <summary>Thêm tin nhắn vào danh sách</summary>
    public void AddMessage(ChatMessageDto message)
    {
        var time = DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).LocalDateTime;
        string display = $"[{time:HH:mm}] {message.Username}: {message.Content}";
        _messageList.Items.Add(display);

        // Cuộn xuống tin nhắn mới nhất
        _messageList.TopIndex = _messageList.Items.Count - 1;
    }

    /// <summary>Thêm thông báo hệ thống</summary>
    public void AddSystemMessage(string text)
    {
        _messageList.Items.Add($"--- {text} ---");
        _messageList.TopIndex = _messageList.Items.Count - 1;
    }

    /// <summary>Xóa tất cả tin nhắn</summary>
    public void ClearMessages()
    {
        _messageList.Items.Clear();
    }

    private void SendMessage()
    {
        var text = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        MessageSent?.Invoke(text);
        _inputBox.Clear();
        _inputBox.Focus();
    }
}
