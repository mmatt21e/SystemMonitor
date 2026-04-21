# System Monitor — Design Spec

**Date:** 2026-04-15
**Status:** Approved

---

## 1. Purpose

A deployable Windows diagnostic tool for PCs experiencing unexplained failures. Dropped onto a problem machine, it captures hardware sensor readings, Windows event logs, and system state over time — then helps distinguish whether observed problems originate **internally** (failing hardware) or **externally** (power quality, thermal environment, network infrastructure).

The tool supports both short bursts (minutes) and extended monitoring (hours to days) to catch intermittent issues. Output is a structured log file (JSON lines) for post-analysis, with a WinForms UI for live observation while it runs.

## 2. Scope & Constraints

- **Platform:** Windows 10/11, x64. Single deployable executable.
- **Language:** C# / .NET 8 (WinForms).
- **Privilege modes:** Must run as both Administrator and standard user, with graceful capability degradation and an explicit capability report.
- **Deployment:** Single self-contained exe produced via `dotnet publish`. Configuration via a `config.json` file next to the exe (optional — sensible defaults apply when absent).
- **UI:** WinForms with a `--headless` CLI flag for unattended collection on headless or locked-down machines.

## 3. Data Points Collected

All of the following are targeted; each collector degrades based on detected capabilities:

- **CPU** — usage (overall + per-core), frequency, temps (admin), throttling events, context switches.
- **Memory** — used/available, page faults, committed bytes, ECC errors if available.
- **Power** — voltage rails (admin), battery/UPS status, power state transitions, unexpected-shutdown events.
- **Storage** — per-drive latency, queue depth, read/write errors, SMART attributes (admin), free-space trends.
- **GPU** — temp, usage, memory, TDR (Timeout Detection and Recovery) events.
- **Network** — adapter link state, packet errors/drops, latency to gateway and public DNS, DNS resolution time.
- **Windows Event Logs** — System, Application, Hardware Events channels (always captured when accessible); Security log (admin); filtered to hardware-relevant event IDs.
- **Reliability data** — WMI `Win32_ReliabilityRecords` (admin), minidump directory scan, Windows Update history, BCD boot-config inspection.
- **Hardware/software inventory** — one-shot capture at startup: CPU model, RAM config, motherboard, BIOS version, drivers, storage devices, installed software.

## 4. Internal vs. External Classification Goals

The correlation engine classifies anomalies into one of:

- **Internal** — originating in the PC's own hardware (e.g., CPU thermal runaway with no load change, SMART attribute degradation, rising memory errors).
- **External** — originating outside the PC (e.g., voltage sag correlating with Kernel-Power 41, NIC link drops correlating with packet-loss spikes to the gateway, ambient-driven temp rise across multiple sensors).
- **Indeterminate** — not enough data to classify (more common when running as standard user).

Classification is always accompanied by a confidence level and a human-readable explanation.

## 5. Architecture

Four layers, with the engine split into a reusable class library:

```
┌─────────────────────────────────────────────────────────┐
│  WinForms UI (MainForm, dialogs, user controls)         │
├─────────────────────────────────────────────────────────┤
│  Engine (UI-agnostic — SystemMonitor.Engine.dll)        │
│   ├─ Privilege Detector & Capability Discovery          │
│   ├─ Collectors (CPU, Mem, Power, Storage, GPU,         │
│   │             Network, EventLog, Reliability, Inv.)   │
│   ├─ Correlation Engine                                 │
│   └─ Logger                                             │
├─────────────────────────────────────────────────────────┤
│  Config (JSON on disk)                                  │
└─────────────────────────────────────────────────────────┘
```

### 5.1 Technology Choices

- **LibreHardwareMonitorLib** (MIT license) — hardware sensor backbone (CPU/GPU/motherboard temps, voltages, fan speeds, storage SMART).
- **System.Diagnostics.PerformanceCounter** — CPU/memory/disk usage and performance counters.
- **System.Management (WMI)** — reliability records, hardware inventory, some event data.
- **System.Diagnostics.Eventing.Reader** — Windows Event Log access.
- **System.Windows.Forms.DataVisualization.Charting** — time-series charts in the UI (built-in, no external dependency).
- **xUnit + FluentAssertions** — test framework.

### 5.2 Projects / Assemblies

- `SystemMonitor.Engine` — class library containing all non-UI logic. Referenced by both the WinForms app and the test projects.
- `SystemMonitor.App` — WinForms executable. Thin layer; no business logic.
- `SystemMonitor.Engine.Tests` — unit tests.
- `SystemMonitor.Engine.IntegrationTests` — Windows-only integration tests.

