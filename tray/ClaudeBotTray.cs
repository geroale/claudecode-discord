using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading;

class ClaudeBotTray : Form
{
    private NotifyIcon trayIcon;
    private System.Windows.Forms.Timer refreshTimer;
    private string botDir;
    private string envPath;
    private string taskName = "ClaudeDiscordBot";

    public ClaudeBotTray()
    {
        botDir = Path.GetDirectoryName(Path.GetDirectoryName(Application.ExecutablePath));
        envPath = Path.Combine(botDir, ".env");

        this.ShowInTaskbar = false;
        this.WindowState = FormWindowState.Minimized;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Opacity = 0;

        trayIcon = new NotifyIcon();
        trayIcon.Visible = true;
        UpdateStatus();
        BuildMenu();

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Interval = 5000;
        refreshTimer.Tick += (s, e) => { UpdateStatus(); BuildMenu(); };
        refreshTimer.Start();

        // .env 없으면 설정 창 열기
        if (!File.Exists(envPath))
        {
            Timer t = new System.Windows.Forms.Timer();
            t.Interval = 500;
            t.Tick += (s, e) => { t.Stop(); OpenSettings(null, null); };
            t.Start();
        }
    }

    private bool IsRunning()
    {
        try
        {
            var procs = Process.GetProcessesByName("node");
            foreach (var p in procs)
            {
                try
                {
                    string cmd = GetCommandLine(p.Id);
                    if (cmd != null && cmd.Contains("dist/index.js"))
                        return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private string GetCommandLine(int pid)
    {
        try
        {
            var proc = new Process();
            proc.StartInfo.FileName = "wmic";
            proc.StartInfo.Arguments = $"process where processid={pid} get commandline /format:list";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }
        catch { return null; }
    }

    private Bitmap CreateIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(color), 1, 1, 14, 14);
        }
        return bmp;
    }

    private void UpdateStatus()
    {
        bool running = IsRunning();
        bool hasEnv = File.Exists(envPath);

        if (!hasEnv)
        {
            trayIcon.Icon = Icon.FromHandle(CreateIcon(Color.Orange).GetHicon());
            trayIcon.Text = "Claude Bot: Setup Required";
        }
        else if (running)
        {
            trayIcon.Icon = Icon.FromHandle(CreateIcon(Color.LimeGreen).GetHicon());
            trayIcon.Text = "Claude Bot: Running";
        }
        else
        {
            trayIcon.Icon = Icon.FromHandle(CreateIcon(Color.Red).GetHicon());
            trayIcon.Text = "Claude Bot: Stopped";
        }
    }

    private void BuildMenu()
    {
        bool running = IsRunning();
        bool hasEnv = File.Exists(envPath);

        var menu = new ContextMenuStrip();

        if (!hasEnv)
        {
            var noEnv = new ToolStripMenuItem("⚙️ Setup Required") { Enabled = false };
            menu.Items.Add(noEnv);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Setup...", null, OpenSettings);
        }
        else
        {
            var status = new ToolStripMenuItem(running ? "🟢 Running" : "🔴 Stopped") { Enabled = false };
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());

            if (running)
            {
                menu.Items.Add("Stop Bot", null, StopBot);
                menu.Items.Add("Restart Bot", null, RestartBot);
            }
            else
            {
                menu.Items.Add("Start Bot", null, StartBot);
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings...", null, OpenSettings);
            menu.Items.Add("View Log", null, OpenLog);
            menu.Items.Add("Open Folder", null, OpenFolder);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, QuitAll);

        trayIcon.ContextMenuStrip = menu;
    }

    private void StartBot(object sender, EventArgs e)
    {
        RunCmd($"\"{Path.Combine(botDir, "win-start.bat")}\"", false);
        Thread.Sleep(2000);
        UpdateStatus();
        BuildMenu();
    }

    private void StopBot(object sender, EventArgs e)
    {
        RunCmd($"\"{Path.Combine(botDir, "win-start.bat")}\" --stop", true);
        Thread.Sleep(1000);
        UpdateStatus();
        BuildMenu();
    }

    private void RestartBot(object sender, EventArgs e)
    {
        RunCmd($"\"{Path.Combine(botDir, "win-start.bat")}\" --stop", true);
        Thread.Sleep(2000);
        RunCmd($"\"{Path.Combine(botDir, "win-start.bat")}\"", false);
        Thread.Sleep(2000);
        UpdateStatus();
        BuildMenu();
    }

    private void OpenLog(object sender, EventArgs e)
    {
        string logPath = Path.Combine(botDir, "bot.log");
        if (File.Exists(logPath))
            Process.Start("notepad.exe", logPath);
    }

    private void OpenFolder(object sender, EventArgs e)
    {
        Process.Start("explorer.exe", botDir);
    }

    private void OpenSettings(object sender, EventArgs e)
    {
        var env = LoadEnv();

        var form = new Form()
        {
            Text = "Claude Discord Bot Settings",
            Width = 500,
            Height = 400,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        string[][] fields = new string[][] {
            new string[] { "DISCORD_BOT_TOKEN", "Discord Bot Token" },
            new string[] { "DISCORD_GUILD_ID", "Discord Guild ID" },
            new string[] { "ALLOWED_USER_IDS", "Allowed User IDs (comma-separated)" },
            new string[] { "BASE_PROJECT_DIR", "Base Project Directory" },
            new string[] { "RATE_LIMIT_PER_MINUTE", "Rate Limit Per Minute" },
            new string[] { "SHOW_COST", "Show Cost (true/false)" },
        };

        string[] defaults = new string[] { "", "", "", botDir, "10", "true" };

        var textBoxes = new TextBox[fields.Length];
        int y = 15;

        for (int i = 0; i < fields.Length; i++)
        {
            var label = new Label() { Text = fields[i][1], Left = 15, Top = y, Width = 450, Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold) };
            form.Controls.Add(label);
            y += 20;

            var tb = new TextBox() { Left = 15, Top = y, Width = 450 };
            string val = "";
            env.TryGetValue(fields[i][0], out val);

            if (fields[i][0] == "DISCORD_BOT_TOKEN" && val != null && val.Length > 10)
            {
                tb.PlaceholderText = "••••" + val.Substring(val.Length - 6) + " (enter full token to change)";
            }
            else
            {
                tb.Text = (val != null && val != "") ? val : defaults[i];
            }

            form.Controls.Add(tb);
            textBoxes[i] = tb;
            y += 30;
        }

        var note = new Label() { Text = "* Max plan users should set Show Cost to false", Left = 15, Top = y, Width = 450, ForeColor = Color.Gray };
        form.Controls.Add(note);
        y += 25;

        var saveBtn = new Button() { Text = "Save", Left = 300, Top = y, Width = 80 };
        var cancelBtn = new Button() { Text = "Cancel", Left = 385, Top = y, Width = 80 };

        saveBtn.Click += (s, ev) =>
        {
            string[] values = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                values[i] = textBoxes[i].Text.Trim();
                if (values[i] == "" && fields[i][0] == "DISCORD_BOT_TOKEN")
                {
                    string existing = "";
                    env.TryGetValue(fields[i][0], out existing);
                    values[i] = existing ?? "";
                }
                if (values[i] == "") values[i] = defaults[i];
            }

            if (values[0] == "" || values[1] == "" || values[2] == "")
            {
                MessageBox.Show("Bot Token, Guild ID, and User IDs are required.", "Required Fields Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sw = new StreamWriter(envPath))
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i][0] == "SHOW_COST")
                        sw.WriteLine("# Show estimated API cost in task results (set false for Max plan users)");
                    sw.WriteLine($"{fields[i][0]}={values[i]}");
                }
            }

            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        cancelBtn.Click += (s, ev) => { form.Close(); };

        form.Controls.Add(saveBtn);
        form.Controls.Add(cancelBtn);
        form.AcceptButton = saveBtn;
        form.CancelButton = cancelBtn;
        form.ShowDialog();

        UpdateStatus();
        BuildMenu();
    }

    private System.Collections.Generic.Dictionary<string, string> LoadEnv()
    {
        var env = new System.Collections.Generic.Dictionary<string, string>();
        if (!File.Exists(envPath)) return env;

        foreach (var line in File.ReadAllLines(envPath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;
            int idx = trimmed.IndexOf('=');
            string key = trimmed.Substring(0, idx);
            string val = trimmed.Substring(idx + 1);
            env[key] = val;
        }
        return env;
    }

    private void QuitAll(object sender, EventArgs e)
    {
        if (IsRunning())
        {
            RunCmd($"\"{Path.Combine(botDir, "win-start.bat")}\" --stop", true);
        }
        trayIcon.Visible = false;
        Application.Exit();
    }

    private void RunCmd(string command, bool wait)
    {
        var proc = new Process();
        proc.StartInfo.FileName = "cmd.exe";
        proc.StartInfo.Arguments = $"/c {command}";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.Start();
        if (wait) proc.WaitForExit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ClaudeBotTray());
    }
}
