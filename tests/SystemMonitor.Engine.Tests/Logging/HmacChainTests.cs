using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.Tests.Logging;

public class HmacChainTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public HmacChainTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static Reading R(double v) => new(
        "cpu", "usage_percent", v, "%", DateTimeOffset.UtcNow,
        ReadingConfidence.High, new Dictionary<string, string>());

    [Fact]
    public void EveryLine_ContainsHmacField()
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
        foreach (var line in lines)
        {
            line.Should().MatchRegex("\"hmac\":\"[0-9a-f]{64}\"");
            // Payload fields are still present — HMAC is an additive field only.
            line.Should().Contain("\"Value\"");
        }
    }

    [Fact]
    public void EachLine_SidecarKeyFileIsCreated()
    {
        string file;
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 1_000_000))
        {
            logger.WriteReading(R(1));
            logger.Flush();
            file = logger.CurrentPath;
        }

        File.Exists(file + ".key").Should().BeTrue();
        // Key should be 32 bytes (HMAC-SHA256 block).
        new FileInfo(file + ".key").Length.Should().Be(32);
    }

    [Fact]
    public void HmacFields_AreNonRepeatingWhenPayloadsDiffer()
    {
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 1_000_000))
        {
            logger.WriteReading(R(1));
            logger.WriteReading(R(2));
            logger.WriteReading(R(3));
            logger.Flush();
        }

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        var hmacs = File.ReadAllLines(file)
            .Select(l => Regex.Match(l, "\"hmac\":\"([0-9a-f]{64})\"").Groups[1].Value)
            .ToList();

        hmacs.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Rotation_CreatesSeparateKeyForEachFile()
    {
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 200))
        {
            for (int i = 0; i < 80; i++) logger.WriteReading(R(i));
            logger.Flush();
        }

        var jsonlFiles = Directory.GetFiles(_dir, "readings-*.jsonl");
        jsonlFiles.Length.Should().BeGreaterThan(1);
        foreach (var f in jsonlFiles)
        {
            File.Exists(f + ".key").Should().BeTrue($"key sidecar expected for {f}");
        }

        // Two keys should not coincidentally be identical.
        var keys = jsonlFiles.Select(f => File.ReadAllBytes(f + ".key")).ToList();
        for (int i = 0; i < keys.Count; i++)
            for (int j = i + 1; j < keys.Count; j++)
                keys[i].SequenceEqual(keys[j]).Should().BeFalse();
    }

    [Fact]
    public void HmacLine_IsValidJson()
    {
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 1_000_000))
        {
            logger.WriteReading(R(42));
            logger.Flush();
        }

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        var line = File.ReadAllLines(file).Single();

        var act = () => JsonDocument.Parse(line);
        act.Should().NotThrow();
    }
}
