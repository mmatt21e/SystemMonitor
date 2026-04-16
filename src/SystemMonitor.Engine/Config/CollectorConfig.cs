using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class CollectorConfig
{
    [Description("Whether this collector runs.")]
    public bool Enabled { get; set; } = true;

    [Description("Polling interval in milliseconds.")]
    public int PollingIntervalMs { get; set; } = 1000;
}
