using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Collectors;

public class CollectorBaseTests
{
    private sealed class FlakyCollector : CollectorBase
    {
        public int CollectCalls;
        public bool ShouldThrow = true;

        public FlakyCollector() : base("flaky", TimeSpan.FromMilliseconds(10)) { }
        public override CapabilityStatus Capability => CapabilityStatus.Full();

        protected override IEnumerable<Reading> CollectCore()
        {
            CollectCalls++;
            if (ShouldThrow) throw new InvalidOperationException("boom");
            return new[] { new Reading("flaky", "m", 1, "x", DateTimeOffset.UtcNow,
                ReadingConfidence.High, new Dictionary<string, string>()) };
        }
    }

    [Fact]
    public void ExceptionInCollect_IsSwallowed_AndReturnsEmpty()
    {
        var c = new FlakyCollector();
        var readings = c.Collect();
        readings.Should().BeEmpty();
        c.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void AfterThreeFailures_CollectorIsMarkedUnavailable()
    {
        var c = new FlakyCollector();
        c.Collect(); c.Collect(); c.Collect();
        c.ConsecutiveFailures.Should().Be(3);
        c.IsCooldownActive.Should().BeTrue();
    }

    [Fact]
    public void SuccessfulCollect_ResetsFailureCount()
    {
        var c = new FlakyCollector();
        c.Collect();
        c.ShouldThrow = false;
        c.Collect();
        c.ConsecutiveFailures.Should().Be(0);
    }
}
