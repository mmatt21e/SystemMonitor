using FluentAssertions;
using SystemMonitor.Engine.Correlation;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class AnomalyEventTests
{
    [Fact]
    public void Constructor_StoresAllFields()
    {
        var ev = new AnomalyEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Classification: Classification.External,
            Confidence: 0.8,
            Summary: "Voltage sag coincident with Kernel-Power 41",
            Explanation: "Rail dropped 8% then unexpected shutdown within 5s",
            SourceMetrics: new[] { "power:voltage_volts", "eventlog:event" });

        ev.Classification.Should().Be(Classification.External);
        ev.Confidence.Should().Be(0.8);
        ev.SourceMetrics.Should().HaveCount(2);
    }
}
