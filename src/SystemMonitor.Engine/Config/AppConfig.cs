using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class AppConfig
{
    [Description("Directory where log files are written.")]
    public string LogOutputDirectory { get; set; } = "";

    [Description("Log file rotation threshold (bytes).")]
    public long LogRotationSizeBytes { get; set; } = 100 * 1024 * 1024;

    [Description("Target UI refresh rate (Hz). The engine itself is unaffected.")]
    public int UiRefreshHz { get; set; } = 2;

    [Description("Per-collector in-memory ring buffer capacity (reading count).")]
    public int BufferCapacityPerCollector { get; set; } = 3600;

    [Description("Interval at which the correlation engine evaluates buffered readings (ms).")]
    public int CorrelationIntervalMs { get; set; } = 30_000;

    [Description("WMI query timeout in milliseconds. Protects against hung queries on broken systems.")]
    public int WmiTimeoutMs { get; set; } = 5_000;

    [Description("Per-collector configuration, keyed by collector name.")]
    public Dictionary<string, CollectorConfig> Collectors { get; set; } = new();

    [Description("Anomaly thresholds used by the correlation engine.")]
    public ThresholdConfig Thresholds { get; set; } = new();

    public static AppConfig Defaults()
    {
        var defaultLogDir = Path.Combine(AppContext.BaseDirectory, "Logs");

        return new AppConfig
        {
            LogOutputDirectory = defaultLogDir,
            Collectors = new Dictionary<string, CollectorConfig>
            {
                ["cpu"]         = new() { PollingIntervalMs = 1000 },
                ["memory"]      = new() { PollingIntervalMs = 1000 },
                ["storage"]     = new() { PollingIntervalMs = 5000 },
                ["network"]     = new() { PollingIntervalMs = 2000 },
                ["power"]       = new() { PollingIntervalMs = 1000 },
                ["gpu"]         = new() { PollingIntervalMs = 2000 },
                ["eventlog"]    = new() { PollingIntervalMs = 10_000 },
                ["reliability"] = new() { PollingIntervalMs = 300_000 },
                ["inventory"]   = new() { Enabled = true, PollingIntervalMs = 0 }  // one-shot
            }
        };
    }
}
