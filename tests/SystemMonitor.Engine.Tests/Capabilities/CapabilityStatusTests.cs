using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using Xunit;

namespace SystemMonitor.Engine.Tests.Capabilities;

public class CapabilityStatusTests
{
    [Fact]
    public void Full_HasFullLevelAndNoReason()
    {
        var c = CapabilityStatus.Full();
        c.Level.Should().Be(CapabilityLevel.Full);
        c.Reason.Should().BeNull();
    }

    [Fact]
    public void Partial_CarriesReason()
    {
        var c = CapabilityStatus.Partial("no admin for temps");
        c.Level.Should().Be(CapabilityLevel.Partial);
        c.Reason.Should().Be("no admin for temps");
    }

    [Fact]
    public void Unavailable_CarriesReason()
    {
        var c = CapabilityStatus.Unavailable("WMI class missing");
        c.Level.Should().Be(CapabilityLevel.Unavailable);
        c.Reason.Should().Be("WMI class missing");
    }
}
