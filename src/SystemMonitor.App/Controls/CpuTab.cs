using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class CpuTab : UserControl, ITabView
{
    private readonly Label _overallUsage = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly Label _temp = new()    { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly FlowLayoutPanel _topStrip = new() { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.LeftToRight };
    private readonly Chart _chart;

    public CpuTab()
    {
        _chart = BuildChart("CPU usage & temp (last 10 min)", "usage", "temp");
        _topStrip.Controls.Add(new Label { Text = "Usage:", AutoSize = true });
        _topStrip.Controls.Add(_overallUsage);
        _topStrip.Controls.Add(new Label { Text = "   Temp:", AutoSize = true });
        _topStrip.Controls.Add(_temp);
        Controls.Add(_chart);
        Controls.Add(_topStrip);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("cpu", out var buf)) return;
        var snap = buf.Snapshot();
        var latestUsage = snap.LastOrDefault(r => r.Metric == "usage_percent"
                                               && r.Labels.TryGetValue("scope", out var s) && s == "overall");
        var latestTemp = snap.LastOrDefault(r => r.Metric == "temperature_celsius");
        _overallUsage.Text = latestUsage is null ? "—" : $"{latestUsage.Value:F0}%";
        _temp.Text = latestTemp is null ? "—" : $"{latestTemp.Value:F0}°C";

        var usageSeries = _chart.Series["usage"];
        var tempSeries = _chart.Series["temp"];
        usageSeries.Points.Clear();
        tempSeries.Points.Clear();
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff))
        {
            if (r.Metric == "usage_percent" && r.Labels.GetValueOrDefault("scope") == "overall")
                usageSeries.Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
            else if (r.Metric == "temperature_celsius")
                tempSeries.Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
        }
    }

    internal static Chart BuildChart(string title, params string[] seriesNames)
    {
        var chart = new Chart { Dock = DockStyle.Fill, MinimumSize = new Size(50, 50) };
        var area = new ChartArea("main");
        area.AxisX.LabelStyle.Format = "HH:mm:ss";
        chart.ChartAreas.Add(area);
        chart.Titles.Add(title);
        foreach (var name in seriesNames)
            chart.Series.Add(new Series(name) { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.DateTime });
        return chart;
    }
}
