<#
.SYNOPSIS
    Code-signs SystemMonitor binaries and the MSI with Authenticode + RFC 3161 timestamp.

.DESCRIPTION
    Called by CI and locally after `dotnet build` produces
    artifacts/publish/SystemMonitor.App.exe and artifacts/msi/SystemMonitor.msi.

    The signing certificate is selected in this order:
      1. $env:SIGNING_CERT_THUMBPRINT -- a thumbprint already in LocalMachine\My or CurrentUser\My.
      2. $env:SIGNING_CERT_PFX_PATH  + $env:SIGNING_CERT_PFX_PASSWORD -- a PFX on disk.
      3. A dev self-signed cert named "SystemMonitor Dev Signing" under CurrentUser\My
         (auto-created if absent). Intune WILL reject this -- it exists solely so the
         signing step always runs in local development and CI dry-runs.

    The production EV cert (Azure Key Vault / DigiCert KeyLocker / hardware token)
    plugs in by setting $env:SIGNING_CERT_THUMBPRINT in the build environment.
    No code changes.

.PARAMETER Files
    One or more paths to files to sign.

.PARAMETER TimestampUrl
    RFC 3161 timestamp server. Defaults to DigiCert.

.EXAMPLE
    pwsh packaging/scripts/sign.ps1 -Files artifacts/publish/SystemMonitor.App.exe, artifacts/msi/SystemMonitor.msi
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Files,

    [string] $TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    # signtool.exe ships with the Windows SDK. Search common locations; let the caller
    # override via $env:SIGNTOOL if they have a non-standard install.
    if ($env:SIGNTOOL -and (Test-Path $env:SIGNTOOL)) { return $env:SIGNTOOL }

    $candidates = @()
    foreach ($root in @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "${env:ProgramFiles}\Windows Kits\10\bin")) {
        if (Test-Path $root) {
            $candidates += Get-ChildItem $root -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
                           Where-Object { $_.FullName -match "x64\\signtool\.exe$" } |
                           Sort-Object -Property FullName -Descending
        }
    }

    if ($candidates.Count -eq 0) {
        throw "signtool.exe not found. Install the Windows 10/11 SDK or set \$env:SIGNTOOL."
    }
    return $candidates[0].FullName
}

function Ensure-DevCert {
    $subject = "CN=SystemMonitor Dev Signing"
    $existing = Get-ChildItem Cert:\CurrentUser\My |
                Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) } |
                Select-Object -First 1
    if ($existing) { return $existing.Thumbprint }

    Write-Host "Creating dev self-signed signing certificate (not valid for production)." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Subject $subject `
        -Type CodeSigningCert `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA -KeyLength 2048 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(2)
    return $cert.Thumbprint
}

function Resolve-SigningArgs {
    if ($env:SIGNING_CERT_THUMBPRINT) {
        Write-Host "Signing with certificate thumbprint from \$env:SIGNING_CERT_THUMBPRINT" -ForegroundColor Cyan
        return @("/sha1", $env:SIGNING_CERT_THUMBPRINT)
    }
    if ($env:SIGNING_CERT_PFX_PATH) {
        if (-not (Test-Path $env:SIGNING_CERT_PFX_PATH)) {
            throw "SIGNING_CERT_PFX_PATH is set but the file does not exist: $env:SIGNING_CERT_PFX_PATH"
        }
        Write-Host "Signing with PFX from \$env:SIGNING_CERT_PFX_PATH" -ForegroundColor Cyan
        return @("/f", $env:SIGNING_CERT_PFX_PATH, "/p", $env:SIGNING_CERT_PFX_PASSWORD)
    }

    Write-Host "No production cert configured -- falling back to dev cert. DO NOT SHIP." -ForegroundColor Yellow
    $thumb = Ensure-DevCert
    return @("/sha1", $thumb)
}

$signtool = Find-SignTool
$certArgs = Resolve-SigningArgs

foreach ($file in $Files) {
    if (-not (Test-Path $file)) {
        throw "File to sign not found: $file"
    }

    $resolved = (Resolve-Path $file).Path
    Write-Host "Signing $resolved" -ForegroundColor Green
    & $signtool sign `
        /fd SHA256 `
        /td SHA256 `
        /tr $TimestampUrl `
        @certArgs `
        $resolved

    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed on $resolved (exit $LASTEXITCODE)"
    }
}

# Verify every file came out with a valid signature + timestamp.
# /pa chases the default authentication policy -- dev self-signed certs fail here
# because they chain to a local root, not a trusted CA. Treat verification as
# advisory when no production cert is configured.
$isDevCert = -not $env:SIGNING_CERT_THUMBPRINT -and -not $env:SIGNING_CERT_PFX_PATH
foreach ($file in $Files) {
    $resolved = (Resolve-Path $file).Path
    # Temporarily relax ErrorActionPreference so signtool's stderr doesn't
    # raise a terminating NativeCommandError before we inspect $LASTEXITCODE.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $verifyOutput = & $signtool verify /pa /all $resolved 2>&1
        $verifyExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }

    if ($verifyExit -ne 0) {
        if ($isDevCert) {
            Write-Host "  (dev cert: signtool verify /pa failed as expected -- chain is local-root)" -ForegroundColor DarkYellow
        } else {
            $verifyOutput | ForEach-Object { Write-Host $_ }
            throw "signtool verify failed on $resolved"
        }
    } else {
        Write-Host "  verified: $resolved" -ForegroundColor Green
    }
}

Write-Host "All files signed." -ForegroundColor Green

# Explicit exit 0 so callers don't inherit $LASTEXITCODE from the verify step
# (which can be non-zero in dev-cert mode even when signing succeeded).
exit 0
