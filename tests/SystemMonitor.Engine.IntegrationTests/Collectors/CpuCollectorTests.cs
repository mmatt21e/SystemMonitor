using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class CpuCollectorTests
{
    [Fact]
    public void Collect_ReturnsAtLeastOverallUsageReading()
    {
        using var c = new CpuCollector(TimeSpan.FromMilliseconds(200));

        // First read from a brand-new PerformanceCounter is often 0; take two samples.
        c.Collect();
        Thread.Sleep(250);
        var readings = c.Collect();

        readings.Should().NotBeEmpty();
        readings.Should().Contain(r => r.Metric == "usage_percent" && r.Labels.ContainsKey("scope") && r.Labels["scope"] == "overall");
    }

    [Fact]
    public void Capability_IsFullOnWindows()
    {
        using var c = new CpuCollector(TimeSpan.FromSeconds(1));
        c.Capability.Level.Should().Be(CapabilityLevel.Full);
    }
}
