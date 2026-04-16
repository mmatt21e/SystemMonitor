using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class NetworkTab : UserControl, ITabView
{
    private readonly Label _link = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly Label _latency = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 50 };
    private readonly Chart _chart = CpuTab.BuildChart("Gateway latency (last 10 min)", "latency");

    public NetworkTab()
    {
        _top.Controls.Add(new Label { Text = "Link:", AutoSize = true });
        _top.Controls.Add(_link);
        _top.Controls.Add(new Label { Text = "  Gateway:", AutoSize = true });
        _top.Controls.Add(_latency);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("network", out var buf)) return;
        var snap = buf.Snapshot();
        var links = snap.Where(r => r.Metric == "link_up").ToList();
        _link.Text = links.Count == 0
            ? "—"
            : links.Any(l => l.Value == 1) ? "Up" : "Down";
        var ping = snap.LastOrDefault(r => r.Metric == "gateway_latency_ms")?.Value;
        _latency.Text = ping is null || ping < 0 ? "unreachable" : $"{ping:F0} ms";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["latency"].Points.Clear();
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff && r.Metric == "gateway_latency_ms" && r.Value >= 0))
            _chart.Series["latency"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
    }
}
