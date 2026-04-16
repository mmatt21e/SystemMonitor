using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

public class EndToEndSmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public EndToEndSmokeTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public async Task CpuReadings_AppearInLogFile()
    {
        using var cpu = new CpuCollector(TimeSpan.FromMilliseconds(200));
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["cpu"] = new ReadingRingBuffer(1000) };

        string[] lines;
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 10_000_000))
        {
            using var orch = new Orchestrator(new[] { (ICollector)cpu }, buffers, logger.WriteReading);
            orch.Start();
            await Task.Delay(700);
            orch.Stop();
            logger.Flush();
        }  // <-- logger disposes here, releasing the file handle

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        lines = File.ReadAllLines(file);
        lines.Should().NotBeEmpty();
        lines.Should().Contain(l => l.Contains("\"Metric\":\"usage_percent\""));
    }
}
