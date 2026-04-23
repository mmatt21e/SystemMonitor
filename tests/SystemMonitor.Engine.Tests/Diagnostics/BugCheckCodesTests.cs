using FluentAssertions;
using SystemMonitor.Engine.Diagnostics;
using Xunit;

namespace SystemMonitor.Engine.Tests.Diagnostics;

public class BugCheckCodesTests
{
    [Theory]
    [InlineData(0x0000001Eu, "KMODE_EXCEPTION_NOT_HANDLED")]
    [InlineData(0x00000050u, "PAGE_FAULT_IN_NONPAGED_AREA")]
    [InlineData(0x0000007Eu, "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED")]
    [InlineData(0x0000009Fu, "DRIVER_POWER_STATE_FAILURE")]
    [InlineData(0x000000D1u, "DRIVER_IRQL_NOT_LESS_OR_EQUAL")]
    [InlineData(0x00000124u, "WHEA_UNCORRECTABLE_ERROR")]
    [InlineData(0x00000139u, "KERNEL_SECURITY_CHECK_FAILURE")]
    [InlineData(0x0000001Au, "MEMORY_MANAGEMENT")]
    public void Name_KnownCodes_MapsToSymbolicName(uint code, string expected)
    {
        BugCheckCodes.Name(code).Should().Be(expected);
    }

    [Fact]
    public void Name_UnknownCode_ReturnsHexFallback()
    {
        BugCheckCodes.Name(0xDEADBEEFu).Should().Be("UNKNOWN_BUGCHECK_0xDEADBEEF");
    }

    [Fact]
    public void Name_Zero_ReturnsHexFallback()
    {
        BugCheckCodes.Name(0u).Should().Be("UNKNOWN_BUGCHECK_0x00000000");
    }
}
