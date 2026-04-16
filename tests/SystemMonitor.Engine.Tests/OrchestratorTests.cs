using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests;

public class OrchestratorTests
{
    private sealed class FakeCollector : CollectorBase
    {
        public int Calls;
        public FakeCollector(TimeSpan interval) : base("fake", interval) { }
        public override CapabilityStatus Capability => CapabilityStatus.Full();
        protected override IEnumerable<Reading> CollectCore()
        {
            Calls++;
            return new[] { new Reading("fake", "m", Calls, "x", DateTimeOffset.UtcNow,
                ReadingConfidence.High, new Dictionary<string, string>()) };
        }
    }

    [Fact]
    public async Task Start_CallsCollectorsOnInterval_AndStoresReadings()
    {
        var fake = new FakeCollector(TimeSpan.FromMilliseconds(50));
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["fake"] = new ReadingRingBuffer(100) };
        var sink = new List<Reading>();

        using var o = new Orchestrator(new[] { fake }, buffers, sink.Add);
        o.Start();
        await Task.Delay(250);
        o.Stop();

        fake.Calls.Should().BeGreaterThan(2);
        buffers["fake"].Count.Should().BeGreaterThan(2);
        sink.Count.Should().BeGreaterThan(2);
    }
}
