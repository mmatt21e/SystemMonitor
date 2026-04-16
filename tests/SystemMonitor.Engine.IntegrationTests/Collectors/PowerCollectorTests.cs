using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

[Collection("Lhm")]
public class PowerCollectorTests
{
    [Fact]
    public void Collect_WithLhm_ReturnsNonThrowingReadings()
    {
        using var lhm = LhmComputer.Open();
        var c = new PowerCollector(TimeSpan.FromSeconds(1), lhm);
        var readings = c.Collect();
        foreach (var r in readings)
        {
            r.Source.Should().Be("power");
            r.Timestamp.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
        }
    }

    [Fact]
    public void Capability_IsUnavailable_WhenLhmNotProvided()
    {
        var c = new PowerCollector(TimeSpan.FromSeconds(1), lhm: null);
        c.Capability.Level.Should().Be(CapabilityLevel.Unavailable);
    }
}
