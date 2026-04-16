using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.App.Controls;
using SystemMonitor.App.ViewModels;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
public partial class MainForm : Form
{
    private readonly AppConfig _config;
    private EngineHost? _host;
    private UiRefreshPump? _pump;
    private DateTime _startedUtc;
    private readonly System.Windows.Forms.Timer _uptimeTimer = new() { Interval = 1000 };

    public MainForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        _startButton.Click += (_, _) => StartEngine();
        _stopButton.Click += (_, _) => StopEngine();
        _configButton.Click += (_, _) => OpenConfigDialog();
        _openLogsButton.Click += (_, _) => OpenLogFolder();
        FormClosing += (_, _) => StopEngine();

        _uptimeTimer.Tick += (_, _) =>
        {
            if (_startedUtc == default) return;
            var d = DateTime.UtcNow - _startedUtc;
            _statusUptime.Text = $"Uptime: {d:hh\\:mm\\:ss}";
        };

        AddTabs();
    }

    private void AddTabs()
    {
        _tabs.TabPages.Add(new TabPage("Overview") { Controls = { new OverviewTab { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("CPU")      { Controls = { new CpuTab      { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Memory")   { Controls = { new MemoryTab   { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Power")    { Controls = { new PowerTab    { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Storage")  { Controls = { new StorageTab  { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("GPU")      { Controls = { new GpuTab      { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Network")  { Controls = { new NetworkTab  { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Events")   { Controls = { new EventsTab   { Dock = DockStyle.Fill } } });
    }

    private void StartEngine()
    {
        if (_host is not null) return;
        _host = EngineHost.Build(_config);
        _host.OnAnomaly += OnAnomalyReceived;
        _pump = new UiRefreshPump(_host, _config.UiRefreshHz, this);
        _pump.Tick += OnPumpTick;

        _host.Start();
        _pump.Start();
        _startedUtc = DateTime.UtcNow;
        _uptimeTimer.Start();

        _statusRunning.Text = "● Running";
        _statusRunning.ForeColor = Color.Green;
        _statusAdmin.Text = _host.IsAdministrator ? "Admin: Yes" : "Admin: No";
        _statusLogPath.Text = $"Logs: {_config.LogOutputDirectory}";
        _startButton.Enabled = false;
        _stopButton.Enabled = true;

        PopulateCapabilityTree(_host);
    }

    private void StopEngine()
    {
        if (_host is null) return;
        _pump?.Stop();
        _host.Stop();
        _pump?.Dispose();
        _host.Dispose();
        _host = null;
        _pump = null;
        _uptimeTimer.Stop();

        _statusRunning.Text = "● Stopped";
        _statusRunning.ForeColor = Color.Gray;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
    }

    private void OnPumpTick(EngineHost host)
    {
        // Each tab snapshots the buffers it cares about on the UI thread.
        foreach (TabPage tab in _tabs.TabPages)
            if (tab.Controls[0] is ITabView view) view.Refresh(host);
    }

    private void OnAnomalyReceived(AnomalyEvent ev)
    {
        // Marshal to UI thread.
        BeginInvoke(() =>
        {
            _eventFeed.Rows.Insert(0,
                ev.Timestamp.LocalDateTime.ToString("HH:mm:ss"),
                ev.Classification.ToString(),
                ev.Summary,
                ev.Confidence.ToString("F2"));
            if (_eventFeed.Rows.Count > 2000) _eventFeed.Rows.RemoveAt(_eventFeed.Rows.Count - 1);
            _tray.ShowBalloonTip(3000, $"{ev.Classification}: {ev.Summary}", ev.Explanation, ToolTipIcon.Warning);
        });
    }

    private void PopulateCapabilityTree(EngineHost host)
    {
        _capabilityTree.Nodes.Clear();
        var root = _capabilityTree.Nodes.Add("Collectors");
        foreach (var c in host.Collectors)
        {
            var node = root.Nodes.Add($"{c.Name} — {c.Capability.Level}");
            if (c.Capability.Reason is not null) node.Nodes.Add($"reason: {c.Capability.Reason}");
            node.Nodes.Add($"interval: {c.PollingInterval.TotalMilliseconds} ms");
        }
        root.Expand();
    }

    private void OpenConfigDialog()
    {
        using var dlg = new Forms.ConfigDialog(_config);
        dlg.ShowDialog(this);
    }

    private void OpenLogFolder()
    {
        if (!Directory.Exists(_config.LogOutputDirectory))
            Directory.CreateDirectory(_config.LogOutputDirectory);
        Process.Start("explorer", _config.LogOutputDirectory);
    }

    private void ForceRefresh()
    {
        if (_host is not null) OnPumpTick(_host);
    }

    private void OpenDocsFolder()
    {
        var exeDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "..", "docs"));
        if (!Directory.Exists(candidate))
            candidate = Path.Combine(exeDir, "docs");
        if (!Directory.Exists(candidate))
        {
            MessageBox.Show(
                $"Could not locate docs folder. Checked:\n{candidate}",
                "Documentation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start("explorer", candidate);
    }

    private void ShowAboutDialog()
    {
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString() ?? "unknown";
        var admin = _host?.IsAdministrator ?? false;
        MessageBox.Show(
            $"SystemMonitor\nVersion {version}\n\n" +
            "Windows diagnostic tool for identifying internal vs. external causes of\n" +
            "unexplained PC failures — hardware sensors, Windows event logs, and\n" +
            "correlation rules flag anomalies as Internal, External, or Indeterminate.\n\n" +
            $"Running as Administrator: {(admin ? "Yes" : "No")}\n" +
            $"Logs: {_config.LogOutputDirectory}\n\n" +
            "See docs/superpowers/specs/ for design, docs/smoke-test-checklist.md for manual validation.",
            "About SystemMonitor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