## 6. Components

### 6.1 Privilege Detector (`PrivilegeDetector.cs`)

- Uses `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)` at startup.
- Probes each sensor source (each wrapped in try/catch) to build a capability manifest.
- Writes a capability report as the first entry of every log file and displays it in the UI capability panel.

### 6.2 Collectors

Each implements `ICollector`:

```
interface ICollector {
    string Name { get; }
    bool Enabled { get; }
    TimeSpan PollingInterval { get; }
    CapabilityStatus Capability { get; }   // Full / Partial / Unavailable + reason
    IEnumerable<Reading> Collect();
}
```

Collectors (one class each):

- `CpuCollector`
- `MemoryCollector`
- `PowerCollector`
- `StorageCollector`
- `GpuCollector`
- `NetworkCollector`
- `EventLogCollector` — System / Application / Hardware Events channels, filtered to hardware-relevant IDs (Disk 7/11/51, Kernel-Power 41, WHEA entries, Disk SMART alerts, etc.).
- `ReliabilityCollector` — WMI reliability records, minidump inventory, Windows Update history (admin).
- `InventoryCollector` — one-shot inventory snapshot.

Each collector tags its readings with a confidence level (e.g., "CPU throttling detected via performance counters" when standard user vs. "CPU throttling detected, core temp 98C" when admin).

Failed collectors are disabled for a cool-down period (default 60s) and retried. After 3 consecutive failures, marked `Unavailable` until next startup.

### 6.3 Correlation Engine (`CorrelationEngine.cs`)

- Runs on its own timer (default every 30s).
- Anomaly detection: thresholds (from config) **and** rolling baseline deviation (e.g., CPU temp >20°C above its 1-hour average).
- Rule-based correlation. Example rules:
  - Voltage drop within 5s of a Kernel-Power 41 event → `External` (power).
  - Disk latency spike plus SMART attribute change → `Internal` (failing drive).
  - Link drop plus packet-loss spike to gateway → `External` (network infrastructure).
  - CPU thermal spike with no corresponding load change → `Internal` (cooling issue).
- Emits `AnomalyEvent` records containing: timestamp, involved readings, classification, confidence, and a human-readable explanation.
- With fewer data sources (standard user), flags more events as `Indeterminate` rather than guessing.

### 6.4 Logger (`Logger.cs`)

- Output format: **JSON Lines** (one JSON object per line) for easy grep/parse/stream.
- Separate files per day per category under the configured log directory:
  - `readings.jsonl` — raw sensor readings.
  - `events.jsonl` — Windows event log entries captured by `EventLogCollector`.
  - `anomalies.jsonl` — correlation engine output.
  - `inventory.json` — one-time hardware/software snapshot at startup.
  - `diagnostics.log` — internal errors from the tool itself, kept separate from monitoring data.
- Filename pattern: `<category>-YYYY-MM-DD.jsonl`.
- Size-based rotation (default 100MB — configurable).
- First entry of each file is the capability report for context.
- Write-ahead buffered; flushed on every anomaly event and every 5 seconds otherwise.
- On disk-full: stops writing, surfaces a red status in the UI, engine stays alive.

### 6.5 Config Loader (`Config.cs`)

- Looks for `config.json` next to the exe; falls back to built-in defaults when absent.
- Validates on load; aborts with line/column information on malformed JSON.
- Logs which values are user-specified vs. defaulted.
- Config covers: polling intervals per collector, enabled flags per collector, log output directory, log rotation size, anomaly thresholds, correlation rule toggles, UI refresh rate.

### 6.6 Ring Buffer

- In-memory bounded buffer per collector, sized by **reading count** (default 3600 readings per collector — one hour at 1Hz polling). Configurable.
- When full, oldest readings are overwritten (circular buffer semantics).
- Decouples collectors (producers) from logger / correlation engine / UI (consumers). A slow disk cannot stall collection.
- Readings are immutable value types — no cross-thread mutation.
- The correlation engine and UI read snapshots of the buffer; the logger drains readings as they are produced (writes are not dependent on the buffer).

### 6.7 WinForms UI

