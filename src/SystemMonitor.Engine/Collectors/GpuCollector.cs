using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class GpuCollector : CollectorBase
{
    private readonly LhmComputer? _lhm;

    public GpuCollector(TimeSpan pollingInterval, LhmComputer? lhm) : base("gpu", pollingInterval)
    {
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Unavailable("LibreHardwareMonitor not available")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        if (_lhm is null) yield break;
        var ts = DateTimeOffset.UtcNow;

        foreach (var s in _lhm.EnumerateSensors())
        {
            var isGpu = s.Hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
            if (!isGpu || !s.Value.HasValue) continue;

            var (metric, unit) = s.SensorType switch
            {
                SensorType.Temperature => ("temperature_celsius", "°C"),
                SensorType.Load        => ("load_percent", "%"),
                SensorType.Clock       => ("clock_mhz", "MHz"),
                SensorType.SmallData   => ("memory_mb", "MB"),
                SensorType.Power       => ("power_watts", "W"),
                SensorType.Fan         => ("fan_rpm", "RPM"),
                _ => (null, null)
            };
            if (metric is null) continue;

            yield return new Reading("gpu", metric, s.Value.Value, unit!, ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name
                });
        }
    }
}
