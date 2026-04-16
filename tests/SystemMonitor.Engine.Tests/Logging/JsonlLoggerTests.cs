using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.Tests.Logging;

public class JsonlLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public JsonlLoggerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private Reading R(double v) => new(
        "cpu", "usage_percent", v, "%", DateTimeOffset.UtcNow,
        ReadingConfidence.High, new Dictionary<string, string>());

    [Fact]
    public void WriteReading_ProducesOneJsonLinePerReading()
    {
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 1_000_000))
        {
            logger.WriteReading(R(1));
            logger.WriteReading(R(2));
            logger.Flush();
        }

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        var lines = File.ReadAllLines(file);
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("\"Value\":1");
        lines[1].Should().Contain("\"Value\":2");
    }

    [Fact]
    public void ExceedingRotationSize_OpensNewFile()
    {
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 200))
        {
            for (int i = 0; i < 50; i++) logger.WriteReading(R(i));
            logger.Flush();
        }

        Directory.GetFiles(_dir, "readings-*.jsonl").Length.Should().BeGreaterThan(1);
    }
}
