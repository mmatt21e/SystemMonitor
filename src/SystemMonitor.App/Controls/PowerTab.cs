using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class PowerTab : UserControl, ITabView
{
    private readonly Label _onAc = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly Label _battery = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 50 };
    private readonly Chart _chart = CpuTab.BuildChart("Voltage rails (last 10 min)", "voltage");

    public PowerTab()
    {
        _top.Controls.Add(new Label { Text = "AC:", AutoSize = true });
        _top.Controls.Add(_onAc);
        _top.Controls.Add(new Label { Text = "  Battery:", AutoSize = true });
        _top.Controls.Add(_battery);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("power", out var buf)) return;
        var snap = buf.Snapshot();
        _onAc.Text = (snap.LastOrDefault(r => r.Metric == "on_ac")?.Value ?? 0) == 1 ? "Online" : "On battery";
        var bp = snap.LastOrDefault(r => r.Metric == "battery_percent")?.Value;
        _battery.Text = bp is null || bp > 100 ? "—" : $"{bp:F0}%";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["voltage"].Points.Clear();
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff && r.Metric == "voltage_volts"))
            _chart.Series["voltage"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
    }
}
