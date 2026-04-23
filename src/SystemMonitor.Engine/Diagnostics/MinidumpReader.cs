namespace SystemMonitor.Engine.Diagnostics;

/// <summary>
/// Reads the DUMP_HEADER64 at the start of a Windows kernel small memory dump
/// (typically <c>C:\Windows\Minidump\*.dmp</c>) and extracts the BugCheck code
/// plus its four parameters.
/// </summary>
/// <remarks>
/// The layout used here is the documented/community-reverse-engineered
/// DUMP_HEADER64 structure:
/// <list type="bullet">
///   <item>0x00  Signature "PAGE"</item>
///   <item>0x04  ValidDump "DU64" (64-bit kernel dump marker)</item>
///   <item>0x38  BugCheckCode (DWORD)</item>
///   <item>0x40..0x58  BugCheckParameter1..4 (ULONG64)</item>
/// </list>
///
/// User-mode minidumps ("MDMP" signature) and 32-bit kernel dumps ("DUMP"
/// ValidDump) are explicitly rejected — they have a different layout and no
/// bugcheck fields at these offsets.
///
/// This is not a full <c>dbghelp.dll</c> integration — stack walking and
/// faulting-module resolution (Phase 1.6b) are a separate follow-up.
/// </remarks>
public static class MinidumpReader
{
    private const int MinHeaderBytes = 0x60;  // enough to reach BugCheckParameter4
    private const int BugCheckCodeOffset = 0x38;
    private const int BugCheckParam1Offset = 0x40;

    private static readonly byte[] KernelSignature = { (byte)'P', (byte)'A', (byte)'G', (byte)'E' };
    private static readonly byte[] KernelValidDump64 = { (byte)'D', (byte)'U', (byte)'6', (byte)'4' };

    public static MinidumpInfo? TryRead(string path)
    {
        if (!File.Exists(path)) return null;

        byte[] header;
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < MinHeaderBytes) return null;
            header = new byte[MinHeaderBytes];
            int read = fs.Read(header, 0, MinHeaderBytes);
            if (read < MinHeaderBytes) return null;
        }
        catch { return null; }

        if (!SpanEquals(header, 0, KernelSignature)) return null;
        if (!SpanEquals(header, 4, KernelValidDump64)) return null;

        var code = BitConverter.ToUInt32(header, BugCheckCodeOffset);
        var p1 = BitConverter.ToUInt64(header, BugCheckParam1Offset);
        var p2 = BitConverter.ToUInt64(header, BugCheckParam1Offset + 8);
        var p3 = BitConverter.ToUInt64(header, BugCheckParam1Offset + 16);
        var p4 = BitConverter.ToUInt64(header, BugCheckParam1Offset + 24);

        return new MinidumpInfo(
            IsKernelDump: true,
            BugCheckCode: code,
            BugCheckName: BugCheckCodes.Name(code),
            BugCheckParameter1: p1,
            BugCheckParameter2: p2,
            BugCheckParameter3: p3,
            BugCheckParameter4: p4);
    }

    private static bool SpanEquals(byte[] haystack, int offset, byte[] needle)
    {
        if (offset + needle.Length > haystack.Length) return false;
        for (int i = 0; i < needle.Length; i++)
            if (haystack[offset + i] != needle[i]) return false;
        return true;
    }
}
