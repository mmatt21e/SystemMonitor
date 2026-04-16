using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Correlates voltage rail sags with Kernel-Power 41 (unexpected shutdown) events.
/// Voltage sag + Kernel-Power 41 within 5 seconds → External (likely mains/UPS/PSU supply).
/// Kernel-Power 41 alone, with no voltage anomaly observed → Indeterminate.
/// </summary>
public sealed class PowerAndKernelPowerRule : ICorrelationRule
{
    private static readonly TimeSpan CorrelationWindow = TimeSpan.FromSeconds(5);

    public string Name => "PowerAndKernelPower";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("eventlog", out var events)) yield break;

        var kp41 = events
            .Where(r => r.Labels.TryGetValue("provider", out var p) && p.Contains("Kernel-Power")
                     && r.Labels.TryGetValue("event_id", out var id) && id == "41")
            .ToList();
        if (kp41.Count == 0) yield break;

        ctx.BufferSnapshots.TryGetValue("power", out var power);
        var voltages = power?.Where(r => r.Metric == "voltage_volts").ToList() ?? new();

        foreach (var ev in kp41)
        {
            var before = voltages.Where(v => v.Timestamp <= ev.Timestamp).ToList();

            double nominal = before.FirstOrDefault()?.Value ?? 0;
            double? minDuring = before.Count > 0 ? before.Min(v => v.Value) : null;
            double deviationPct = (nominal == 0 || minDuring is null)
                ? 0
                : Math.Abs(nominal - minDuring.Value) / nominal * 100;

            if (deviationPct >= ctx.Thresholds.VoltageDeviationPercentWarn)
            {
                yield return new AnomalyEvent(
                    Timestamp: ev.Timestamp,
                    Classification: Classification.External,
                    Confidence: 0.9,
                    Summary: $"Unexpected shutdown coincident with {deviationPct:F1}% voltage sag",
                    Explanation: $"Kernel-Power 41 (unexpected shutdown) preceded by voltage drop from {nominal:F2}V to {minDuring:F2}V within {CorrelationWindow.TotalSeconds}s. Points to upstream power delivery (mains instability, UPS switchover, or PSU supply-side input) rather than an OS/hardware fault.",
                    SourceMetrics: new[] { "eventlog:event(41)", "power:voltage_volts" });
            }
            else
            {
                yield return new AnomalyEvent(
                    Timestamp: ev.Timestamp,
                    Classification: Classification.Indeterminate,
                    Confidence: 0.4,
                    Summary: "Unexpected shutdown without voltage correlation",
                    Explanation: "Kernel-Power 41 (unexpected shutdown) observed but no concurrent voltage anomaly was recorded. Cause could be internal (PSU, motherboard, CPU fault) OR external without the voltage collector having visibility. Capture more runs, especially with a UPS or voltage-logging device upstream, to narrow this down.",
                    SourceMetrics: new[] { "eventlog:event(41)" });
            }
        }
    }
}
