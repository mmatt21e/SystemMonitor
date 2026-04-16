using FluentAssertions;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.Tests.Config;

public class AppConfigTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var c = AppConfig.Defaults();
        c.LogOutputDirectory.Should().NotBeNullOrEmpty();
        c.LogRotationSizeBytes.Should().Be(100 * 1024 * 1024);
        c.UiRefreshHz.Should().Be(2);
        c.BufferCapacityPerCollector.Should().Be(3600);
        c.Collectors.Should().NotBeEmpty();
        c.Collectors.Should().ContainKey("cpu");
        c.Collectors["cpu"].Enabled.Should().BeTrue();
        c.Collectors["cpu"].PollingIntervalMs.Should().Be(1000);
        c.Thresholds.CpuTempCelsiusWarn.Should().Be(80);
        c.Thresholds.CpuTempCelsiusCritical.Should().Be(95);
    }
}
