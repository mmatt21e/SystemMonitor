using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class PowerAndKernelPowerRuleTests
{
    private static Reading Power(double volts, DateTimeOffset ts) =>
        new("power", "voltage_volts", volts, "V", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["sensor"] = "+12V" });

    private static Reading KernelPower(DateTimeOffset ts) =>
        new("eventlog", "event", 2, "level", ts, ReadingConfidence.High,
            new Dictionary<string, string>
            {
                ["channel"] = "System",
                ["event_id"] = "41",
                ["level"] = "Error",
                ["provider"] = "Microsoft-Windows-Kernel-Power"
            });

    [Fact]
    public void VoltageSag_FollowedByKernelPower41_ClassifiedExternal()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new Dictionary<string, IReadOnlyList<Reading>>
        {
            ["power"] = new[] { Power(12.0, now.AddSeconds(-10)), Power(11.0, now.AddSeconds(-6)) },
            ["eventlog"] = new[] { KernelPower(now.AddSeconds(-4)) }
        };
        var ctx = new CorrelationContext
        {
            BufferSnapshots = readings,
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var ev = new PowerAndKernelPowerRule().Evaluate(ctx).ToList();
        ev.Should().ContainSingle();
        ev[0].Classification.Should().Be(Classification.External);
    }

    [Fact]
    public void KernelPower41_WithoutVoltageSag_ClassifiedIndeterminate()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new Dictionary<string, IReadOnlyList<Reading>>
        {
            ["power"] = new[] { Power(12.0, now.AddSeconds(-10)), Power(12.0, now.AddSeconds(-6)) },
            ["eventlog"] = new[] { KernelPower(now.AddSeconds(-4)) }
        };
        var ctx = new CorrelationContext
        {
            BufferSnapshots = readings,
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var ev = new PowerAndKernelPowerRule().Evaluate(ctx).ToList();
        ev.Should().ContainSingle();
        ev[0].Classification.Should().Be(Classification.Indeterminate);
    }
}
