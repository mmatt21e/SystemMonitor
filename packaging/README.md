# Packaging — MSI + Code Signing

Enterprise deployment artifacts for SystemMonitor. Produces a signed MSI that
Intune / GPO / SCCM can deploy per-machine.

## Layout

```
packaging/
├── SystemMonitor.Msi/          WiX v5 SDK-style installer project
│   ├── SystemMonitor.Msi.wixproj
│   └── Package.wxs
├── scripts/
│   └── sign.ps1                Authenticode signing with RFC 3161 timestamp
└── README.md
```

## Build

```powershell
dotnet build packaging/SystemMonitor.Msi/SystemMonitor.Msi.wixproj -c Release
```

The build transitively:
1. Publishes `SystemMonitor.App` as a self-contained single-file exe under
   `artifacts/publish/`.
2. Compiles `Package.wxs` against that publish output.
3. Drops the MSI at `packaging/SystemMonitor.Msi/bin/Release/SystemMonitor.msi`.

Required tooling: .NET 8 SDK, WiX CLI (`dotnet tool install --global wix`),
Windows 10/11 SDK (for `signtool.exe`).

## Sign

```powershell
pwsh packaging/scripts/sign.ps1 -Files `
    artifacts/publish/SystemMonitor.App.exe, `
    packaging/SystemMonitor.Msi/bin/Release/SystemMonitor.msi
```

Cert selection (first match wins):

| Environment variable            | Use case                                         |
|---------------------------------|--------------------------------------------------|
| `SIGNING_CERT_THUMBPRINT`       | Prod — cert already in `LocalMachine\My` or `CurrentUser\My` |
| `SIGNING_CERT_PFX_PATH` + `SIGNING_CERT_PFX_PASSWORD` | Prod — PFX on disk   |
| *(none)*                        | Dev — auto-creates a self-signed `CurrentUser\My` cert |

Signatures are timestamped via DigiCert's RFC 3161 server so they remain valid
after the cert expires. Dev-cert signing works end-to-end (produces a signed
MSI) but Intune / SmartScreen / App Control for Business will reject it —
that's by design. The dev cert exists so the signing step always runs in local
development and CI dry-runs.

## Install / uninstall (manual validation)

```powershell
# Install silently, writing a log for diagnosis.
msiexec /i SystemMonitor.msi /quiet /log install.log

# Uninstall.
msiexec /x SystemMonitor.msi /quiet
```

Installed files:

- `%ProgramFiles%\SystemMonitor\SystemMonitor.App.exe`
- `%ProgramFiles%\SystemMonitor\config.example.json`
- Start menu shortcut under `SystemMonitor\`

Uninstall removes the install tree but **never** touches log files (those live
under `%ProgramData%\SystemMonitor\Logs` by default, outside the install tree —
forensic data is never auto-deleted).

## What's missing — tracked

| Item                                    | Phase  |
|-----------------------------------------|--------|
| Production EV code-signing certificate  | 1.1    |
| `INSTALLSERVICE` property + service registration | 1.3 |
| ADMX policy template + Intune Settings Catalog JSON | 2 |
| MSIX packaging                          | 2+     |
| Auto-update channel                     | Out of scope (handled by Intune/WSUS) |

The upgrade code `A4E62C9B-0C6F-4A5F-8A1B-7F4C3F1D1E2A` is stable across
versions — **do not regenerate it**, or every existing installation breaks
in-place upgrades.
