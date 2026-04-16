using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

[Collection("Lhm")]
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
    public void Capability_IsPartialWithoutLhm()
    {
        using var c = new CpuCollector(TimeSpan.FromSeconds(1));
        c.Capability.Level.Should().Be(CapabilityLevel.Partial);
    }

    [Fact]
    public void Collect_WithLhm_MayIncludeTemperatureReadings()
    {
        using var lhm = SystemMonitor.Engine.Collectors.Lhm.LhmComputer.Open();
        using var c = new CpuCollector(TimeSpan.FromMilliseconds(200), lhm);
        c.Collect();
        Thread.Sleep(250);
        var readings = c.Collect();

        // We cannot assert presence (requires admin + supported hardware), but we assert
        // that WHEN temp readings exist they are well-formed.
        foreach (var r in readings.Where(r => r.Metric == "temperature_celsius"))
        {
            r.Unit.Should().Be("°C");
            r.Value.Should().BeGreaterThan(-50).And.BeLessThan(150);
        }
    }
}
