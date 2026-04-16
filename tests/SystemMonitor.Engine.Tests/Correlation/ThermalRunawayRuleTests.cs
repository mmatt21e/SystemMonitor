using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class ThermalRunawayRuleTests
{
    private static Reading Cpu(string metric, double value, DateTimeOffset ts) =>
        new("cpu", metric, value, metric == "temperature_celsius" ? "°C" : "%", ts,
            ReadingConfidence.High,
            metric == "usage_percent"
                ? new Dictionary<string, string> { ["scope"] = "overall" }
                : new Dictionary<string, string>());

    private static CorrelationContext Ctx(IEnumerable<Reading> cpuReadings, ThresholdConfig? thr = null) => new()
    {
        BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["cpu"] = cpuReadings.ToList() },
        Thresholds = thr ?? new ThresholdConfig(),
        Now = DateTimeOffset.UtcNow
    };

    [Fact]
    public void HighTemp_WithSteadyLoad_ClassifiedAsInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new List<Reading>();
        // Steady 20% load, climbing temperature.
        for (int i = 0; i < 60; i++)
        {
            readings.Add(Cpu("usage_percent", 20, now.AddSeconds(-60 + i)));
            readings.Add(Cpu("temperature_celsius", 70 + i * 0.5, now.AddSeconds(-60 + i)));
        }

        var rule = new ThermalRunawayRule();
        var events = rule.Evaluate(Ctx(readings)).ToList();

        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
        events[0].Summary.Should().Contain("thermal");
    }

    [Fact]
    public void HighTemp_WithHighLoad_DoesNotFire()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new List<Reading>();
        for (int i = 0; i < 60; i++)
        {
            readings.Add(Cpu("usage_percent", 95, now.AddSeconds(-60 + i)));
            readings.Add(Cpu("temperature_celsius", 96, now.AddSeconds(-60 + i)));
        }

        new ThermalRunawayRule().Evaluate(Ctx(readings)).Should().BeEmpty();
    }
}
