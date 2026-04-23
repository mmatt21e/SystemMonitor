using FluentAssertions;
using SystemMonitor.Engine.Diagnostics;
using Xunit;

namespace SystemMonitor.Engine.Tests.Diagnostics;

public class MinidumpReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public MinidumpReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    /// <summary>
    /// Synthesizes a 0x200-byte buffer with a valid kernel DUMP_HEADER64 prefix
    /// carrying the given BugCheck code + parameters at the documented offsets.
    /// </summary>
    private static byte[] KernelDumpHeader(uint bugCheckCode,
        ulong p1 = 0, ulong p2 = 0, ulong p3 = 0, ulong p4 = 0)
    {
        var bytes = new byte[0x200];
        // Signature "PAGE" at offset 0
        bytes[0] = (byte)'P'; bytes[1] = (byte)'A'; bytes[2] = (byte)'G'; bytes[3] = (byte)'E';
        // ValidDump "DU64" at offset 4
        bytes[4] = (byte)'D'; bytes[5] = (byte)'U'; bytes[6] = (byte)'6'; bytes[7] = (byte)'4';
        // BugCheckCode DWORD at offset 0x38
        BitConverter.GetBytes(bugCheckCode).CopyTo(bytes, 0x38);
        // BugCheckParameter1..4 ULONG64 at 0x40, 0x48, 0x50, 0x58
        BitConverter.GetBytes(p1).CopyTo(bytes, 0x40);
        BitConverter.GetBytes(p2).CopyTo(bytes, 0x48);
        BitConverter.GetBytes(p3).CopyTo(bytes, 0x50);
        BitConverter.GetBytes(p4).CopyTo(bytes, 0x58);
        return bytes;
    }

    private string WriteDump(byte[] bytes)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".dmp");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void TryRead_KernelDumpWithBugCheck_ReturnsBugCheckFields()
    {
        var path = WriteDump(KernelDumpHeader(
            bugCheckCode: 0x139u,
            p1: 0x3, p2: 0xFFFFFFFF_BADCAFE5, p3: 0, p4: 0));

        var info = MinidumpReader.TryRead(path);

        info.Should().NotBeNull();
        info!.IsKernelDump.Should().BeTrue();
        info.BugCheckCode.Should().Be(0x139u);
        info.BugCheckName.Should().Be("KERNEL_SECURITY_CHECK_FAILURE");
        info.BugCheckParameter1.Should().Be(0x3u);
        info.BugCheckParameter2.Should().Be(0xFFFFFFFF_BADCAFE5);
    }

    [Fact]
    public void TryRead_UnknownBugCheck_StillReturnsInfoWithFallbackName()
    {
        var path = WriteDump(KernelDumpHeader(bugCheckCode: 0xABCDEF12u));

        var info = MinidumpReader.TryRead(path);

        info.Should().NotBeNull();
        info!.BugCheckCode.Should().Be(0xABCDEF12u);
        info.BugCheckName.Should().Be("UNKNOWN_BUGCHECK_0xABCDEF12");
    }

    [Fact]
    public void TryRead_UserModeMinidump_ReturnsNull()
    {
        // User-mode minidumps start with "MDMP" and have no BugCheck fields.
        var bytes = new byte[0x200];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'D'; bytes[2] = (byte)'M'; bytes[3] = (byte)'P';

        var path = WriteDump(bytes);

        MinidumpReader.TryRead(path).Should().BeNull();
    }

    [Fact]
    public void TryRead_FileTooShort_ReturnsNull()
    {
        var path = WriteDump(new byte[0x20]);  // way short of 0x60 needed for bugcheck params

        MinidumpReader.TryRead(path).Should().BeNull();
    }

    [Fact]
    public void TryRead_WrongSignature_ReturnsNull()
    {
        var bytes = new byte[0x200];
        // Random bytes — no recognizable signature
        new Random(42).NextBytes(bytes);

        var path = WriteDump(bytes);

        MinidumpReader.TryRead(path).Should().BeNull();
    }

    [Fact]
    public void TryRead_MissingFile_ReturnsNull()
    {
        MinidumpReader.TryRead(Path.Combine(_dir, "nope.dmp")).Should().BeNull();
    }

    [Fact]
    public void TryRead_PageSignatureButWrongValidDump_ReturnsNull()
    {
        var bytes = KernelDumpHeader(0x139u);
        // Overwrite ValidDump — "DUMP" instead of "DU64" — making it not a 64-bit kernel dump.
        bytes[4] = (byte)'D'; bytes[5] = (byte)'U'; bytes[6] = (byte)'M'; bytes[7] = (byte)'P';
        var path = WriteDump(bytes);

        MinidumpReader.TryRead(path).Should().BeNull();
    }
}
