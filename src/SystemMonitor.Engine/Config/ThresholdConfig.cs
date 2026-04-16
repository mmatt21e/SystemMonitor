using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class ThresholdConfig
{
    [Description("CPU package temperature that triggers a warning (°C).")]
    public double CpuTempCelsiusWarn { get; set; } = 80;

    [Description("CPU package temperature that triggers a critical anomaly (°C).")]
    public double CpuTempCelsiusCritical { get; set; } = 95;

    [Description("Memory committed percent considered high.")]
    public double MemoryCommittedPercentWarn { get; set; } = 85;

    [Description("Disk latency above this is an anomaly (ms).")]
    public double DiskLatencyMsWarn { get; set; } = 50;

    [Description("Network packet-loss percent above this is an anomaly.")]
    public double NetworkPacketLossPercentWarn { get; set; } = 2.0;

    [Description("Voltage deviation from nominal above this percent is flagged.")]
    public double VoltageDeviationPercentWarn { get; set; } = 5.0;

    [Description("Baseline deviation in standard deviations that counts as an anomaly.")]
    public double BaselineStdDevWarn { get; set; } = 3.0;
}
