using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class OverviewTab : UserControl, ITabView
{
    private readonly TableLayoutPanel _grid = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
    private readonly SubsystemCard _cpu = new("CPU", "cpu", "usage_percent", "%");
    private readonly SubsystemCard _mem = new("Memory", "memory", "committed_percent", "%");
    private readonly SubsystemCard _pow = new("Power", "power", "voltage_volts", "V");
    private readonly SubsystemCard _sto = new("Storage", "storage", "avg_disk_sec_per_transfer_ms", "ms");
    private readonly SubsystemCard _gpu = new("GPU", "gpu", "load_percent", "%");
    private readonly SubsystemCard _net = new("Network", "network", "gateway_latency_ms", "ms");

    public OverviewTab()
    {
        for (int i = 0; i < 3; i++) _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        for (int i = 0; i < 2; i++) _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _grid.Controls.Add(_cpu, 0, 0);
        _grid.Controls.Add(_mem, 1, 0);
        _grid.Controls.Add(_pow, 2, 0);
        _grid.Controls.Add(_sto, 0, 1);
        _grid.Controls.Add(_gpu, 1, 1);
        _grid.Controls.Add(_net, 2, 1);
        Controls.Add(_grid);
    }

    public void Refresh(EngineHost host)
    {
        _cpu.Refresh(host); _mem.Refresh(host); _pow.Refresh(host);
        _sto.Refresh(host); _gpu.Refresh(host); _net.Refresh(host);
    }
}

internal sealed class SubsystemCard : UserControl
{
    private readonly Label _title;
    private readonly Label _value;
    private readonly Chart _sparkline;
    private readonly string _bufferKey;
    private readonly string _metric;
    private readonly string _unit;

    public SubsystemCard(string title, string bufferKey, string metric, string unit)
    {
        _bufferKey = bufferKey; _metric = metric; _unit = unit;
        _title = new Label { Text = title, Font = new Font("Segoe UI", 11, FontStyle.Bold), Dock = DockStyle.Top, Height = 24 };
        _value = new Label { Text = "—", Font = new Font("Segoe UI", 20), Dock = DockStyle.Top, Height = 40 };
        _sparkline = new Chart { Dock = DockStyle.Fill, MinimumSize = new Size(50, 50) };
        var area = new ChartArea("main");
        area.AxisX.Enabled = AxisEnabled.False;
        area.AxisY.Enabled = AxisEnabled.False;
        _sparkline.ChartAreas.Add(area);
        _sparkline.Series.Add(new Series("data") { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.DateTime });
        Controls.Add(_sparkline);
        Controls.Add(_value);
        Controls.Add(_title);
        BorderStyle = BorderStyle.FixedSingle;
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue(_bufferKey, out var buf)) return;
        var snap = buf.Snapshot();
        var filtered = snap.Where(r => r.Metric == _metric).ToList();
        var latest = filtered.LastOrDefault();
        _value.Text = latest is null ? "—" : $"{latest.Value:F1} {_unit}";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _sparkline.Series["data"].Points.Clear();
        foreach (var r in filtered.Where(r => r.Timestamp >= cutoff))
            _sparkline.Series["data"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
    }
}
