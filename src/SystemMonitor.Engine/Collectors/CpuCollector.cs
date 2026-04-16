using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class CpuCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _overallUsage;
    private readonly List<PerformanceCounter> _perCoreUsage = new();

    public CpuCollector(TimeSpan pollingInterval)
        : base("cpu", pollingInterval)
    {
        // "_Total" = overall CPU usage across all cores.
        _overallUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);

        // Per-core counters are named by index ("0", "1", ...).
        var cat = new PerformanceCounterCategory("Processor");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _perCoreUsage.Add(new PerformanceCounter("Processor", "% Processor Time", instance, readOnly: true));
        }
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return new Reading(
            Source: "cpu",
            Metric: "usage_percent",
            Value: _overallUsage.NextValue(),
            Unit: "%",
            Timestamp: ts,
            Confidence: ReadingConfidence.High,
            Labels: new Dictionary<string, string> { ["scope"] = "overall" });

        foreach (var (counter, idx) in _perCoreUsage.Select((c, i) => (c, i)))
        {
            yield return new Reading(
                Source: "cpu",
                Metric: "usage_percent",
                Value: counter.NextValue(),
                Unit: "%",
                Timestamp: ts,
                Confidence: ReadingConfidence.High,
                Labels: new Dictionary<string, string>
                {
                    ["scope"] = "core",
                    ["core"] = idx.ToString()
                });
        }
    }

    public void Dispose()
    {
        _overallUsage.Dispose();
        foreach (var c in _perCoreUsage) c.Dispose();
    }
}
