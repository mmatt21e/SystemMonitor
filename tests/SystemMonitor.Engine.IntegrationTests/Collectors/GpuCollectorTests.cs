using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

[Collection("Lhm")]
public class GpuCollectorTests
{
    [Fact]
    public void Collect_ReadingsAreWellFormed()
    {
        using var lhm = LhmComputer.Open();
        var c = new GpuCollector(TimeSpan.FromSeconds(1), lhm);
        var readings = c.Collect();
        foreach (var r in readings)
        {
            r.Source.Should().Be("gpu");
            r.Labels.Should().ContainKey("hardware");
        }
    }

    [Fact]
    public void Capability_IsUnavailable_WhenLhmNotProvided()
    {
        var c = new GpuCollector(TimeSpan.FromSeconds(1), lhm: null);
        c.Capability.Level.Should().Be(CapabilityLevel.Unavailable);
    }
}
