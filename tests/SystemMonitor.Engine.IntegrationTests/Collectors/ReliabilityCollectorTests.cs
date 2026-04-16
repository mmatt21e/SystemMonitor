using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class ReliabilityCollectorTests
{
    [Fact]
    public void Collect_DoesNotThrow()
    {
        var c = new ReliabilityCollector(TimeSpan.FromMinutes(5), wmiTimeoutMs: 5000);
        var readings = c.Collect();
        foreach (var r in readings)
        {
            r.Source.Should().Be("reliability");
        }
    }
}
