#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Drives Sysinternals NotMyFault to produce kernel minidumps on demand.

.DESCRIPTION
  DESIGNED FOR DISPOSABLE TEST VMs ONLY. NotMyFault deliberately crashes
  Windows. Running this on your workstation will hard-reboot it and lose any
  unsaved work. The script refuses to run unless -IAmInAVm is supplied.

  Because the crash kills this PowerShell process, each invocation triggers
  exactly one crash. After the reboot, re-run with a different -CrashType to
  build up a varied corpus. Dumps land in C:\Windows\Minidump\.

  Recommended workflow inside a VM:
    1. Snapshot the VM in a known-clean state.
    2. Run `-List` to see the crash types NotMyFault exposes.
    3. Run `-CrashType N -IAmInAVm` to trigger one crash + reboot.
    4. After reboot, log in, copy C:\Windows\Minidump\*.dmp somewhere safe.
    5. Repeat steps 3-4 for each desired crash type.
    6. Revert to the snapshot before your next engineering session.

.PARAMETER CrashType
  Integer passed to NotMyFault's /crash N switch. Typical range is 1..10;
  use -List to discover what the binary on your system supports.

.PARAMETER NotMyFaultPath
  Path to notmyfault64.exe. Defaults to C:\Tools\NotMyFault\notmyfault64.exe.

.PARAMETER List
  Print NotMyFault's own /? help (which lists its crash types) and exit.

.PARAMETER IAmInAVm
  Required safety gate. Confirms you understand this will BSOD the machine.

.EXAMPLE
  .\generate-dumps.ps1 -List

.EXAMPLE
  .\generate-dumps.ps1 -CrashType 1 -IAmInAVm
  # Triggers a kernel-mode high-IRQL fault. Machine reboots immediately.
#>
[CmdletBinding()]
param(
    [int] $CrashType,
    [string] $NotMyFaultPath = 'C:\Tools\NotMyFault\notmyfault64.exe',
    [switch] $List,
    [switch] $IAmInAVm
)

$ErrorActionPreference = 'Stop'

function Assert-NotMyFault {
    if (-not (Test-Path -LiteralPath $NotMyFaultPath)) {
        Write-Error @"
NotMyFault not found at: $NotMyFaultPath

Download from: https://learn.microsoft.com/sysinternals/downloads/notmyfault
Extract notmyfault64.exe (and accept the EULA) to:
  $NotMyFaultPath
Then re-run.
"@
    }
}

function Assert-MinidumpConfig {
    $key = 'HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl'
    $enabled = (Get-ItemProperty -Path $key -Name 'CrashDumpEnabled' -ErrorAction SilentlyContinue).CrashDumpEnabled
    if ($null -eq $enabled -or $enabled -ne 3) {
        Write-Warning "CrashDumpEnabled at $key is '$enabled' — expected 3 (small memory dump). Dumps may not land in C:\Windows\Minidump\."
    }
    $dir = 'C:\Windows\Minidump'
    if (-not (Test-Path -LiteralPath $dir)) {
        Write-Warning "$dir does not exist yet. Windows will create it on first crash."
    }
}

if ($List) {
    Assert-NotMyFault
    & $NotMyFaultPath '/?' | Out-Host
    exit 0
}

if (-not $IAmInAVm) {
    Write-Error @"
Refusing to run without -IAmInAVm.

This script invokes NotMyFault, which BSODs the machine it runs on. That is
almost never what you want on a workstation. If you are certain you are
inside a disposable test VM with a recent snapshot, re-run with -IAmInAVm.
"@
}

if ($CrashType -le 0) {
    Write-Error "Specify -CrashType <N>. Use -List to see the crash types NotMyFault supports."
}

Assert-NotMyFault
Assert-MinidumpConfig

Write-Host "Triggering NotMyFault /crash $CrashType in 3 seconds. Expect an immediate BSOD." -ForegroundColor Yellow
Start-Sleep -Seconds 3

& $NotMyFaultPath "/crash" "$CrashType" "/accepteula"

Write-Host "If you are reading this, the crash did not fire. Check NotMyFault output above." -ForegroundColor Red
exit 1
