using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using Xunit;

namespace SystemMonitor.Engine.Tests.Capabilities;

public class PrivilegeDetectorTests
{
    [Fact]
    public void IsAdministrator_ReturnsBoolWithoutCrashing()
    {
        // We can't assert true/false deterministically (depends on how tests were launched),
        // but the call must complete and yield a boolean.
        var result = PrivilegeDetector.IsAdministrator();
        (result == true || result == false).Should().BeTrue();
    }
}
