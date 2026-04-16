using FluentAssertions;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class CorrelationEngineTests
{
    private sealed class AlwaysRule : ICorrelationRule
    {
        public string Name => "Always";
        public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx) =>
            new[] { new AnomalyEvent(ctx.Now, Classification.Indeterminate, 0.1, "s", "e", Array.Empty<string>()) };
    }

    [Fact]
    public void EvaluateOnce_CallsRules_AndEmitsAnomalies()
    {
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["cpu"] = new ReadingRingBuffer(10) };
        var emitted = new List<AnomalyEvent>();

        var engine = new CorrelationEngine(
            rules: new[] { new AlwaysRule() },
            buffers: buffers,
            thresholds: new ThresholdConfig(),
            sink: emitted.Add);

        engine.EvaluateOnce();
        emitted.Should().ContainSingle();
    }
}
