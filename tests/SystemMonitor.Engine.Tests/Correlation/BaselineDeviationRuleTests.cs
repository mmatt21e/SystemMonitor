using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class BaselineDeviationRuleTests
{
    [Fact]
    public void ValueFarFromRollingMean_Flags()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        // 100 samples around 50 ± 1 then one at 80.
        var rng = new Random(0);
        for (int i = 0; i < 100; i++)
            samples.Add(new Reading("cpu", "usage_percent", 50 + rng.NextDouble() * 2 - 1, "%",
                now.AddSeconds(-100 + i), ReadingConfidence.High,
                new Dictionary<string, string> { ["scope"] = "overall" }));
        samples.Add(new Reading("cpu", "usage_percent", 80, "%", now, ReadingConfidence.High,
            new Dictionary<string, string> { ["scope"] = "overall" }));

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["cpu"] = samples },
            Thresholds = new ThresholdConfig { BaselineStdDevWarn = 3 },
            Now = now
        };

        var events = new BaselineDeviationRule(sourceName: "cpu", metric: "usage_percent").Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Indeterminate);
    }
}
