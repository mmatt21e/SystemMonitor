using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Persistent high disk latency (above threshold for >50% of recent samples on the same disk)
/// is classified as Internal — the storage subsystem is the failing component.
/// </summary>
public sealed class DiskLatencyAndSmartRule : ICorrelationRule
{
    public string Name => "DiskLatencyAndSmart";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("storage", out var storage)) yield break;

        var latencies = storage.Where(r => r.Metric == "avg_disk_sec_per_transfer_ms").ToList();
        if (latencies.Count < 10) yield break;

        var byDisk = latencies.GroupBy(r => r.Labels.GetValueOrDefault("disk", "unknown"));
        foreach (var group in byDisk)
        {
            var samples = group.ToList();
            double threshold = ctx.Thresholds.DiskLatencyMsWarn;
            double fractionOver = samples.Count(s => s.Value > threshold) / (double)samples.Count;
            if (fractionOver < 0.5) continue;

            yield return new AnomalyEvent(
                Timestamp: ctx.Now,
                Classification: Classification.Internal,
                Confidence: 0.75,
                Summary: $"Disk {group.Key} latency consistently above {threshold:F0}ms",
                Explanation: $"{fractionOver * 100:F0}% of recent samples on disk '{group.Key}' exceeded {threshold:F0}ms (peak {samples.Max(s => s.Value):F0}ms). Persistent latency on a specific disk points to a failing storage device — run SMART diagnostics (when admin) to confirm, and back up data.",
                SourceMetrics: new[] { "storage:avg_disk_sec_per_transfer_ms" });
        }
    }
}
