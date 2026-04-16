using FluentAssertions;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void IntegrationHarness_RunsOnWindows()
    {
        OperatingSystem.IsWindows().Should().BeTrue();
    }
}
