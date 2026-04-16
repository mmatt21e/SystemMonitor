using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class GpuTab : UserControl, ITabView
{
    private readonly Label _temp = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly Label _load = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 50 };
    private readonly Chart _chart = CpuTab.BuildChart("GPU (last 10 min)", "temp", "load");

    public GpuTab()
    {
        _top.Controls.Add(new Label { Text = "Temp:", AutoSize = true });
        _top.Controls.Add(_temp);
        _top.Controls.Add(new Label { Text = "  Load:", AutoSize = true });
        _top.Controls.Add(_load);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("gpu", out var buf)) return;
        var snap = buf.Snapshot();
        var latestTemp = snap.LastOrDefault(r => r.Metric == "temperature_celsius");
        var latestLoad = snap.LastOrDefault(r => r.Metric == "load_percent");
        _temp.Text = latestTemp is null ? "—" : $"{latestTemp.Value:F0}°C";
        _load.Text = latestLoad is null ? "—" : $"{latestLoad.Value:F0}%";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["temp"].Points.Clear();
        _chart.Series["load"].Points.Clear();
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff))
        {
            if (r.Metric == "temperature_celsius") _chart.Series["temp"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
            else if (r.Metric == "load_percent") _chart.Series["load"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
        }
    }
}
