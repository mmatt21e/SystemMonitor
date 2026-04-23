namespace SystemMonitor.Engine.Diagnostics;

/// <summary>
/// Information extracted from a Windows kernel minidump's DUMP_HEADER64.
/// Phase 1 exposes BugCheck fields only; stack walking and faulting-module
/// resolution are planned for Phase 1.6b (dbghelp + symbol server).
/// </summary>
public sealed record MinidumpInfo(
    bool IsKernelDump,
    uint BugCheckCode,
    string BugCheckName,
    ulong BugCheckParameter1,
    ulong BugCheckParameter2,
    ulong BugCheckParameter3,
    ulong BugCheckParameter4);
