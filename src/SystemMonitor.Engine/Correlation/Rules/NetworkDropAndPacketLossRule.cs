using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Distinguishes between:
///   • link up, gateway unreachable → External (ISP/router/switch)
///   • link flapping (up/down transitions) → Internal (NIC, cable, port)
/// </summary>
public sealed class NetworkDropAndPacketLossRule : ICorrelationRule
{
    public string Name => "NetworkDropAndPacketLoss";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("network", out var net)) yield break;

        // Link-flap detection: count transitions per adapter.
        var byAdapter = net.Where(r => r.Metric == "link_up")
                           .GroupBy(r => r.Labels.GetValueOrDefault("adapter", ""));
        foreach (var group in byAdapter)
        {
            var values = group.OrderBy(r => r.Timestamp).Select(r => (int)r.Value).ToList();
            int transitions = 0;
            for (int i = 1; i < values.Count; i++)
                if (values[i] != values[i - 1]) transitions++;

            if (transitions >= 3)
            {
                yield return new AnomalyEvent(
                    Timestamp: ctx.Now,
                    Classification: Classification.Internal,
                    Confidence: 0.8,
                    Summary: $"Adapter '{group.Key}' link flapped {transitions} times",
                    Explanation: $"Network adapter '{group.Key}' changed state {transitions} times in the recent window. Repeated link up/down transitions typically indicate a NIC, cable, or physical port problem on this machine (or its immediate patch cable) rather than upstream infrastructure.",
                    SourceMetrics: new[] { "network:link_up" });
            }
        }

        // Gateway unreachable with link up → External.
        var pings = net.Where(r => r.Metric == "gateway_latency_ms").ToList();
        var linkUps = net.Where(r => r.Metric == "link_up").ToList();
        if (pings.Count >= 10 && linkUps.All(r => r.Value == 1))
        {
            double lossPct = pings.Count(p => p.Value < 0) / (double)pings.Count * 100;
            if (lossPct >= ctx.Thresholds.NetworkPacketLossPercentWarn * 10) // persistent, not occasional
            {
                yield return new AnomalyEvent(
                    Timestamp: ctx.Now,
                    Classification: Classification.External,
                    Confidence: 0.75,
                    Summary: $"Gateway unreachable ({lossPct:F0}% loss) while link is up",
                    Explanation: $"NIC reports link up but {lossPct:F0}% of gateway pings timed out. A working link with an unreachable default gateway points to upstream network infrastructure — switch, router, or cabling between this machine and the gateway — not the PC itself.",
                    SourceMetrics: new[] { "network:gateway_latency_ms", "network:link_up" });
            }
        }
    }
}
