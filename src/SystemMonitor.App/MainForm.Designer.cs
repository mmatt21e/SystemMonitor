namespace SystemMonitor.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private ToolStripButton _startButton = null!;
    private ToolStripButton _stopButton = null!;
    private ToolStripButton _configButton = null!;
    private ToolStripButton _openLogsButton = null!;
    private SplitContainer _split = null!;
    private TreeView _capabilityTree = null!;
    private TabControl _tabs = null!;
    private DataGridView _eventFeed = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _statusRunning = null!;
    private ToolStripStatusLabel _statusAdmin = null!;
    private ToolStripStatusLabel _statusLogPath = null!;
    private ToolStripStatusLabel _statusUptime = null!;
    private NotifyIcon _tray = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());
        var viewMenu = new ToolStripMenuItem("View");
        var toolsMenu = new ToolStripMenuItem("Tools");
        var helpMenu = new ToolStripMenuItem("Help");
        _menu.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });

        _toolbar = new ToolStrip();
        _startButton = new ToolStripButton("Start");
        _stopButton = new ToolStripButton("Stop") { Enabled = false };
        _configButton = new ToolStripButton("Config");
        _openLogsButton = new ToolStripButton("Open Logs");
        _toolbar.Items.AddRange(new ToolStripItem[] { _startButton, _stopButton,
            new ToolStripSeparator(), _configButton, _openLogsButton });

        _split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 240 };
        _capabilityTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        _split.Panel1.Controls.Add(_capabilityTree);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _split.Panel2.Controls.Add(_tabs);

        _eventFeed = new DataGridView
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            VirtualMode = false
        };
        _eventFeed.Columns.Add("Time", "Time");
        _eventFeed.Columns.Add("Class", "Classification");
        _eventFeed.Columns.Add("Summary", "Summary");
        _eventFeed.Columns.Add("Confidence", "Confidence");

        _status = new StatusStrip();
        _statusRunning = new ToolStripStatusLabel("● Stopped") { ForeColor = Color.Gray };
        _statusAdmin = new ToolStripStatusLabel("Admin: ?");
        _statusLogPath = new ToolStripStatusLabel("Logs: (none)") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusUptime = new ToolStripStatusLabel("Uptime: 00:00:00");
        _status.Items.AddRange(new ToolStripItem[] { _statusRunning, _statusAdmin, _statusLogPath, _statusUptime });

        _tray = new NotifyIcon(components)
        {
            Icon = SystemIcons.Information,
            Text = "SystemMonitor",
            Visible = true
        };
        _tray.BalloonTipTitle = "SystemMonitor";

        Text = "SystemMonitor";
        ClientSize = new Size(1200, 800);
        MainMenuStrip = _menu;
        Controls.Add(_split);
        Controls.Add(_eventFeed);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_status);
    }
}
