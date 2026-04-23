namespace SystemMonitor.Engine.Diagnostics;

/// <summary>
/// Maps Windows BugCheck codes (the number shown as STOP: 0xNN on a BSOD) to
/// symbolic names. Covers the common ~40 codes seen in practice on current Windows
/// versions — not every code ever defined. Unknown codes round-trip as UNKNOWN_BUGCHECK_0xXXXXXXXX.
/// </summary>
/// <remarks>
/// Source: Microsoft "Bug Check Code Reference" (docs.microsoft.com/windows-hardware/drivers/debugger/).
/// The list is intentionally small — Phase 1 favours correctness on the common codes over completeness.
/// </remarks>
public static class BugCheckCodes
{
    private static readonly Dictionary<uint, string> Known = new()
    {
        [0x0000000A] = "IRQL_NOT_LESS_OR_EQUAL",
        [0x0000001A] = "MEMORY_MANAGEMENT",
        [0x0000001E] = "KMODE_EXCEPTION_NOT_HANDLED",
        [0x00000024] = "NTFS_FILE_SYSTEM",
        [0x0000002E] = "DATA_BUS_ERROR",
        [0x0000003B] = "SYSTEM_SERVICE_EXCEPTION",
        [0x0000003D] = "INTERRUPT_EXCEPTION_NOT_HANDLED",
        [0x00000044] = "MULTIPLE_IRP_COMPLETE_REQUESTS",
        [0x0000004A] = "IRQL_GT_ZERO_AT_SYSTEM_SERVICE",
        [0x00000050] = "PAGE_FAULT_IN_NONPAGED_AREA",
        [0x00000051] = "REGISTRY_ERROR",
        [0x0000005C] = "HAL_INITIALIZATION_FAILED",
        [0x0000007A] = "KERNEL_DATA_INPAGE_ERROR",
        [0x0000007B] = "INACCESSIBLE_BOOT_DEVICE",
        [0x0000007E] = "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED",
        [0x0000007F] = "UNEXPECTED_KERNEL_MODE_TRAP",
        [0x00000080] = "NMI_HARDWARE_FAILURE",
        [0x000000BE] = "ATTEMPTED_WRITE_TO_READONLY_MEMORY",
        [0x000000C1] = "SPECIAL_POOL_DETECTED_MEMORY_CORRUPTION",
        [0x000000C2] = "BAD_POOL_CALLER",
        [0x000000C4] = "DRIVER_VERIFIER_DETECTED_VIOLATION",
        [0x000000C5] = "DRIVER_CORRUPTED_EXPOOL",
        [0x000000CE] = "DRIVER_UNLOADED_WITHOUT_CANCELLING_PENDING_OPERATIONS",
        [0x000000D1] = "DRIVER_IRQL_NOT_LESS_OR_EQUAL",
        [0x000000D5] = "DRIVER_PAGE_FAULT_IN_FREED_SPECIAL_POOL",
        [0x000000D6] = "DRIVER_PAGE_FAULT_BEYOND_END_OF_ALLOCATION",
        [0x000000DE] = "POOL_CORRUPTION_IN_FILE_AREA",
        [0x000000E2] = "MANUALLY_INITIATED_CRASH",
        [0x000000E3] = "RESOURCE_NOT_OWNED",
        [0x000000EA] = "THREAD_STUCK_IN_DEVICE_DRIVER",
        [0x000000ED] = "UNMOUNTABLE_BOOT_VOLUME",
        [0x000000F2] = "HARDWARE_INTERRUPT_STORM",
        [0x000000F3] = "DISORDERLY_SHUTDOWN",
        [0x000000F4] = "CRITICAL_OBJECT_TERMINATION",
        [0x000000F7] = "DRIVER_OVERRAN_STACK_BUFFER",
        [0x0000009F] = "DRIVER_POWER_STATE_FAILURE",
        [0x000000A0] = "INTERNAL_POWER_ERROR",
        [0x000000A5] = "ACPI_BIOS_ERROR",
        [0x000000BE] = "ATTEMPTED_WRITE_TO_READONLY_MEMORY",
        [0x00000101] = "CLOCK_WATCHDOG_TIMEOUT",
        [0x00000109] = "CRITICAL_STRUCTURE_CORRUPTION",
        [0x00000113] = "VIDEO_DXGKRNL_FATAL_ERROR",
        [0x00000116] = "VIDEO_TDR_FAILURE",
        [0x00000117] = "VIDEO_TDR_TIMEOUT_DETECTED",
        [0x00000124] = "WHEA_UNCORRECTABLE_ERROR",
        [0x00000133] = "DPC_WATCHDOG_VIOLATION",
        [0x00000139] = "KERNEL_SECURITY_CHECK_FAILURE",
        [0x0000013A] = "KERNEL_MODE_HEAP_CORRUPTION",
        [0x00000141] = "VIDEO_ENGINE_TIMEOUT_DETECTED",
        [0x00000144] = "BUGCODE_USB3_DRIVER",
        [0x0000015D] = "SOC_SUBSYSTEM_FAILURE",
        [0x00000154] = "UNEXPECTED_STORE_EXCEPTION",
        [0x00000161] = "LOCAL_SECURITY_AUTHORITY_SUBSYSTEM_SERVICE_MACHINE_CHECK",
        [0x000001A0] = "SYNTHETIC_WATCHDOG_TIMEOUT"
    };

    public static string Name(uint code) =>
        Known.TryGetValue(code, out var name) ? name : $"UNKNOWN_BUGCHECK_0x{code:X8}";
}
