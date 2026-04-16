using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Classifies as Internal: CPU temperature exceeds the critical threshold while
/// average load is low. Cooling fault (pump, pads, dust), NOT an external thermal cause.
/// </summary>
public sealed class ThermalRunawayRule : ICorrelationRule
{
    public string Name => "ThermalRunaway";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("cpu", out var cpu) || cpu.Count == 0) yield break;

        var temps = cpu.Where(r => r.Metric == "temperature_celsius").ToList();
        var loads = cpu.Where(r => r.Metric == "usage_percent"
                                && r.Labels.TryGetValue("scope", out var s) && s == "overall").ToList();
        if (temps.Count == 0 || loads.Count == 0) yield break;

        var maxTemp = temps.Max(r => r.Value);
        var avgLoad = loads.Average(r => r.Value);

        if (maxTemp >= ctx.Thresholds.CpuTempCelsiusCritical && avgLoad < 50)
        {
            yield return new AnomalyEvent(
                Timestamp: ctx.Now,
                Classification: Classification.Internal,
                Confidence: 0.85,
                Summary: $"Critical CPU thermal at low load ({maxTemp:F0}°C, {avgLoad:F0}% load)",
                Explanation: $"CPU reached {maxTemp:F0}°C while average load over the window was {avgLoad:F0}%. High temperatures without corresponding load strongly suggest a cooling-system fault internal to the machine (failed pump, dried thermal paste, heatsink seating, fan failure, or blocked intake).",
                SourceMetrics: new[] { "cpu:temperature_celsius", "cpu:usage_percent" });
        }
    }
}
