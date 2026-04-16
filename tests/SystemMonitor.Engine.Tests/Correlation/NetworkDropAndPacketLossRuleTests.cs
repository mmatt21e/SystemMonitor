using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class NetworkDropAndPacketLossRuleTests
{
    private static Reading Ping(double ms, DateTimeOffset ts) =>
        new("network", "gateway_latency_ms", ms, "ms", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["target"] = "gateway" });

    private static Reading LinkUp(double up, string adapter, DateTimeOffset ts) =>
        new("network", "link_up", up, "bool", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["adapter"] = adapter });

    [Fact]
    public void GatewayPingTimeouts_WithLinkUp_ClassifiedExternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 20; i++)
        {
            samples.Add(LinkUp(1, "eth0", now.AddSeconds(-20 + i)));
            samples.Add(Ping(-1, now.AddSeconds(-20 + i))); // all timeouts
        }

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["network"] = samples },
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var events = new NetworkDropAndPacketLossRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.External);
    }

    [Fact]
    public void LinkFlapping_ClassifiedInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 20; i++) samples.Add(LinkUp(i % 2, "eth0", now.AddSeconds(-20 + i)));

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["network"] = samples },
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var events = new NetworkDropAndPacketLossRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
    }
}
