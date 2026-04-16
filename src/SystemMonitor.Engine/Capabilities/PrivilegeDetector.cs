using System.Runtime.Versioning;
using System.Security.Principal;

namespace SystemMonitor.Engine.Capabilities;

public static class PrivilegeDetector
{
    /// <summary>Returns true if the current process is running with Administrator privileges.</summary>
    [SupportedOSPlatform("windows")]
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