- `MainForm` layout: `MenuStrip` + `ToolStrip` + `SplitContainer` (left: capability `TreeView`; right: `TabControl`) + `DataGridView` for live event feed + `StatusStrip`.
- Tabs: Overview, CPU, Memory, Power, Storage, GPU, Network, Events.
- Overview tab: sparkline cards per subsystem with green/yellow/red health indicator and click-through to detail tabs.
- Detail tabs share a consistent pattern: current values strip at top, detail breakdown in the middle, time-series chart at the bottom.
- Config dialog uses `PropertyGrid` bound to the config object — auto-generated editable UI with categories and descriptions from attributes.
- `NotifyIcon` in the system tray for long runs; balloon notifications on anomalies.
- UI refresh is decoupled from collection (default 2 Hz refresh regardless of collector polling rate).
- UI updates go through `SynchronizationContext.Post()` / `Control.BeginInvoke()`; the UI thread never blocks on collection or logging.

### 6.8 Main Orchestrator (`Program.cs`)

- Parses minimal CLI args: `--config <path>`, `--output <path>`, `--headless`, `--help`.
- Wires up collectors, correlation engine, logger, and (unless `--headless`) the UI.
- Handles graceful shutdown (Ctrl+C in console; Form Close in UI): flushes logs, writes a final summary with run duration and anomaly count.

## 7. Data Flow

```
Timer tick → Collector.Collect() → Reading[]
                                       │
                                       ▼
                            ┌──────────────────────┐
                            │  In-memory ring buffer│
                            └──────────┬───────────┘
                                       │
                   ┌───────────────────┼───────────────────┐
                   ▼                   ▼                   ▼
           Correlation Engine      Logger              UI Layer
                   │
                   ▼
             AnomalyEvent → Logger + UI Event Feed + NotifyIcon balloon
```

## 8. Error Handling

Guiding principles — the tool runs on unhealthy machines, so robustness matters more than for typical apps:

1. **Never crash the whole tool because one sensor failed.** Each collector is individually isolated via try/catch with cool-down + retry.
2. **Log all tool-internal errors to a separate `diagnostics.log`** — distinct from monitoring data so operator mistakes or tool bugs do not pollute the data being analyzed.
3. **Degrade visibly, not silently.** Capability panel and log header show exactly what isn't working and why.
4. **Protect log integrity.** Write-ahead buffered, flushed regularly. JSONL format means a partial last line on hard crash is safely discardable.
5. **Handle disk-full.** Engine keeps running; UI flags the state in red.
6. **WMI/Performance Counter timeouts.** All WMI calls use a timeout (default 5s) to protect against hanging on broken systems.

Fail-fast exceptions (rare — these DO stop the tool):

- Log directory is unwritable.
- Config file is present but malformed.

## 9. Testing Strategy

### 9.1 Unit Tests (fast, deterministic)

Target ~80% line coverage on non-hardware code.

- Correlation engine — synthetic reading sequences exercise classification and confidence outputs. Highest-value surface.
- Config loader — valid, malformed, missing-field, out-of-range, defaulting.
- Logger — JSON format, size rotation, date rotation, header content.
- Ring buffer — bounded behavior, thread safety, ordering.
- Privilege detector logic (with mocked principal).

### 9.2 Collector Integration Tests (real Windows runner)

Per-collector suite asserting:

- Collector returns well-formed readings (non-null, recent timestamps, plausible ranges).
- Collector fails gracefully when a sensor is unreachable (via test-only `CapabilityStatus.Unavailable` injection).

These do not assert specific values — actual readings vary by machine.

### 9.3 Manual Smoke Tests

Documented in `docs/smoke-test-checklist.md` and run before any release:

- Admin vs. standard-user capability report verification.
- Long-run stability (24+ hours): memory, log rotation, UI responsiveness.
- Synthetic CPU stress → thermal anomaly fires.
- Unplug ethernet → network collector flags External.
- (Nice-to-have) Run against known-bad hardware for real-world validation.

### 9.4 Not Tested

- LibreHardwareMonitor itself is trusted as a dependency.
- No simulated hardware failure in unit tests (correlation engine uses synthetic reading sequences).
- No UI automation tests — the UI is a thin shell; all logic lives in the tested engine.

## 10. Open Items / Future Extensions

- **Export/report generation.** Future enhancement: a post-run "generate summary report" feature that turns a log run into a human-readable PDF or HTML report.
- **Remote collection.** Future enhancement: a mode that pushes logs to a central SMB/HTTP endpoint for fleet use.
- **Additional correlation rules.** The rule set will grow as real-world cases are encountered; rules are designed to be data-driven additions, not code changes.
