using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class MemoryTab : UserControl, ITabView
{
    private readonly Label _committed = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly Label _available = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 60 };
    private readonly Chart _chart = CpuTab.BuildChart("Memory (last 10 min)", "committed", "available");

    public MemoryTab()
    {
        _top.Controls.Add(new Label { Text = "Committed:", AutoSize = true });
        _top.Controls.Add(_committed);
        _top.Controls.Add(new Label { Text = "  Available:", AutoSize = true });
        _top.Controls.Add(_available);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("memory", out var buf)) return;
        var snap = buf.Snapshot();
        _committed.Text = snap.LastOrDefault(r => r.Metric == "committed_percent")?.Value.ToString("F0") + "%" ?? "—";
        _available.Text = snap.LastOrDefault(r => r.Metric == "available_mb")?.Value.ToString("F0") + " MB" ?? "—";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["committed"].Points.Clear();
        _chart.Series["available"].Points.Clear();
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff))
        {
            if (r.Metric == "committed_percent") _chart.Series["committed"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
            else if (r.Metric == "available_mb") _chart.Series["available"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
        }
    }
}
