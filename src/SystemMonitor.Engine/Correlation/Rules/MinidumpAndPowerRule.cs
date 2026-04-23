using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Correlates a kernel minidump with a Microsoft-Windows-Kernel-Power event 41
/// within a 60-second window. Temporal proximity flips the default Internal
/// (driver/OS fault) classification to External (likely upstream power event).
/// </summary>
public sealed class MinidumpAndPowerRule : ICorrelationRule
{
    private static readonly TimeSpan CorrelationWindow = TimeSpan.FromSeconds(60);

    public string Name => "MinidumpAndPower";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("reliability", out var reliability)) yield break;
        if (!ctx.BufferSnapshots.TryGetValue("eventlog", out var events)) yield break;

        var minidumps = reliability.Where(r => r.Metric == "minidump").ToList();
        if (minidumps.Count == 0) yield break;

        var kp41 = events
            .Where(r => r.Labels.TryGetValue("provider", out var p) && p.Contains("Kernel-Power")
                     && r.Labels.TryGetValue("event_id", out var id) && id == "41")
            .ToList();
        if (kp41.Count == 0) yield break;

        foreach (var dump in minidumps)
        {
            var match = kp41.FirstOrDefault(ev =>
                (ev.Timestamp - dump.Timestamp).Duration() <= CorrelationWindow);
            if (match is null) continue;

            var bugcheckCode = dump.Labels.TryGetValue("bugcheck_code", out var code) ? code : "unknown";
            var filename = dump.Labels.TryGetValue("filename", out var fn) ? fn : "unknown";

            yield return new AnomalyEvent(
                Timestamp: dump.Timestamp,
                Classification: Classification.External,
                Confidence: 0.9,
                Summary: $"Kernel-Power 41 within 60s of BugCheck {bugcheckCode} — likely external power event, not a driver fault",
                Explanation: $"A kernel minidump ({filename}) was written within {CorrelationWindow.TotalSeconds:F0}s of a Microsoft-Windows-Kernel-Power event ID 41 (the system rebooted without cleanly shutting down). The temporal proximity points to an external power event (brownout, mains instability, UPS failover, PSU undervolt) rather than a driver or kernel fault originating in this machine.",
                SourceMetrics: new[] { "reliability:minidump", "eventlog:event(41)" });
        }
    }
}
