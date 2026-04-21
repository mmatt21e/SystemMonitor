# SystemMonitor

Windows diagnostic tool for PCs experiencing unexplained failures. Captures
hardware sensors, Windows event logs, and system state, then classifies
anomalies as Internal (PC hardware), External (power/thermal/network), or
Indeterminate.

## Quickstart

### Build

    dotnet build -c Release

### Publish a self-contained exe

    dotnet publish src/SystemMonitor.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

Output: `src/SystemMonitor.App/bin/Release/net8.0-windows/win-x64/publish/SystemMonitor.App.exe`

### Run (GUI)

Copy the exe (and optionally `config.example.json` renamed to `config.json`)
to the target machine. Double-click `SystemMonitor.App.exe`. For full
capability (hardware temps, voltages, SMART) right-click → Run as administrator.

### Run (headless)

    SystemMonitor.App.exe --headless --output C:\SystemMonitor\Logs

Runs until Ctrl+C. Suitable for unattended long runs on headless or
locked-down machines.

### CLI flags

| Flag | Purpose |
|---|---|
| `--config <path>` | Path to config.json. Defaults are used if file missing. |
| `--output <dir>`  | Override log directory from config. |
| `--headless`      | Run without UI. |
| `--help`, `-h`    | Show usage. |

## Architecture

See `docs/design/2026-04-15-system-monitor-design.md`.

## Manual Release Validation

See `docs/smoke-test-checklist.md`.

## Development

    dotnet test                                 # unit tests
    dotnet test tests/SystemMonitor.Engine.IntegrationTests  # Windows-only
