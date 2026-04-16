using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class MemoryCollectorTests
{
    [Fact]
    public void Collect_ReturnsAvailableAndCommittedReadings()
    {
        using var c = new MemoryCollector(TimeSpan.FromSeconds(1));
        c.Collect();                         // prime PerformanceCounters
        var readings = c.Collect();

        readings.Should().Contain(r => r.Metric == "available_mb");
        readings.Should().Contain(r => r.Metric == "committed_percent");
        readings.Should().Contain(r => r.Metric == "page_faults_per_sec");
    }
}
