using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class InventoryCollectorTests
{
    [Fact]
    public void Collect_ReturnsMachineInfo()
    {
        var c = new InventoryCollector(wmiTimeoutMs: 5000);
        var readings = c.Collect();
        readings.Should().Contain(r => r.Metric == "os_version");
        readings.Should().Contain(r => r.Metric == "machine_name");
    }
}
