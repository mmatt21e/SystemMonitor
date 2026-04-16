using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class MemoryCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _availableMb = new("Memory", "Available MBytes");
    private readonly PerformanceCounter _committedPercent = new("Memory", "% Committed Bytes In Use");
    private readonly PerformanceCounter _pageFaults = new("Memory", "Page Faults/sec");

    public MemoryCollector(TimeSpan pollingInterval) : base("memory", pollingInterval) { }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var empty = new Dictionary<string, string>();

        yield return new Reading("memory", "available_mb", _availableMb.NextValue(), "MB", ts, ReadingConfidence.High, empty);
        yield return new Reading("memory", "committed_percent", _committedPercent.NextValue(), "%", ts, ReadingConfidence.High, empty);
        yield return new Reading("memory", "page_faults_per_sec", _pageFaults.NextValue(), "count/s", ts, ReadingConfidence.High, empty);
    }

    public void Dispose()
    {
        _availableMb.Dispose();
        _committedPercent.Dispose();
        _pageFaults.Dispose();
    }
}
