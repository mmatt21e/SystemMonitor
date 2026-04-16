using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class NetworkCollectorTests
{
    [Fact]
    public void Collect_ReturnsAdapterStatusReadings()
    {
        using var c = new NetworkCollector(TimeSpan.FromSeconds(1));
        var readings = c.Collect();
        readings.Should().Contain(r => r.Metric == "link_up");
        // Gateway latency attempt produces a reading even on offline systems (value == -1).
        readings.Should().Contain(r => r.Metric == "gateway_latency_ms");
    }
}
