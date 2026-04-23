<#
.SYNOPSIS
  Calls SystemMonitor.exe --analyze-dump against a directory of .dmp files
  and prints a summary table, plus optionally writes manifest.jsonl.

.DESCRIPTION
  Read-only. Safe to run on any machine with dumps — no NotMyFault, no
  crashing. Expects the SystemMonitor build output to exist; override
  -MonitorExe if you want to point at a release build or an installed copy.

.PARAMETER Path
  Directory of .dmp files to inventory. Defaults to the repo's test corpus.

.PARAMETER MonitorExe
  Path to SystemMonitor.exe. Defaults to the Debug build output.

.PARAMETER WriteManifest
  If set, writes one JSON line per dump to <Path>\manifest.jsonl.

.EXAMPLE
  .\inventory.ps1 -Path C:\Windows\Minidump

.EXAMPLE
  .\inventory.ps1 -Path ..\..\tests\fixtures\minidumps -WriteManifest
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string] $Path,

    [string] $MonitorExe = "$PSScriptRoot\..\..\src\SystemMonitor.App\bin\Debug\net8.0-windows\SystemMonitor.App.exe",

    [switch] $WriteManifest
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Path not found: $Path"
}

if (-not (Test-Path -LiteralPath $MonitorExe)) {
    Write-Error @"
SystemMonitor.exe not found at:
  $MonitorExe
Build first (dotnet build SystemMonitor.sln) or pass -MonitorExe <path>.
"@
}

$raw = & $MonitorExe --analyze-dump $Path
if ($LASTEXITCODE -ne 0) {
    Write-Error "SystemMonitor.exe --analyze-dump exited with code $LASTEXITCODE."
}

$entries = $raw | Where-Object { $_ -match '\S' } | ForEach-Object { $_ | ConvertFrom-Json }

if (-not $entries) {
    Write-Host "No dumps found under $Path." -ForegroundColor Yellow
    exit 0
}

$entries | Select-Object `
    @{n='File';    e={$_.filename}},
    @{n='Size';    e={'{0,8:N0}' -f $_.size_bytes}},
    @{n='BugCheck';e={ if ($_.parsed) { $_.bugcheck_code } else { '(unparsed)' } }},
    @{n='Name';    e={ if ($_.parsed) { $_.bugcheck_name } else { $_.reason } }} `
    | Format-Table -AutoSize

$all       = @($entries)
$parsed    = @($entries | Where-Object { $_.parsed }).Count
$unparsed  = @($entries | Where-Object { -not $_.parsed }).Count
Write-Host ""
Write-Host "Total: $($all.Count)  |  Parsed: $parsed  |  Unparsed: $unparsed"

if ($WriteManifest) {
    $manifestPath = Join-Path $Path 'manifest.jsonl'
    $raw | Where-Object { $_ -match '\S' } | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    Write-Host "Wrote: $manifestPath"
}
