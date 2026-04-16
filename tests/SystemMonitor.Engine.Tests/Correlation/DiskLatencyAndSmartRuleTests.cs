using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class DiskLatencyAndSmartRuleTests
{
    private static Reading Latency(double ms, string disk, DateTimeOffset ts) =>
        new("storage", "avg_disk_sec_per_transfer_ms", ms, "ms", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["disk"] = disk });

    [Fact]
    public void PersistentHighLatency_ClassifiedInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 30)
            .Select(i => Latency(150, "0 C:", now.AddSeconds(-30 + i)))
            .ToList<Reading>();

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["storage"] = samples },
            Thresholds = new ThresholdConfig { DiskLatencyMsWarn = 50 },
            Now = now
        };

        var events = new DiskLatencyAndSmartRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
    }

    [Fact]
    public void BriefLatencySpike_DoesNotFire()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 30; i++)
        {
            samples.Add(Latency(i == 15 ? 300 : 5, "0 C:", now.AddSeconds(-30 + i)));
        }

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["storage"] = samples },
            Thresholds = new ThresholdConfig { DiskLatencyMsWarn = 50 },
            Now = now
        };

        new DiskLatencyAndSmartRule().Evaluate(ctx).Should().BeEmpty();
    }
}
