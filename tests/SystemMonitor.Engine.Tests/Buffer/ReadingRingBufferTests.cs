using FluentAssertions;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Buffer;

public class ReadingRingBufferTests
{
    private static Reading R(int i) => new(
        "t", "m", i, "x", DateTimeOffset.FromUnixTimeSeconds(i),
        ReadingConfidence.High, new Dictionary<string, string>());

    [Fact]
    public void Add_BelowCapacity_PreservesOrder()
    {
        var buf = new ReadingRingBuffer(4);
        buf.Add(R(1)); buf.Add(R(2)); buf.Add(R(3));
        buf.Snapshot().Select(r => (int)r.Value).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Add_OverCapacity_DropsOldest()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Add(R(1)); buf.Add(R(2)); buf.Add(R(3)); buf.Add(R(4));
        buf.Snapshot().Select(r => (int)r.Value).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Snapshot_IsIndependentCopy()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Add(R(1));
        var snap = buf.Snapshot();
        buf.Add(R(2));
        snap.Should().HaveCount(1);
    }

    [Fact]
    public void Count_ReflectsCurrentSize()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Count.Should().Be(0);
        buf.Add(R(1)); buf.Add(R(2));
        buf.Count.Should().Be(2);
        buf.Add(R(3)); buf.Add(R(4));
        buf.Count.Should().Be(3);
    }
}
