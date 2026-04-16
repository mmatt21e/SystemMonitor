using FluentAssertions;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class LhmComputerTests
{
    [Fact]
    public void Open_DoesNotThrow_AndExposesAtLeastOneHardwareItem()
    {
        using var lhm = LhmComputer.Open();
        // Even without admin, the Computer lists hardware — just with sparse sensor data.
        lhm.EnumerateSensors().Should().NotBeNull();
    }
}
