using System.Drawing.Imaging;
using TypeRacer.Client.Controls;
using TypeRacer.Client.Forms;
using TypeRacer.Client.State;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Ai;
using TypeRacer.Shared.Payloads.Game;

namespace TypeRacer.UiProbe;

internal static class Program
{
    private static readonly string OutputDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts", "ui-probe"));

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        AppState.Instance.InitializeDispatcher();
        AppState.Instance.CurrentUser = new UserDto { Id = 1, Username = "LayoutTester" };
        AppState.Instance.SessionToken = "ui-probe";

        Directory.CreateDirectory(OutputDir);
        var issues = new List<string>();

        foreach (var spec in BuildFormSpecs())
        {
            using var form = spec.Factory();
            ProbeForm(spec.Name, form, issues);
        }

        File.WriteAllLines(Path.Combine(OutputDir, "ui-probe-report.txt"), issues.Count == 0
            ? new[] { "UI PROBE PASSED" }
            : issues);

        if (issues.Count == 0)
        {
            Console.WriteLine($"UI PROBE PASSED ({BuildFormSpecs().Count} forms)");
            Console.WriteLine(OutputDir);
            return 0;
        }

        Console.Error.WriteLine($"UI PROBE FAILED ({issues.Count} issues)");
        foreach (var issue in issues)
            Console.Error.WriteLine(issue);
        Console.Error.WriteLine(OutputDir);
        return 1;
    }

    private static List<FormSpec> BuildFormSpecs()
    {
        const string passage = "Những dòng chữ nhỏ giúp kiểm tra tiếng Việt có dấu, dấu câu, tốc độ và cách UI xuống dòng trong các vùng hẹp.";
        var mission = new AiPracticeMissionDto
        {
            Title = "Luyện dấu tiếng Việt",
            Objective = "Tập trung sửa các lỗi dấu sắc, hỏi, ngã trong một đoạn ngắn.",
            RewardBadge = "Vietnamese Precision",
            DurationSeconds = 180,
            TargetWpm = 45,
            TargetAccuracy = 96,
        };

        var result = new RaceResultPayload
        {
            RoomCode = "ABC123",
            RaceId = 4242,
            Results =
            {
                new RaceResultDto
                {
                    UserId = 1,
                    Username = "LayoutTester",
                    Position = 1,
                    Wpm = 72.4m,
                    Accuracy = 97.8m,
                    CharsCorrect = 240,
                    CharsWrong = 5,
                    TimeTakenMs = 118000,
                    IsCompleted = true,
                    TypedText = passage,
                    BestStreak = 64,
                    ConsistencyScore = 91.5m,
                    Achievements = new List<string> { "Daily Gold", "Precision" },
                    ObservedMistakeCharacters = new Dictionary<string, int> { ["ê"] = 4, ["á"] = 3 },
                    ObservedMistakeWords = new Dictionary<string, int> { ["nghĩa"] = 2 },
                    ObservedMistakeNgrams = new Dictionary<string, int> { ["th"] = 3 },
                },
                new RaceResultDto
                {
                    UserId = 2,
                    Username = "Bot Alpha",
                    Position = 2,
                    Wpm = 58.1m,
                    Accuracy = 94.2m,
                    TimeTakenMs = 145000,
                    IsCompleted = true,
                    IsAiBot = true,
                    BestStreak = 40,
                    ConsistencyScore = 80,
                },
            },
        };

        var samples = new[]
        {
            new TypingPerformanceSample(10, 54, 62, 96),
            new TypingPerformanceSample(30, 68, 75, 97),
            new TypingPerformanceSample(60, 72, 80, 98),
        };

        return new List<FormSpec>
        {
            new("LoginForm", () => new LoginForm()),
            new("RegisterForm", () => new RegisterForm()),
            new("MainForm", () => new MainForm()),
            new("RoomForm", () => new RoomForm("ABC123", true, "vi", 180, true, "ai_practice", "nightmare", true)),
            new("RaceForm", () => new RaceForm("ABC123", passage, true, "vi", "vi", 180, true, "ai_practice", "nightmare", true)),
            new("PracticeForm", () => new PracticeForm(passage, "vi", mission)),
            new("LeaderboardForm", () => new LeaderboardForm()),
            new("ProfileForm", () => new ProfileForm()),
            new("ResultForm", () => new ResultForm(result, passage, "vi", 180, "ai_practice", "nightmare", samples)),
        };
    }

    private static void ProbeForm(string name, Form form, List<string> issues)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(20, 20);
        form.Size = form.MinimumSize.IsEmpty ? form.Size : form.MinimumSize;

        form.Show();
        for (var i = 0; i < 4; i++)
        {
            form.PerformLayout();
            Application.DoEvents();
            Thread.Sleep(40);
        }

        AuditControl(name, form, issues);
        SaveScreenshot(name, form);
        form.Close();
        Application.DoEvents();
    }

    private static void AuditControl(string formName, Control parent, List<string> issues)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible)
                continue;

            if (!ParentAllowsOverflow(parent) && IsImportantControl(child) && !IsInsideParentClient(child))
            {
                issues.Add($"{formName}: {Describe(child)} is clipped by {Describe(parent)}. Bounds={child.Bounds}, ParentClient={parent.ClientRectangle}");
            }

            if (child is Button button)
            {
                if (button.Width < 44 || button.Height < 44)
                    issues.Add($"{formName}: button '{button.Text}' has small target {button.Width}x{button.Height}");
            }

            if (child is FlowLayoutPanel flow)
                AuditFlowLayout(formName, flow, issues);

            if (child.HasChildren)
                AuditControl(formName, child, issues);
        }
    }

    private static void AuditFlowLayout(string formName, FlowLayoutPanel flow, List<string> issues)
    {
        if (!flow.WrapContents)
            issues.Add($"{formName}: FlowLayoutPanel {Describe(flow)} does not wrap controls");

        if (!flow.AutoScroll)
            issues.Add($"{formName}: FlowLayoutPanel {Describe(flow)} does not scroll overflow");

        if (flow.HorizontalScroll.Visible || flow.VerticalScroll.Visible)
            issues.Add($"{formName}: FlowLayoutPanel {Describe(flow)} shows an internal scrollbar at minimum size");

        foreach (Control child in flow.Controls)
        {
            if (!child.Visible)
                continue;

            if (IsImportantControl(child) && child.Bottom > flow.DisplayRectangle.Bottom && !flow.AutoScroll)
                issues.Add($"{formName}: {Describe(child)} overflows non-scrollable flow {Describe(flow)}");
        }
    }

    private static bool ParentAllowsOverflow(Control parent)
    {
        if (parent is ScrollableControl { AutoScroll: true })
            return true;

        return parent is FlowLayoutPanel { AutoScroll: true };
    }

    private static bool IsImportantControl(Control control)
        => control is Button or TextBox or ComboBox or CheckBox or NumericUpDown or DataGridView or ListBox or RichTextBox;

    private static bool IsInsideParentClient(Control control)
    {
        var parent = control.Parent;
        if (parent == null)
            return true;

        var rect = parent.ClientRectangle;
        return control.Left >= rect.Left &&
               control.Top >= rect.Top &&
               control.Right <= rect.Right &&
               control.Bottom <= rect.Bottom;
    }

    private static string Describe(Control control)
    {
        var name = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name;
        var text = string.IsNullOrWhiteSpace(control.Text) ? string.Empty : $" '{control.Text}'";
        return $"{name}{text}";
    }

    private static void SaveScreenshot(string name, Form form)
    {
        using var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        try
        {
            var screenPoint = form.PointToScreen(Point.Empty);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(screenPoint, Point.Empty, bitmap.Size);
        }
        catch
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        }
        bitmap.Save(Path.Combine(OutputDir, $"{name}.png"), ImageFormat.Png);
    }

    private sealed record FormSpec(string Name, Func<Form> Factory);
}
