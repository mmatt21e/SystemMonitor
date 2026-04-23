using System.Text.Json;
using FluentAssertions;
using SystemMonitor.Engine.Diagnostics;
using Xunit;

namespace SystemMonitor.Engine.Tests.Diagnostics;

public class MinidumpAnalyzeCommandTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public MinidumpAnalyzeCommandTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static byte[] KernelDumpHeader(uint bugCheckCode,
        ulong p1 = 0, ulong p2 = 0, ulong p3 = 0, ulong p4 = 0)
    {
        var bytes = new byte[0x200];
        bytes[0] = (byte)'P'; bytes[1] = (byte)'A'; bytes[2] = (byte)'G'; bytes[3] = (byte)'E';
        bytes[4] = (byte)'D'; bytes[5] = (byte)'U'; bytes[6] = (byte)'6'; bytes[7] = (byte)'4';
        BitConverter.GetBytes(bugCheckCode).CopyTo(bytes, 0x38);
        BitConverter.GetBytes(p1).CopyTo(bytes, 0x40);
        BitConverter.GetBytes(p2).CopyTo(bytes, 0x48);
        BitConverter.GetBytes(p3).CopyTo(bytes, 0x50);
        BitConverter.GetBytes(p4).CopyTo(bytes, 0x58);
        return bytes;
    }

    private string WriteDump(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static IReadOnlyList<JsonElement> ParseLines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
              .Select(l => JsonDocument.Parse(l).RootElement)
              .ToList();

    [Fact]
    public void Run_SingleKernelDump_WritesOneJsonLineWithBugCheckFields()
    {
        var path = WriteDump("kernel.dmp", KernelDumpHeader(
            bugCheckCode: 0x139u, p1: 3, p2: 0xFFFFFFFF_BADCAFE5));
        var writer = new StringWriter();

        var exit = MinidumpAnalyzeCommand.Run(path, writer);

        exit.Should().Be(0);
        var lines = ParseLines(writer.ToString());
        lines.Should().HaveCount(1);
        var obj = lines[0];
        obj.GetProperty("parsed").GetBoolean().Should().BeTrue();
        obj.GetProperty("bugcheck_code").GetString().Should().Be("0x00000139");
        obj.GetProperty("bugcheck_name").GetString().Should().Be("KERNEL_SECURITY_CHECK_FAILURE");
        obj.GetProperty("filename").GetString().Should().Be("kernel.dmp");
        obj.GetProperty("path").GetString().Should().Be(path);
        var parameters = obj.GetProperty("bugcheck_parameters");
        parameters.GetArrayLength().Should().Be(4);
        parameters[0].GetString().Should().Be("0x0000000000000003");
        parameters[1].GetString().Should().Be("0xffffffffbadcafe5");
    }

    [Fact]
    public void Run_UnparseableFile_EmitsParsedFalseWithReason()
    {
        var path = WriteDump("garbage.dmp", new byte[0x200]);  // zero-filled: no signature
        var writer = new StringWriter();

        var exit = MinidumpAnalyzeCommand.Run(path, writer);

        exit.Should().Be(0);  // file existed, so exit is success even though parse failed
        var obj = ParseLines(writer.ToString()).Single();
        obj.GetProperty("parsed").GetBoolean().Should().BeFalse();
        obj.TryGetProperty("reason", out var reason).Should().BeTrue();
        reason.GetString().Should().NotBeNullOrEmpty();
        obj.TryGetProperty("bugcheck_code", out _).Should().BeFalse();
    }

    [Fact]
    public void Run_UserModeMinidump_EmitsParsedFalse()
    {
        var bytes = new byte[0x200];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'D'; bytes[2] = (byte)'M'; bytes[3] = (byte)'P';
        var path = WriteDump("usermode.dmp", bytes);
        var writer = new StringWriter();

        MinidumpAnalyzeCommand.Run(path, writer).Should().Be(0);
        ParseLines(writer.ToString()).Single()
            .GetProperty("parsed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Run_DirectoryWithMultipleDumps_EmitsOneJsonLinePerFile()
    {
        WriteDump("a.dmp", KernelDumpHeader(0x50u));
        WriteDump("b.dmp", KernelDumpHeader(0x7Eu));
        WriteDump("c.dmp", KernelDumpHeader(0x139u));
        var writer = new StringWriter();

        var exit = MinidumpAnalyzeCommand.Run(_dir, writer);

        exit.Should().Be(0);
        var lines = ParseLines(writer.ToString());
        lines.Should().HaveCount(3);
        lines.Select(l => l.GetProperty("bugcheck_code").GetString())
             .Should().BeEquivalentTo(new[] { "0x00000050", "0x0000007E", "0x00000139" });
    }

    [Fact]
    public void Run_DirectoryIgnoresNonDumpFiles()
    {
        WriteDump("real.dmp", KernelDumpHeader(0x139u));
        File.WriteAllText(Path.Combine(_dir, "not-a-dump.txt"), "hello");
        var writer = new StringWriter();

        MinidumpAnalyzeCommand.Run(_dir, writer).Should().Be(0);
        ParseLines(writer.ToString()).Should().HaveCount(1);
    }

    [Fact]
    public void Run_MissingPath_Returns2AndWritesNothing()
    {
        var writer = new StringWriter();

        var exit = MinidumpAnalyzeCommand.Run(Path.Combine(_dir, "nope.dmp"), writer);

        exit.Should().Be(2);
        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Run_EmptyDirectory_Returns2()
    {
        var writer = new StringWriter();

        MinidumpAnalyzeCommand.Run(_dir, writer).Should().Be(2);
    }

    [Fact]
    public void Run_EmitsFileMetadata()
    {
        var path = WriteDump("meta.dmp", KernelDumpHeader(0x50u));
        var writer = new StringWriter();

        MinidumpAnalyzeCommand.Run(path, writer);

        var obj = ParseLines(writer.ToString()).Single();
        obj.GetProperty("size_bytes").GetInt64().Should().Be(new FileInfo(path).Length);
        obj.TryGetProperty("created_utc", out var created).Should().BeTrue();
        created.GetString().Should().NotBeNullOrEmpty();
    }
}
