using System.Drawing.Drawing2D;

namespace CodexOledMonitor;

internal sealed class MainForm : Form
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CodexClient _codex = new();
    private readonly GameSenseClient _gameSense = new();
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon = AppIcon.Create();
    private MonitorSettings _settings = MonitorSettings.Load();
    private volatile bool _forceRefresh = true;
    private bool _exitRequested;

    private readonly PictureBox _preview = new();
    private readonly Label _status = new();
    private readonly Label _fiveValue = new();
    private readonly Label _weekValue = new();
    private readonly Label _lastUpdate = new();
    private readonly NumericUpDown _refresh = new();
    private readonly NumericUpDown _keepAlive = new();
    private readonly CheckBox _oledEnabled = new();
    private readonly CheckBox _autoStart = new();
    private bool _trayHintShown;

    public MainForm(bool startMinimized)
    {
        Text = "Codex OLED Monitor";
        Icon = _appIcon;
        ClientSize = new Size(640, 585);
        MinimumSize = new Size(656, 624);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 17, 22);
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);
        ShowInTaskbar = true;
        BuildInterface();

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => RestoreWindow());
        menu.Items.Add("立即刷新", null, (_, _) => _forceRefresh = true);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _tray = new NotifyIcon
        {
            Text = "Codex OLED Monitor",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => RestoreWindow();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) BeginInvoke(HideToTray);
        };
        Shown += (_, _) =>
        {
            _ = Task.Run(MonitorLoopAsync);
            if (startMinimized) HideToTray();
        };
    }

    private void BuildInterface()
    {
        Controls.Add(MakeLabel("Codex OLED Monitor", 28, 22, 380, 38, 22, FontStyle.Bold));
        Controls.Add(MakeLabel("SteelSeries Apex Pro · 实时额度显示", 30, 61, 430, 24, 9, FontStyle.Regular,
            Color.FromArgb(155, 163, 178)));

        _status.SetBounds(475, 28, 130, 30);
        _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Text = "正在启动";
        _status.BackColor = Color.FromArgb(41, 47, 58);
        Controls.Add(_status);

        var previewPanel = new Panel
        {
            Bounds = new Rectangle(28, 100, 584, 190),
            BackColor = Color.FromArgb(25, 28, 35)
        };
        previewPanel.Controls.Add(MakeLabel("OLED 实时预览", 18, 13, 200, 24, 10, FontStyle.Bold));
        _preview.SetBounds(36, 46, 512, 128);
        _preview.BackColor = Color.Black;
        _preview.SizeMode = PictureBoxSizeMode.StretchImage;
        _preview.Paint += (_, e) =>
        {
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        };
        previewPanel.Controls.Add(_preview);
        Controls.Add(previewPanel);

        var fiveCard = MakeCard(28, 308, "5 小时窗口", _fiveValue);
        var weekCard = MakeCard(326, 308, "7 天窗口", _weekValue);
        Controls.Add(fiveCard);
        Controls.Add(weekCard);

        var settingsPanel = new Panel
        {
            Bounds = new Rectangle(28, 408, 584, 112),
            BackColor = Color.FromArgb(25, 28, 35)
        };
        settingsPanel.Controls.Add(MakeLabel("设置", 18, 12, 100, 24, 10, FontStyle.Bold));

        _oledEnabled.Text = "持续显示在 OLED";
        _oledEnabled.Checked = _settings.OledEnabled;
        _oledEnabled.SetBounds(18, 43, 160, 25);
        _oledEnabled.ForeColor = Color.White;
        settingsPanel.Controls.Add(_oledEnabled);

        settingsPanel.Controls.Add(MakeLabel("关闭或最小化后仅驻留系统托盘", 18, 74, 240, 25, 8, FontStyle.Regular,
            Color.FromArgb(145, 153, 168)));

        settingsPanel.Controls.Add(MakeLabel("额度刷新", 270, 43, 70, 24, 9, FontStyle.Regular));
        ConfigureNumber(_refresh, _settings.RefreshSeconds, 15, 3600, 340, 41);
        settingsPanel.Controls.Add(_refresh);
        settingsPanel.Controls.Add(MakeLabel("秒", 408, 43, 25, 24, 9, FontStyle.Regular));

        settingsPanel.Controls.Add(MakeLabel("屏幕保活", 270, 75, 70, 24, 9, FontStyle.Regular));
        ConfigureNumber(_keepAlive, _settings.KeepAliveSeconds, 5, 14, 340, 73);
        settingsPanel.Controls.Add(_keepAlive);
        settingsPanel.Controls.Add(MakeLabel("秒", 408, 75, 25, 24, 9, FontStyle.Regular));

        _autoStart.Text = "登录 Windows 后启动";
        _autoStart.Checked = AutoStartManager.IsEnabled();
        _autoStart.SetBounds(440, 54, 140, 28);
        _autoStart.ForeColor = Color.White;
        settingsPanel.Controls.Add(_autoStart);
        Controls.Add(settingsPanel);

        var save = MakeButton("保存并应用", 28, 538, 130, Color.FromArgb(46, 110, 232));
        save.Click += (_, _) => SaveAndApply();
        Controls.Add(save);
        var refresh = MakeButton("立即刷新", 170, 538, 115, Color.FromArgb(45, 50, 61));
        refresh.Click += (_, _) => _forceRefresh = true;
        Controls.Add(refresh);
        var exit = MakeButton("退出监控", 497, 538, 115, Color.FromArgb(75, 40, 45));
        exit.Click += (_, _) => ExitApplication();
        Controls.Add(exit);

        _lastUpdate.SetBounds(300, 543, 180, 22);
        _lastUpdate.ForeColor = Color.FromArgb(145, 153, 168);
        _lastUpdate.TextAlign = ContentAlignment.MiddleRight;
        _lastUpdate.Text = "尚未刷新";
        Controls.Add(_lastUpdate);
    }

    private static Panel MakeCard(int x, int y, string title, Label value)
    {
        var panel = new Panel { Bounds = new Rectangle(x, y, 286, 82), BackColor = Color.FromArgb(25, 28, 35) };
        panel.Controls.Add(MakeLabel(title, 17, 12, 160, 22, 9, FontStyle.Regular, Color.FromArgb(155, 163, 178)));
        value.SetBounds(16, 34, 250, 34);
        value.Font = new Font("Microsoft YaHei UI", 17, FontStyle.Bold);
        value.ForeColor = Color.White;
        value.Text = "--% 剩余";
        panel.Controls.Add(value);
        return panel;
    }

    private static Label MakeLabel(string text, int x, int y, int width, int height, float size,
        FontStyle style, Color? color = null) => new()
    {
        Text = text,
        Bounds = new Rectangle(x, y, width, height),
        Font = new Font("Microsoft YaHei UI", size, style),
        ForeColor = color ?? Color.White,
        BackColor = Color.Transparent
    };

    private static Button MakeButton(string text, int x, int y, int width, Color backColor) => new()
    {
        Text = text,
        Bounds = new Rectangle(x, y, width, 34),
        BackColor = backColor,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand
    };

    private static void ConfigureNumber(NumericUpDown number, int value, int min, int max, int x, int y)
    {
        number.SetBounds(x, y, 62, 26);
        number.Minimum = min;
        number.Maximum = max;
        number.Value = Math.Clamp(value, min, max);
        number.BackColor = Color.FromArgb(36, 40, 49);
        number.ForeColor = Color.White;
        number.BorderStyle = BorderStyle.FixedSingle;
    }

    private void SaveAndApply()
    {
        _settings = new MonitorSettings
        {
            OledEnabled = _oledEnabled.Checked,
            RefreshSeconds = (int)_refresh.Value,
            KeepAliveSeconds = (int)_keepAlive.Value
        };
        _settings.Save();
        AutoStartManager.SetEnabled(_autoStart.Checked);
        _forceRefresh = true;
        SetStatus("设置已应用", Color.FromArgb(36, 117, 79));
    }

    private async Task MonitorLoopAsync()
    {
        var nextUsage = DateTime.MinValue;
        var nextFrame = DateTime.MinValue;
        OledFrame? frame = null;
        var stopped = false;

        while (!_shutdown.IsCancellationRequested)
        {
            var settings = _settings;
            try
            {
                if (!settings.OledEnabled)
                {
                    if (!stopped) await _gameSense.StopGameAsync(_shutdown.Token);
                    stopped = true;
                    SetStatusSafe("OLED 已暂停", Color.FromArgb(104, 80, 34));
                    await Task.Delay(1000, _shutdown.Token);
                    continue;
                }
                stopped = false;
                var now = DateTime.UtcNow;
                if (frame is null || now >= nextUsage || _forceRefresh)
                {
                    _forceRefresh = false;
                    var usage = await _codex.ReadUsageAsync(_shutdown.Token);
                    frame?.Preview.Dispose();
                    frame = OledRenderer.Render(usage);
                    nextUsage = now.AddSeconds(settings.RefreshSeconds);
                    UpdateUiSafe(frame);
                }
                if (frame is not null && now >= nextFrame)
                {
                    try
                    {
                        await _gameSense.SendFrameAsync(frame.Bytes, frame.FiveHourRemaining, _shutdown.Token);
                        nextFrame = now.AddSeconds(settings.KeepAliveSeconds);
                        SetStatusSafe("运行中 · OLED 在线", Color.FromArgb(30, 126, 82));
                    }
                    catch
                    {
                        _gameSense.ResetConnection();
                        nextFrame = now.AddSeconds(3);
                        SetStatusSafe("等待 SteelSeries GG", Color.FromArgb(120, 76, 32));
                    }
                }
                await Task.Delay(500, _shutdown.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception error)
            {
                SetStatusSafe("读取失败，正在重试", Color.FromArgb(126, 46, 52));
                SetLastUpdateSafe(error.Message);
                try { await Task.Delay(5000, _shutdown.Token); } catch { break; }
            }
        }
        frame?.Preview.Dispose();
    }

    private void UpdateUiSafe(OledFrame frame)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            var old = _preview.Image;
            _preview.Image = new Bitmap(frame.Preview);
            old?.Dispose();
            _fiveValue.Text = $"{frame.FiveHourRemaining}% 剩余";
            _weekValue.Text = $"{frame.WeeklyRemaining}% 剩余";
            _lastUpdate.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
        });
    }

    private void SetStatusSafe(string text, Color color)
    {
        if (!IsDisposed) BeginInvoke(() => SetStatus(text, color));
    }

    private void SetLastUpdateSafe(string text)
    {
        if (!IsDisposed) BeginInvoke(() => _lastUpdate.Text = text.Length > 25 ? text[..25] + "…" : text);
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.BackColor = color;
    }

    private void RestoreWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.BalloonTipTitle = "Codex OLED Monitor";
        _tray.BalloonTipText = "监控器已隐藏到系统托盘，双击图标可打开设置。";
        _tray.ShowBalloonTip(2500);
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _shutdown.Cancel();
        _gameSense.StopGameAsync(CancellationToken.None).GetAwaiter().GetResult();
        _codex.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _gameSense.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon.Dispose();
        base.OnFormClosing(e);
    }
}
