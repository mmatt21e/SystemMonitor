using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class StorageCollectorTests
{
    [Fact]
    public void Collect_ReturnsLatencyAndFreeSpacePerDrive()
    {
        using var c = new StorageCollector(TimeSpan.FromSeconds(1));
        c.Collect();
        var readings = c.Collect();

        readings.Should().Contain(r => r.Metric == "avg_disk_sec_per_transfer_ms");
        readings.Should().Contain(r => r.Metric == "free_space_percent");
        readings.Where(r => r.Metric == "free_space_percent")
                .Should().OnlyContain(r => r.Labels.ContainsKey("drive"));
    }
}
