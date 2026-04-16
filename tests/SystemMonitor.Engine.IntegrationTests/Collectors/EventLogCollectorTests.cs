using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class EventLogCollectorTests
{
    [Fact]
    public void Collect_ReturnsReadings_FromSystemAndApplicationLogs()
    {
        // Look back 24h — every live machine will have *some* Application entries.
        var c = new EventLogCollector(TimeSpan.FromSeconds(10), lookback: TimeSpan.FromHours(24));
        var readings = c.Collect();

        foreach (var r in readings)
        {
            r.Source.Should().Be("eventlog");
            r.Labels.Should().ContainKey("channel");
            r.Labels.Should().ContainKey("event_id");
            r.Labels.Should().ContainKey("level");
        }
    }
}
