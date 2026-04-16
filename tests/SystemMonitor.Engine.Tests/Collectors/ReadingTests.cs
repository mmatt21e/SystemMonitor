using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Collectors;

public class ReadingTests
{
    [Fact]
    public void Reading_StoresAllFields()
    {
        var ts = DateTimeOffset.UtcNow;
        var r = new Reading(
            Source: "cpu",
            Metric: "usage_percent",
            Value: 42.5,
            Unit: "%",
            Timestamp: ts,
            Confidence: ReadingConfidence.High,
            Labels: new Dictionary<string, string> { ["core"] = "0" });

        r.Source.Should().Be("cpu");
        r.Metric.Should().Be("usage_percent");
        r.Value.Should().Be(42.5);
        r.Unit.Should().Be("%");
        r.Timestamp.Should().Be(ts);
        r.Confidence.Should().Be(ReadingConfidence.High);
        r.Labels["core"].Should().Be("0");
    }
}
