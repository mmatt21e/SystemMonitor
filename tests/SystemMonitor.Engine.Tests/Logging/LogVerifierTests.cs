using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.Tests.Logging;

public class LogVerifierTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public LogVerifierTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static Reading R(double v) => new(
        "cpu", "usage_percent", v, "%", DateTimeOffset.UtcNow,
        ReadingConfidence.High, new Dictionary<string, string>());

    private string WriteTen()
    {
        string path;
        using (var logger = new JsonlLogger(_dir, "readings", rotationBytes: 10_000_000))
        {
            for (int i = 0; i < 10; i++) logger.WriteReading(R(i));
            logger.Flush();
            path = logger.CurrentPath;
        }
        return path;
    }

    [Fact]
    public void UnmodifiedFile_VerifiesSuccessfully()
    {
        var path = WriteTen();

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeTrue();
        result.LinesChecked.Should().Be(10);
        result.FirstFailureLine.Should().BeNull();
    }

    [Fact]
    public void MissingKeyFile_ReturnsError()
    {
        var path = WriteTen();
        File.Delete(path + ".key");

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("key");
    }

    [Fact]
    public void MissingLogFile_ReturnsError()
    {
        var result = LogVerifier.Verify(Path.Combine(_dir, "does-not-exist.jsonl"));

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ByteFlipInAnyLine_IsDetected()
    {
        var path = WriteTen();
        var lines = File.ReadAllLines(path);
        // Flip a character inside line 4 (index 3), in the Value number.
        lines[3] = lines[3].Replace("\"Value\":3", "\"Value\":9");
        File.WriteAllLines(path, lines);

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeFalse();
        result.FirstFailureLine.Should().Be(4);  // 1-based
    }

    [Fact]
    public void DroppedLine_IsDetected()
    {
        var path = WriteTen();
        var lines = File.ReadAllLines(path).ToList();
        lines.RemoveAt(5);
        File.WriteAllLines(path, lines);

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeFalse();
        result.FirstFailureLine.Should().Be(6);  // now occupies what was line 7
    }

    [Fact]
    public void ReorderedLines_IsDetected()
    {
        var path = WriteTen();
        var lines = File.ReadAllLines(path);
        (lines[2], lines[3]) = (lines[3], lines[2]);
        File.WriteAllLines(path, lines);

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeFalse();
        result.FirstFailureLine.Should().Be(3);
    }

    [Fact]
    public void AppendedLineWithoutValidChain_IsDetected()
    {
        var path = WriteTen();
        // Append a plausibly-formatted line whose HMAC doesn't chain correctly.
        File.AppendAllText(path, "{\"Source\":\"cpu\",\"Value\":99,\"hmac\":\"00000000000000000000000000000000000000000000000000000000deadbeef\"}" + Environment.NewLine);

        var result = LogVerifier.Verify(path);

        result.Ok.Should().BeFalse();
        result.FirstFailureLine.Should().Be(11);
    }
}
