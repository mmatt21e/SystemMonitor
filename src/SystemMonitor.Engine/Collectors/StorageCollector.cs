using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class StorageCollector : CollectorBase, IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _avgSecPerXfer = new();
    private readonly Dictionary<string, PerformanceCounter> _queueDepth = new();

    public StorageCollector(TimeSpan pollingInterval) : base("storage", pollingInterval)
    {
        var cat = new PerformanceCounterCategory("PhysicalDisk");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _avgSecPerXfer[instance] = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Transfer", instance, readOnly: true);
            _queueDepth[instance]    = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", instance, readOnly: true);
        }
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        foreach (var (instance, counter) in _avgSecPerXfer)
        {
            var secs = counter.NextValue();
            yield return new Reading("storage", "avg_disk_sec_per_transfer_ms", secs * 1000.0, "ms", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["disk"] = instance });
        }

        foreach (var (instance, counter) in _queueDepth)
        {
            yield return new Reading("storage", "queue_depth", counter.NextValue(), "count", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["disk"] = instance });
        }

        // Free space — one reading per logical drive.
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
            var percent = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize;
            yield return new Reading("storage", "free_space_percent", percent, "%", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["drive"] = drive.Name });
        }
    }

    public void Dispose()
    {
        foreach (var c in _avgSecPerXfer.Values) c.Dispose();
        foreach (var c in _queueDepth.Values) c.Dispose();
    }
}
