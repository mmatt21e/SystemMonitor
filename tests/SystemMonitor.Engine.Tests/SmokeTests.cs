using FluentAssertions;
using Xunit;

namespace SystemMonitor.Engine.Tests;

public class SmokeTests
{
    [Fact]
    public void TestHarness_Works()
    {
        (1 + 1).Should().Be(2);
    }
}
