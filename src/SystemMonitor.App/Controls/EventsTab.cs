using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class EventsTab : UserControl, ITabView
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    public EventsTab()
    {
        _grid.Columns.Add("Time", "Time");
        _grid.Columns.Add("Channel", "Channel");
        _grid.Columns.Add("Id", "Event ID");
        _grid.Columns.Add("Level", "Level");
        _grid.Columns.Add("Provider", "Provider");
        _grid.Columns.Add("Message", "Message");
        Controls.Add(_grid);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("eventlog", out var buf)) return;
        var snap = buf.Snapshot();
        _grid.Rows.Clear();
        foreach (var r in snap.TakeLast(200).Reverse())
        {
            _grid.Rows.Add(
                r.Timestamp.LocalDateTime.ToString("HH:mm:ss"),
                r.Labels.GetValueOrDefault("channel", ""),
                r.Labels.GetValueOrDefault("event_id", ""),
                r.Labels.GetValueOrDefault("level", ""),
                r.Labels.GetValueOrDefault("provider", ""),
                r.Labels.GetValueOrDefault("message", ""));
        }
    }
}
