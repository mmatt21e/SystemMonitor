using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class StorageTab : UserControl, ITabView
{
    private readonly Label _worstLatency = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly Label _lowestFree = new() { Font = new Font("Segoe UI", 14), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 50 };
    private readonly Chart _chart = CpuTab.BuildChart("Disk latency (last 10 min)", "latency");

    public StorageTab()
    {
        _top.Controls.Add(new Label { Text = "Worst latency:", AutoSize = true });
        _top.Controls.Add(_worstLatency);
        _top.Controls.Add(new Label { Text = "  Lowest free:", AutoSize = true });
        _top.Controls.Add(_lowestFree);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("storage", out var buf)) return;
        var snap = buf.Snapshot();
        var latencies = snap.Where(r => r.Metric == "avg_disk_sec_per_transfer_ms").ToList();
        var free = snap.Where(r => r.Metric == "free_space_percent").ToList();
        _worstLatency.Text = latencies.Count == 0 ? "—" : $"{latencies.Max(r => r.Value):F0} ms";
        _lowestFree.Text = free.Count == 0 ? "—" : $"{free.Min(r => r.Value):F0}%";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["latency"].Points.Clear();
        foreach (var r in latencies.Where(r => r.Timestamp >= cutoff))
            _chart.Series["latency"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
    }
}
