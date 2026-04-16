using System.Diagnostics;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class CpuCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _overallUsage;
    private readonly List<PerformanceCounter> _perCoreUsage = new();
    private readonly LhmComputer? _lhm;

    public CpuCollector(TimeSpan pollingInterval, LhmComputer? lhm = null)
        : base("cpu", pollingInterval)
    {
        _overallUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        var cat = new PerformanceCounterCategory("Processor");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _perCoreUsage.Add(new PerformanceCounter("Processor", "% Processor Time", instance, readOnly: true));
        }
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Partial("no hardware sensors — usage only (no LHM)")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return new Reading("cpu", "usage_percent", _overallUsage.NextValue(), "%", ts,
            ReadingConfidence.High,
            new Dictionary<string, string> { ["scope"] = "overall" });

        foreach (var (counter, idx) in _perCoreUsage.Select((c, i) => (c, i)))
        {
            yield return new Reading("cpu", "usage_percent", counter.NextValue(), "%", ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["scope"] = "core",
                    ["core"] = idx.ToString()
                });
        }

        if (_lhm is null) yield break;

        foreach (var s in _lhm.EnumerateSensors())
        {
            if (s.Hardware.HardwareType != HardwareType.Cpu) continue;
            if (!s.Value.HasValue) continue;

            var metric = s.SensorType switch
            {
                SensorType.Temperature => "temperature_celsius",
                SensorType.Clock       => "clock_mhz",
                SensorType.Load        => null,   // PerformanceCounter already covers this
                _ => null
            };
            if (metric is null) continue;

            yield return new Reading("cpu", metric, s.Value.Value,
                s.SensorType == SensorType.Temperature ? "°C" : "MHz",
                ts, ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name
                });
        }
    }

    public void Dispose()
    {
        _overallUsage.Dispose();
        foreach (var c in _perCoreUsage) c.Dispose();
        // Do NOT dispose _lhm — it is owned by the orchestrator and shared across collectors.
    }
}
