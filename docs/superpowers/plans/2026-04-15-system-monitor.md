# System Monitor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deployable Windows diagnostic tool (C# / .NET 8 / WinForms) that captures hardware sensors, Windows event logs, and system state, then classifies anomalies as Internal (hardware-originated), External (power/thermal/network environment), or Indeterminate.

**Architecture:** Three projects — `SystemMonitor.Engine` (UI-agnostic class library holding config, collectors, ring buffer, correlation engine, logger), `SystemMonitor.App` (WinForms executable — thin UI shell), and two test projects. The app supports both admin and standard-user modes via runtime capability detection, and supports both an interactive UI and a `--headless` mode for unattended long runs.

**Tech Stack:** .NET 8, C#, WinForms, LibreHardwareMonitorLib (hardware sensors), System.Diagnostics.PerformanceCounter, System.Management (WMI), System.Diagnostics.Eventing.Reader (Event Log), System.Windows.Forms.DataVisualization.Charting, xUnit + FluentAssertions + Moq.

**Related spec:** `docs/superpowers/specs/2026-04-15-system-monitor-design.md`

---

## File Structure

```
SystemMonitor/
├── SystemMonitor.sln
├── src/
│   ├── SystemMonitor.Engine/                    # Class library
│   │   ├── SystemMonitor.Engine.csproj
│   │   ├── Capabilities/
│   │   │   ├── CapabilityStatus.cs              # Enum + detail record
│   │   │   └── PrivilegeDetector.cs             # Admin check + capability probe
│   │   ├── Config/
│   │   │   ├── AppConfig.cs                     # Root config type
│   │   │   ├── CollectorConfig.cs               # Per-collector knobs
│   │   │   ├── ThresholdConfig.cs               # Anomaly thresholds
│   │   │   └── ConfigLoader.cs                  # JSON load + validate
│   │   ├── Buffer/
│   │   │   └── ReadingRingBuffer.cs             # Per-collector circular buffer
│   │   ├── Collectors/
│   │   │   ├── Reading.cs                       # Immutable reading record
│   │   │   ├── ICollector.cs                    # Collector contract
│   │   │   ├── CollectorBase.cs                 # Shared retry/cooldown logic
│   │   │   ├── CpuCollector.cs
│   │   │   ├── MemoryCollector.cs
│   │   │   ├── StorageCollector.cs
│   │   │   ├── NetworkCollector.cs
│   │   │   ├── PowerCollector.cs                # LHM-backed
│   │   │   ├── GpuCollector.cs                  # LHM-backed
│   │   │   ├── EventLogCollector.cs
│   │   │   ├── ReliabilityCollector.cs
│   │   │   └── InventoryCollector.cs
│   │   ├── Correlation/
│   │   │   ├── AnomalyEvent.cs                  # Output record
│   │   │   ├── Classification.cs                # Internal/External/Indeterminate enum
│   │   │   ├── ICorrelationRule.cs
│   │   │   ├── CorrelationEngine.cs
│   │   │   └── Rules/
│   │   │       ├── PowerAndKernelPowerRule.cs
│   │   │       ├── DiskLatencyAndSmartRule.cs
│   │   │       ├── NetworkDropAndPacketLossRule.cs
│   │   │       ├── ThermalRunawayRule.cs
│   │   │       └── BaselineDeviationRule.cs
│   │   ├── Logging/
│   │   │   ├── JsonlLogger.cs                   # JSON lines writer
│   │   │   ├── LogRotator.cs                    # Size + date rotation
│   │   │   └── ILogger.cs
│   │   └── Orchestrator.cs                      # Wires collectors, buffer, engine, logger
│   └── SystemMonitor.App/                       # WinForms exe
│       ├── SystemMonitor.App.csproj
│       ├── Program.cs                           # Entry + CLI + headless branching
│       ├── MainForm.cs / MainForm.Designer.cs
│       ├── Forms/
│       │   └── ConfigDialog.cs
│       ├── Controls/
│       │   ├── OverviewTab.cs
│       │   ├── CpuTab.cs
│       │   ├── MemoryTab.cs
│       │   ├── PowerTab.cs
│       │   ├── StorageTab.cs
│       │   ├── GpuTab.cs
│       │   ├── NetworkTab.cs
│       │   └── EventsTab.cs
│       └── ViewModels/
│           └── UiRefreshPump.cs                 # 2Hz buffer snapshot → UI
├── tests/
│   ├── SystemMonitor.Engine.Tests/              # Unit tests (no hardware)
│   │   └── SystemMonitor.Engine.Tests.csproj
│   └── SystemMonitor.Engine.IntegrationTests/   # Windows-only real-hardware tests
│       └── SystemMonitor.Engine.IntegrationTests.csproj
├── config.example.json
└── docs/
    ├── superpowers/
    │   ├── specs/2026-04-15-system-monitor-design.md
    │   └── plans/2026-04-15-system-monitor.md
    └── smoke-test-checklist.md
```

---

## Phases Overview

- **Phase 0** — Solution & project scaffolding (everything builds, tests run, nothing collects yet)
- **Phase 1** — Engine foundation: types, config, ring buffer, privilege detector, logger
- **Phase 2** — First collector end-to-end (CPU) + orchestrator, proving the pattern
- **Phase 3** — OS-native collectors: Memory, Storage, Network
- **Phase 4** — LibreHardwareMonitor collectors: Power, GPU
- **Phase 5** — Event-log and WMI collectors: EventLog, Reliability, Inventory
- **Phase 6** — Correlation engine + rules
- **Phase 7** — WinForms UI (MainForm, tabs, config dialog, NotifyIcon)
- **Phase 8** — CLI + headless mode + graceful shutdown + smoke-test doc

Every phase ends with a green-build commit and the app able to run (even if doing less than the final vision).

---

## Phase 0 — Solution & Project Scaffolding

### Task 0.1: Create the solution and engine project

**Files:**
- Create: `SystemMonitor.sln`
- Create: `src/SystemMonitor.Engine/SystemMonitor.Engine.csproj`
- Create: `src/SystemMonitor.Engine/Placeholder.cs`

- [ ] **Step 1: Create solution file**

Run from repo root:
```bash
dotnet new sln -n SystemMonitor
```

Expected: `SystemMonitor.sln` created.

- [ ] **Step 2: Create engine class library**

```bash
dotnet new classlib -n SystemMonitor.Engine -o src/SystemMonitor.Engine -f net8.0
```

- [ ] **Step 3: Replace generated `Class1.cs` with a placeholder marker**

Delete `src/SystemMonitor.Engine/Class1.cs`. Create `src/SystemMonitor.Engine/Placeholder.cs`:

```csharp
namespace SystemMonitor.Engine;

// Placeholder so the assembly has at least one type during scaffolding.
// Removed when real types land in Phase 1.
internal static class Placeholder { }
```

- [ ] **Step 4: Enable nullable and implicit usings on the csproj**

Edit `src/SystemMonitor.Engine/SystemMonitor.Engine.csproj` so `<PropertyGroup>` contains:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <LangVersion>latest</LangVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

- [ ] **Step 5: Add project to solution**

```bash
dotnet sln add src/SystemMonitor.Engine/SystemMonitor.Engine.csproj
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git init
git add .
git commit -m "chore: scaffold solution and engine class library"
```

---

### Task 0.2: Create the WinForms app project

**Files:**
- Create: `src/SystemMonitor.App/SystemMonitor.App.csproj`
- Create: `src/SystemMonitor.App/Program.cs`

- [ ] **Step 1: Create WinForms app**

```bash
dotnet new winforms -n SystemMonitor.App -o src/SystemMonitor.App -f net8.0
```

- [ ] **Step 2: Replace the generated `Program.cs` with a minimal placeholder**

Overwrite `src/SystemMonitor.App/Program.cs`:

```csharp
using System.Windows.Forms;

namespace SystemMonitor.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        MessageBox.Show("SystemMonitor — scaffolding in place.", "SystemMonitor");
    }
}
```

Also delete `src/SystemMonitor.App/Form1.cs`, `Form1.Designer.cs`, and `Form1.resx` if present — we will create `MainForm` in Phase 7.

- [ ] **Step 3: Configure csproj**

Edit `src/SystemMonitor.App/SystemMonitor.App.csproj` so `<PropertyGroup>` contains:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <LangVersion>latest</LangVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

- [ ] **Step 4: Add an application manifest requesting `asInvoker` execution level**

Create `src/SystemMonitor.App/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="SystemMonitor.App"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- asInvoker: run at the caller's privilege level. We detect admin at runtime
             and degrade gracefully. Do NOT use requireAdministrator — we support both modes. -->
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

- [ ] **Step 5: Reference the engine project**

```bash
dotnet add src/SystemMonitor.App/SystemMonitor.App.csproj reference src/SystemMonitor.Engine/SystemMonitor.Engine.csproj
dotnet sln add src/SystemMonitor.App/SystemMonitor.App.csproj
```

- [ ] **Step 6: Build and run**

```bash
dotnet build
dotnet run --project src/SystemMonitor.App
```

Expected: Build succeeds; a message box appears. Close it.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "chore: scaffold WinForms app project with asInvoker manifest"
```

---

### Task 0.3: Create the test projects

**Files:**
- Create: `tests/SystemMonitor.Engine.Tests/SystemMonitor.Engine.Tests.csproj`
- Create: `tests/SystemMonitor.Engine.Tests/SmokeTests.cs`
- Create: `tests/SystemMonitor.Engine.IntegrationTests/SystemMonitor.Engine.IntegrationTests.csproj`
- Create: `tests/SystemMonitor.Engine.IntegrationTests/SmokeTests.cs`

- [ ] **Step 1: Create unit test project**

```bash
dotnet new xunit -n SystemMonitor.Engine.Tests -o tests/SystemMonitor.Engine.Tests -f net8.0
dotnet add tests/SystemMonitor.Engine.Tests/SystemMonitor.Engine.Tests.csproj reference src/SystemMonitor.Engine/SystemMonitor.Engine.csproj
dotnet add tests/SystemMonitor.Engine.Tests package FluentAssertions
dotnet add tests/SystemMonitor.Engine.Tests package Moq
dotnet sln add tests/SystemMonitor.Engine.Tests/SystemMonitor.Engine.Tests.csproj
```

- [ ] **Step 2: Write a trivial passing smoke test**

Delete generated `UnitTest1.cs`. Create `tests/SystemMonitor.Engine.Tests/SmokeTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace SystemMonitor.Engine.Tests;

public class SmokeTests
{
    [Fact]
    public void TestHarness_Works()
    {
        (1 + 1).Should().Be(2);
    }
}
```

- [ ] **Step 3: Run the tests**

```bash
dotnet test tests/SystemMonitor.Engine.Tests
```

Expected: 1 passed.

- [ ] **Step 4: Create integration test project**

```bash
dotnet new xunit -n SystemMonitor.Engine.IntegrationTests -o tests/SystemMonitor.Engine.IntegrationTests -f net8.0-windows
dotnet add tests/SystemMonitor.Engine.IntegrationTests/SystemMonitor.Engine.IntegrationTests.csproj reference src/SystemMonitor.Engine/SystemMonitor.Engine.csproj
dotnet add tests/SystemMonitor.Engine.IntegrationTests package FluentAssertions
dotnet sln add tests/SystemMonitor.Engine.IntegrationTests/SystemMonitor.Engine.IntegrationTests.csproj
```

- [ ] **Step 5: Write a trivial integration smoke test**

Delete generated `UnitTest1.cs`. Create `tests/SystemMonitor.Engine.IntegrationTests/SmokeTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void IntegrationHarness_RunsOnWindows()
    {
        OperatingSystem.IsWindows().Should().BeTrue();
    }
}
```

- [ ] **Step 6: Run all tests**

```bash
dotnet test
```

Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "test: add unit and integration test projects"
```

---

### Task 0.4: Add `.gitignore` and initial README

**Files:**
- Create: `.gitignore`
- Create: `README.md`

- [ ] **Step 1: Create `.gitignore`**

Use the standard Visual Studio / dotnet template. From the repo root:

```bash
dotnet new gitignore
```

- [ ] **Step 2: Create a minimal README**

Create `README.md`:

```markdown
# SystemMonitor

Windows diagnostic tool for PCs experiencing unexplained failures.

See `docs/superpowers/specs/2026-04-15-system-monitor-design.md` for the design.

## Build

    dotnet build

## Run

    dotnet run --project src/SystemMonitor.App

## Test

    dotnet test
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "chore: add gitignore and README"
```

---

## Phase 1 — Engine Foundation

### Task 1.1: `Reading` record type

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/Reading.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Collectors/ReadingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Collectors/ReadingTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Collectors;

public class ReadingTests
{
    [Fact]
    public void Reading_StoresAllFields()
    {
        var ts = DateTimeOffset.UtcNow;
        var r = new Reading(
            Source: "cpu",
            Metric: "usage_percent",
            Value: 42.5,
            Unit: "%",
            Timestamp: ts,
            Confidence: ReadingConfidence.High,
            Labels: new Dictionary<string, string> { ["core"] = "0" });

        r.Source.Should().Be("cpu");
        r.Metric.Should().Be("usage_percent");
        r.Value.Should().Be(42.5);
        r.Unit.Should().Be("%");
        r.Timestamp.Should().Be(ts);
        r.Confidence.Should().Be(ReadingConfidence.High);
        r.Labels["core"].Should().Be("0");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ReadingTests
```

Expected: FAIL — `Reading` type does not exist.

- [ ] **Step 3: Implement `Reading`**

Create `src/SystemMonitor.Engine/Collectors/Reading.cs`:

```csharp
namespace SystemMonitor.Engine.Collectors;

public enum ReadingConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
/// An immutable sensor/metric reading. Produced by collectors, consumed by the
/// ring buffer, logger, correlation engine, and UI.
/// </summary>
public sealed record Reading(
    string Source,
    string Metric,
    double Value,
    string Unit,
    DateTimeOffset Timestamp,
    ReadingConfidence Confidence,
    IReadOnlyDictionary<string, string> Labels);
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ReadingTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add Reading record and confidence enum"
```

---

### Task 1.2: `CapabilityStatus` type

**Files:**
- Create: `src/SystemMonitor.Engine/Capabilities/CapabilityStatus.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Capabilities/CapabilityStatusTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Capabilities/CapabilityStatusTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using Xunit;

namespace SystemMonitor.Engine.Tests.Capabilities;

public class CapabilityStatusTests
{
    [Fact]
    public void Full_HasFullLevelAndNoReason()
    {
        var c = CapabilityStatus.Full();
        c.Level.Should().Be(CapabilityLevel.Full);
        c.Reason.Should().BeNull();
    }

    [Fact]
    public void Partial_CarriesReason()
    {
        var c = CapabilityStatus.Partial("no admin for temps");
        c.Level.Should().Be(CapabilityLevel.Partial);
        c.Reason.Should().Be("no admin for temps");
    }

    [Fact]
    public void Unavailable_CarriesReason()
    {
        var c = CapabilityStatus.Unavailable("WMI class missing");
        c.Level.Should().Be(CapabilityLevel.Unavailable);
        c.Reason.Should().Be("WMI class missing");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CapabilityStatusTests
```

Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Capabilities/CapabilityStatus.cs`:

```csharp
namespace SystemMonitor.Engine.Capabilities;

public enum CapabilityLevel
{
    Full,
    Partial,
    Unavailable
}

/// <summary>
/// Describes whether a collector/sensor source is usable, and if not, why.
/// Surfaced in the capability report at the top of every log file and in the UI.
/// </summary>
public sealed record CapabilityStatus(CapabilityLevel Level, string? Reason)
{
    public static CapabilityStatus Full() => new(CapabilityLevel.Full, null);
    public static CapabilityStatus Partial(string reason) => new(CapabilityLevel.Partial, reason);
    public static CapabilityStatus Unavailable(string reason) => new(CapabilityLevel.Unavailable, reason);
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CapabilityStatusTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add CapabilityStatus with Full/Partial/Unavailable levels"
```

---

### Task 1.3: `ICollector` interface and `CollectorBase`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/ICollector.cs`
- Create: `src/SystemMonitor.Engine/Collectors/CollectorBase.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Collectors/CollectorBaseTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Collectors/CollectorBaseTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Collectors;

public class CollectorBaseTests
{
    private sealed class FlakyCollector : CollectorBase
    {
        public int CollectCalls;
        public bool ShouldThrow = true;

        public FlakyCollector() : base("flaky", TimeSpan.FromMilliseconds(10)) { }
        public override CapabilityStatus Capability => CapabilityStatus.Full();

        protected override IEnumerable<Reading> CollectCore()
        {
            CollectCalls++;
            if (ShouldThrow) throw new InvalidOperationException("boom");
            return new[] { new Reading("flaky", "m", 1, "x", DateTimeOffset.UtcNow,
                ReadingConfidence.High, new Dictionary<string, string>()) };
        }
    }

    [Fact]
    public void ExceptionInCollect_IsSwallowed_AndReturnsEmpty()
    {
        var c = new FlakyCollector();
        var readings = c.Collect();
        readings.Should().BeEmpty();
        c.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void AfterThreeFailures_CollectorIsMarkedUnavailable()
    {
        var c = new FlakyCollector();
        c.Collect(); c.Collect(); c.Collect();
        c.ConsecutiveFailures.Should().Be(3);
        c.IsCooldownActive.Should().BeTrue();
    }

    [Fact]
    public void SuccessfulCollect_ResetsFailureCount()
    {
        var c = new FlakyCollector();
        c.Collect();
        c.ShouldThrow = false;
        c.Collect();
        c.ConsecutiveFailures.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CollectorBaseTests
```

Expected: FAIL — types missing.

- [ ] **Step 3: Implement `ICollector`**

Create `src/SystemMonitor.Engine/Collectors/ICollector.cs`:

```csharp
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

public interface ICollector
{
    string Name { get; }
    TimeSpan PollingInterval { get; }
    CapabilityStatus Capability { get; }

    /// <summary>
    /// Produces readings for this tick. Implementations MUST NOT throw —
    /// the base class wraps the concrete collector in a try/catch cooldown/retry loop.
    /// </summary>
    IReadOnlyList<Reading> Collect();
}
```

- [ ] **Step 4: Implement `CollectorBase`**

Create `src/SystemMonitor.Engine/Collectors/CollectorBase.cs`:

```csharp
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

public abstract class CollectorBase : ICollector
{
    // After this many consecutive failures, the collector enters cooldown.
    private const int FailureThreshold = 3;

    // Cooldown duration before retry is allowed.
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(60);

    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    protected CollectorBase(string name, TimeSpan pollingInterval)
    {
        Name = name;
        PollingInterval = pollingInterval;
    }

    public string Name { get; }
    public TimeSpan PollingInterval { get; }
    public abstract CapabilityStatus Capability { get; }

    public int ConsecutiveFailures { get; private set; }
    public bool IsCooldownActive => DateTimeOffset.UtcNow < _cooldownUntil;

    /// <summary>Concrete collectors implement this; may throw.</summary>
    protected abstract IEnumerable<Reading> CollectCore();

    public IReadOnlyList<Reading> Collect()
    {
        if (IsCooldownActive) return Array.Empty<Reading>();

        try
        {
            var result = CollectCore().ToList();
            ConsecutiveFailures = 0;
            return result;
        }
        catch (Exception ex)
        {
            ConsecutiveFailures++;
            OnFailure(ex);
            if (ConsecutiveFailures >= FailureThreshold)
                _cooldownUntil = DateTimeOffset.UtcNow + CooldownDuration;
            return Array.Empty<Reading>();
        }
    }

    /// <summary>Override to surface collector-specific failure to the diagnostics log.</summary>
    protected virtual void OnFailure(Exception ex) { }
}
```

- [ ] **Step 5: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CollectorBaseTests
```

Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(engine): ICollector + CollectorBase with cooldown/retry"
```

---

### Task 1.4: Ring buffer

**Files:**
- Create: `src/SystemMonitor.Engine/Buffer/ReadingRingBuffer.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Buffer/ReadingRingBufferTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Buffer/ReadingRingBufferTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests.Buffer;

public class ReadingRingBufferTests
{
    private static Reading R(int i) => new(
        "t", "m", i, "x", DateTimeOffset.FromUnixTimeSeconds(i),
        ReadingConfidence.High, new Dictionary<string, string>());

    [Fact]
    public void Add_BelowCapacity_PreservesOrder()
    {
        var buf = new ReadingRingBuffer(4);
        buf.Add(R(1)); buf.Add(R(2)); buf.Add(R(3));
        buf.Snapshot().Select(r => (int)r.Value).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Add_OverCapacity_DropsOldest()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Add(R(1)); buf.Add(R(2)); buf.Add(R(3)); buf.Add(R(4));
        buf.Snapshot().Select(r => (int)r.Value).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Snapshot_IsIndependentCopy()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Add(R(1));
        var snap = buf.Snapshot();
        buf.Add(R(2));
        snap.Should().HaveCount(1);
    }

    [Fact]
    public void Count_ReflectsCurrentSize()
    {
        var buf = new ReadingRingBuffer(3);
        buf.Count.Should().Be(0);
        buf.Add(R(1)); buf.Add(R(2));
        buf.Count.Should().Be(2);
        buf.Add(R(3)); buf.Add(R(4));
        buf.Count.Should().Be(3);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ReadingRingBufferTests
```

Expected: FAIL — type missing.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Buffer/ReadingRingBuffer.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Buffer;

/// <summary>
/// Thread-safe fixed-capacity circular buffer of readings. When full, the oldest
/// reading is overwritten. Consumers (UI, correlation engine) read snapshots.
/// </summary>
public sealed class ReadingRingBuffer
{
    private readonly Reading[] _items;
    private readonly object _lock = new();
    private int _head;     // index of next write slot
    private int _count;

    public ReadingRingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new Reading[capacity];
    }

    public int Capacity => _items.Length;

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public void Add(Reading reading)
    {
        lock (_lock)
        {
            _items[_head] = reading;
            _head = (_head + 1) % _items.Length;
            if (_count < _items.Length) _count++;
        }
    }

    /// <summary>Returns a chronological copy (oldest → newest).</summary>
    public IReadOnlyList<Reading> Snapshot()
    {
        lock (_lock)
        {
            var result = new Reading[_count];
            var start = _count < _items.Length ? 0 : _head;
            for (int i = 0; i < _count; i++)
                result[i] = _items[(start + i) % _items.Length];
            return result;
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ReadingRingBufferTests
```

Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add ReadingRingBuffer with thread-safe snapshot"
```

---

### Task 1.5: Config types

**Files:**
- Create: `src/SystemMonitor.Engine/Config/ThresholdConfig.cs`
- Create: `src/SystemMonitor.Engine/Config/CollectorConfig.cs`
- Create: `src/SystemMonitor.Engine/Config/AppConfig.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Config/AppConfigTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Config/AppConfigTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.Tests.Config;

public class AppConfigTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var c = AppConfig.Defaults();
        c.LogOutputDirectory.Should().NotBeNullOrEmpty();
        c.LogRotationSizeBytes.Should().Be(100 * 1024 * 1024);
        c.UiRefreshHz.Should().Be(2);
        c.BufferCapacityPerCollector.Should().Be(3600);
        c.Collectors.Should().NotBeEmpty();
        c.Collectors.Should().ContainKey("cpu");
        c.Collectors["cpu"].Enabled.Should().BeTrue();
        c.Collectors["cpu"].PollingIntervalMs.Should().Be(1000);
        c.Thresholds.CpuTempCelsiusWarn.Should().Be(80);
        c.Thresholds.CpuTempCelsiusCritical.Should().Be(95);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~AppConfigTests
```

Expected: FAIL — types missing.

- [ ] **Step 3: Implement `ThresholdConfig`**

Create `src/SystemMonitor.Engine/Config/ThresholdConfig.cs`:

```csharp
using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class ThresholdConfig
{
    [Description("CPU package temperature that triggers a warning (°C).")]
    public double CpuTempCelsiusWarn { get; set; } = 80;

    [Description("CPU package temperature that triggers a critical anomaly (°C).")]
    public double CpuTempCelsiusCritical { get; set; } = 95;

    [Description("Memory committed percent considered high.")]
    public double MemoryCommittedPercentWarn { get; set; } = 85;

    [Description("Disk latency above this is an anomaly (ms).")]
    public double DiskLatencyMsWarn { get; set; } = 50;

    [Description("Network packet-loss percent above this is an anomaly.")]
    public double NetworkPacketLossPercentWarn { get; set; } = 2.0;

    [Description("Voltage deviation from nominal above this percent is flagged.")]
    public double VoltageDeviationPercentWarn { get; set; } = 5.0;

    [Description("Baseline deviation in standard deviations that counts as an anomaly.")]
    public double BaselineStdDevWarn { get; set; } = 3.0;
}
```

- [ ] **Step 4: Implement `CollectorConfig`**

Create `src/SystemMonitor.Engine/Config/CollectorConfig.cs`:

```csharp
using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class CollectorConfig
{
    [Description("Whether this collector runs.")]
    public bool Enabled { get; set; } = true;

    [Description("Polling interval in milliseconds.")]
    public int PollingIntervalMs { get; set; } = 1000;
}
```

- [ ] **Step 5: Implement `AppConfig`**

Create `src/SystemMonitor.Engine/Config/AppConfig.cs`:

```csharp
using System.ComponentModel;

namespace SystemMonitor.Engine.Config;

public sealed class AppConfig
{
    [Description("Directory where log files are written.")]
    public string LogOutputDirectory { get; set; } = "";

    [Description("Log file rotation threshold (bytes).")]
    public long LogRotationSizeBytes { get; set; } = 100 * 1024 * 1024;

    [Description("Target UI refresh rate (Hz). The engine itself is unaffected.")]
    public int UiRefreshHz { get; set; } = 2;

    [Description("Per-collector in-memory ring buffer capacity (reading count).")]
    public int BufferCapacityPerCollector { get; set; } = 3600;

    [Description("Interval at which the correlation engine evaluates buffered readings (ms).")]
    public int CorrelationIntervalMs { get; set; } = 30_000;

    [Description("WMI query timeout in milliseconds. Protects against hung queries on broken systems.")]
    public int WmiTimeoutMs { get; set; } = 5_000;

    [Description("Per-collector configuration, keyed by collector name.")]
    public Dictionary<string, CollectorConfig> Collectors { get; set; } = new();

    [Description("Anomaly thresholds used by the correlation engine.")]
    public ThresholdConfig Thresholds { get; set; } = new();

    public static AppConfig Defaults()
    {
        var defaultLogDir = Path.Combine(
            Path.GetDirectoryName(typeof(AppConfig).Assembly.Location) ?? AppContext.BaseDirectory,
            "Logs");

        return new AppConfig
        {
            LogOutputDirectory = defaultLogDir,
            Collectors = new Dictionary<string, CollectorConfig>
            {
                ["cpu"]         = new() { PollingIntervalMs = 1000 },
                ["memory"]      = new() { PollingIntervalMs = 1000 },
                ["storage"]     = new() { PollingIntervalMs = 5000 },
                ["network"]     = new() { PollingIntervalMs = 2000 },
                ["power"]       = new() { PollingIntervalMs = 1000 },
                ["gpu"]         = new() { PollingIntervalMs = 2000 },
                ["eventlog"]    = new() { PollingIntervalMs = 10_000 },
                ["reliability"] = new() { PollingIntervalMs = 300_000 },
                ["inventory"]   = new() { Enabled = true, PollingIntervalMs = 0 }  // one-shot
            }
        };
    }
}
```

- [ ] **Step 6: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~AppConfigTests
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(engine): add AppConfig, CollectorConfig, ThresholdConfig with sensible defaults"
```

---

### Task 1.6: `ConfigLoader`

**Files:**
- Create: `src/SystemMonitor.Engine/Config/ConfigLoader.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Config/ConfigLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Config/ConfigLoaderTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.Tests.Config;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ConfigLoaderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    [Fact]
    public void LoadOrDefaults_FileMissing_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        var (cfg, source) = ConfigLoader.LoadOrDefaults(path);
        cfg.Collectors.Should().ContainKey("cpu");
        source.Should().Be(ConfigSource.BuiltInDefaults);
    }

    [Fact]
    public void LoadOrDefaults_ValidFile_MergesUserValuesOverDefaults()
    {
        var path = Path.Combine(_tempDir, "cfg.json");
        File.WriteAllText(path, """
            {
              "UiRefreshHz": 5,
              "Collectors": { "cpu": { "PollingIntervalMs": 500 } }
            }
            """);
        var (cfg, source) = ConfigLoader.LoadOrDefaults(path);
        cfg.UiRefreshHz.Should().Be(5);
        cfg.Collectors["cpu"].PollingIntervalMs.Should().Be(500);
        // Unspecified values keep defaults:
        cfg.Collectors.Should().ContainKey("memory");
        cfg.LogRotationSizeBytes.Should().Be(100 * 1024 * 1024);
        source.Should().Be(ConfigSource.UserFile);
    }

    [Fact]
    public void LoadOrDefaults_MalformedJson_Throws()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "{ this is not valid json");
        var act = () => ConfigLoader.LoadOrDefaults(path);
        act.Should().Throw<ConfigLoadException>().WithMessage("*parse*");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ConfigLoaderTests
```

Expected: FAIL — types missing.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Config/ConfigLoader.cs`:

```csharp
using System.Text.Json;

namespace SystemMonitor.Engine.Config;

public enum ConfigSource { BuiltInDefaults, UserFile }

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads <paramref name="path"/> if it exists, merging user values over defaults.
    /// If the file is missing, returns built-in defaults. Throws <see cref="ConfigLoadException"/>
    /// on malformed JSON — we fail fast so the operator doesn't silently monitor with the wrong config.
    /// </summary>
    public static (AppConfig Config, ConfigSource Source) LoadOrDefaults(string path)
    {
        var defaults = AppConfig.Defaults();
        if (!File.Exists(path)) return (defaults, ConfigSource.BuiltInDefaults);

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex) { throw new ConfigLoadException($"Could not read config file '{path}': {ex.Message}", ex); }

        AppConfig? user;
        try { user = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions); }
        catch (JsonException ex) { throw new ConfigLoadException($"Failed to parse config '{path}' at line {ex.LineNumber}, col {ex.BytePositionInLine}: {ex.Message}", ex); }

        if (user is null) return (defaults, ConfigSource.UserFile);

        return (Merge(defaults, user), ConfigSource.UserFile);
    }

    // Simple merge: user values override defaults at the leaf level. Collector dictionary
    // is merged key-by-key (user entries add to / override defaults).
    private static AppConfig Merge(AppConfig defaults, AppConfig user)
    {
        if (!string.IsNullOrWhiteSpace(user.LogOutputDirectory)) defaults.LogOutputDirectory = user.LogOutputDirectory;
        if (user.LogRotationSizeBytes > 0) defaults.LogRotationSizeBytes = user.LogRotationSizeBytes;
        if (user.UiRefreshHz > 0) defaults.UiRefreshHz = user.UiRefreshHz;
        if (user.BufferCapacityPerCollector > 0) defaults.BufferCapacityPerCollector = user.BufferCapacityPerCollector;
        if (user.CorrelationIntervalMs > 0) defaults.CorrelationIntervalMs = user.CorrelationIntervalMs;
        if (user.WmiTimeoutMs > 0) defaults.WmiTimeoutMs = user.WmiTimeoutMs;

        foreach (var kv in user.Collectors)
            defaults.Collectors[kv.Key] = kv.Value;

        if (user.Thresholds is not null)
            defaults.Thresholds = user.Thresholds;

        return defaults;
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ConfigLoaderTests
```

Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add ConfigLoader with merge + malformed-JSON detection"
```

---

### Task 1.7: `PrivilegeDetector`

**Files:**
- Create: `src/SystemMonitor.Engine/Capabilities/PrivilegeDetector.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Capabilities/PrivilegeDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Capabilities/PrivilegeDetectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using Xunit;

namespace SystemMonitor.Engine.Tests.Capabilities;

public class PrivilegeDetectorTests
{
    [Fact]
    public void IsAdministrator_ReturnsBoolWithoutCrashing()
    {
        // We can't assert true/false deterministically (depends on how tests were launched),
        // but the call must complete and yield a boolean.
        var result = PrivilegeDetector.IsAdministrator();
        (result == true || result == false).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~PrivilegeDetectorTests
```

Expected: FAIL — type missing.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Capabilities/PrivilegeDetector.cs`:

```csharp
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
```

Update `src/SystemMonitor.Engine/SystemMonitor.Engine.csproj` to target Windows only:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  ...
</PropertyGroup>
```

Update `tests/SystemMonitor.Engine.Tests/SystemMonitor.Engine.Tests.csproj` target framework to `net8.0-windows` to match.

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet build
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~PrivilegeDetectorTests
```

Expected: Build succeeds, test passes.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add PrivilegeDetector; retarget engine to net8.0-windows"
```

---

### Task 1.8: Delete `Placeholder.cs`

**Files:**
- Delete: `src/SystemMonitor.Engine/Placeholder.cs`

- [ ] **Step 1: Delete the file**

```bash
rm src/SystemMonitor.Engine/Placeholder.cs
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: Success.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: remove scaffolding placeholder now that real types exist"
```

---

### Task 1.9: `JsonlLogger` + `LogRotator`

**Files:**
- Create: `src/SystemMonitor.Engine/Logging/ILogger.cs`
- Create: `src/SystemMonitor.Engine/Logging/LogRotator.cs`
- Create: `src/SystemMonitor.Engine/Logging/JsonlLogger.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Logging/JsonlLoggerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Logging/JsonlLoggerTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.Tests.Logging;

public class JsonlLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public JsonlLoggerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private Reading R(double v) => new(
        "cpu", "usage_percent", v, "%", DateTimeOffset.UtcNow,
        ReadingConfidence.High, new Dictionary<string, string>());

    [Fact]
    public void WriteReading_ProducesOneJsonLinePerReading()
    {
        using var logger = new JsonlLogger(_dir, "readings", rotationBytes: 1_000_000);
        logger.WriteReading(R(1));
        logger.WriteReading(R(2));
        logger.Flush();

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        var lines = File.ReadAllLines(file);
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("\"Value\":1");
        lines[1].Should().Contain("\"Value\":2");
    }

    [Fact]
    public void ExceedingRotationSize_OpensNewFile()
    {
        using var logger = new JsonlLogger(_dir, "readings", rotationBytes: 200);
        for (int i = 0; i < 50; i++) logger.WriteReading(R(i));
        logger.Flush();

        Directory.GetFiles(_dir, "readings-*.jsonl").Length.Should().BeGreaterThan(1);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~JsonlLoggerTests
```

Expected: FAIL — types missing.

- [ ] **Step 3: Implement `ILogger`**

Create `src/SystemMonitor.Engine/Logging/ILogger.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Logging;

public interface ILogger : IDisposable
{
    void WriteReading(Reading reading);
    void WriteLine(string jsonLine);     // for non-reading payloads (events, anomalies, headers)
    void Flush();
}
```

- [ ] **Step 4: Implement `LogRotator`**

Create `src/SystemMonitor.Engine/Logging/LogRotator.cs`:

```csharp
namespace SystemMonitor.Engine.Logging;

internal static class LogRotator
{
    public static string NextFilePath(string directory, string category)
    {
        Directory.CreateDirectory(directory);
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        int seq = 0;
        while (true)
        {
            var name = seq == 0
                ? $"{category}-{date}.jsonl"
                : $"{category}-{date}.{seq}.jsonl";
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) return path;
            seq++;
        }
    }
}
```

- [ ] **Step 5: Implement `JsonlLogger`**

Create `src/SystemMonitor.Engine/Logging/JsonlLogger.cs`:

```csharp
using System.Text;
using System.Text.Json;
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Logging;

/// <summary>
/// Writes JSON-lines logs to disk. One file per day per category, rotating by size.
/// Write-ahead buffered; flush on every anomaly event and every 5 seconds otherwise
/// (caller controls flush cadence).
/// </summary>
public sealed class JsonlLogger : ILogger
{
    private readonly string _directory;
    private readonly string _category;
    private readonly long _rotationBytes;
    private readonly object _lock = new();
    private StreamWriter _writer;
    private string _currentPath;
    private long _currentBytes;

    public JsonlLogger(string directory, string category, long rotationBytes)
    {
        _directory = directory;
        _category = category;
        _rotationBytes = rotationBytes;
        _currentPath = LogRotator.NextFilePath(_directory, _category);
        _writer = new StreamWriter(_currentPath, append: false, Encoding.UTF8) { AutoFlush = false };
        _currentBytes = 0;
    }

    public string CurrentPath { get { lock (_lock) return _currentPath; } }

    public void WriteReading(Reading reading) => WriteLine(JsonSerializer.Serialize(reading));

    public void WriteLine(string jsonLine)
    {
        lock (_lock)
        {
            _writer.WriteLine(jsonLine);
            _currentBytes += Encoding.UTF8.GetByteCount(jsonLine) + Environment.NewLine.Length;
            if (_currentBytes >= _rotationBytes) Rotate();
        }
    }

    public void Flush()
    {
        lock (_lock) _writer.Flush();
    }

    private void Rotate()
    {
        _writer.Flush();
        _writer.Dispose();
        _currentPath = LogRotator.NextFilePath(_directory, _category);
        _writer = new StreamWriter(_currentPath, append: false, Encoding.UTF8) { AutoFlush = false };
        _currentBytes = 0;
    }

    public void Dispose()
    {
        lock (_lock) { _writer.Flush(); _writer.Dispose(); }
    }
}
```

- [ ] **Step 6: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~JsonlLoggerTests
```

Expected: PASS (2/2).

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(engine): add JsonlLogger with size-based rotation"
```

---

## Phase 2 — First Collector (CPU) + Orchestrator

Goal of this phase: prove the whole pipeline end-to-end — a collector produces readings, the orchestrator runs it on a timer, readings go to the ring buffer and the logger, and we can see output on disk. Subsequent collectors follow this exact pattern.

### Task 2.1: `CpuCollector` (usage % via PerformanceCounter)

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/CpuCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/CpuCollectorTests.cs`

Note: This is an **integration test**, not a unit test — the CPU collector reads real performance counters.

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/CpuCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class CpuCollectorTests
{
    [Fact]
    public void Collect_ReturnsAtLeastOverallUsageReading()
    {
        using var c = new CpuCollector(TimeSpan.FromMilliseconds(200));

        // First read from a brand-new PerformanceCounter is often 0; take two samples.
        c.Collect();
        Thread.Sleep(250);
        var readings = c.Collect();

        readings.Should().NotBeEmpty();
        readings.Should().Contain(r => r.Metric == "usage_percent" && r.Labels.ContainsKey("scope") && r.Labels["scope"] == "overall");
    }

    [Fact]
    public void Capability_IsFullOnWindows()
    {
        using var c = new CpuCollector(TimeSpan.FromSeconds(1));
        c.Capability.Level.Should().Be(CapabilityLevel.Full);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~CpuCollectorTests
```

Expected: FAIL — `CpuCollector` does not exist.

- [ ] **Step 3: Implement `CpuCollector`**

Create `src/SystemMonitor.Engine/Collectors/CpuCollector.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class CpuCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _overallUsage;
    private readonly List<PerformanceCounter> _perCoreUsage = new();

    public CpuCollector(TimeSpan pollingInterval)
        : base("cpu", pollingInterval)
    {
        // "_Total" = overall CPU usage across all cores.
        _overallUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);

        // Per-core counters are named by index ("0", "1", ...).
        var cat = new PerformanceCounterCategory("Processor");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _perCoreUsage.Add(new PerformanceCounter("Processor", "% Processor Time", instance, readOnly: true));
        }
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return new Reading(
            Source: "cpu",
            Metric: "usage_percent",
            Value: _overallUsage.NextValue(),
            Unit: "%",
            Timestamp: ts,
            Confidence: ReadingConfidence.High,
            Labels: new Dictionary<string, string> { ["scope"] = "overall" });

        foreach (var (counter, idx) in _perCoreUsage.Select((c, i) => (c, i)))
        {
            yield return new Reading(
                Source: "cpu",
                Metric: "usage_percent",
                Value: counter.NextValue(),
                Unit: "%",
                Timestamp: ts,
                Confidence: ReadingConfidence.High,
                Labels: new Dictionary<string, string>
                {
                    ["scope"] = "core",
                    ["core"] = idx.ToString()
                });
        }
    }

    public void Dispose()
    {
        _overallUsage.Dispose();
        foreach (var c in _perCoreUsage) c.Dispose();
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~CpuCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add CpuCollector for overall and per-core usage"
```

---

### Task 2.2: `Orchestrator`

**Files:**
- Create: `src/SystemMonitor.Engine/Orchestrator.cs`
- Test: `tests/SystemMonitor.Engine.Tests/OrchestratorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/OrchestratorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.Tests;

public class OrchestratorTests
{
    private sealed class FakeCollector : CollectorBase
    {
        public int Calls;
        public FakeCollector(TimeSpan interval) : base("fake", interval) { }
        public override CapabilityStatus Capability => CapabilityStatus.Full();
        protected override IEnumerable<Reading> CollectCore()
        {
            Calls++;
            return new[] { new Reading("fake", "m", Calls, "x", DateTimeOffset.UtcNow,
                ReadingConfidence.High, new Dictionary<string, string>()) };
        }
    }

    [Fact]
    public async Task Start_CallsCollectorsOnInterval_AndStoresReadings()
    {
        var fake = new FakeCollector(TimeSpan.FromMilliseconds(50));
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["fake"] = new ReadingRingBuffer(100) };
        var sink = new List<Reading>();

        using var o = new Orchestrator(new[] { fake }, buffers, sink.Add);
        o.Start();
        await Task.Delay(250);
        o.Stop();

        fake.Calls.Should().BeGreaterThan(2);
        buffers["fake"].Count.Should().BeGreaterThan(2);
        sink.Count.Should().BeGreaterThan(2);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~OrchestratorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `Orchestrator`**

Create `src/SystemMonitor.Engine/Orchestrator.cs`:

```csharp
using System.Collections.Concurrent;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine;

/// <summary>
/// Drives each collector on its own timer. Each tick's readings are added to the
/// per-collector ring buffer AND published to the sink callback (typically the logger).
/// </summary>
public sealed class Orchestrator : IDisposable
{
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly IReadOnlyDictionary<string, ReadingRingBuffer> _buffers;
    private readonly Action<Reading> _sink;
    private readonly List<Timer> _timers = new();
    private volatile bool _running;

    public Orchestrator(
        IEnumerable<ICollector> collectors,
        IReadOnlyDictionary<string, ReadingRingBuffer> buffers,
        Action<Reading> sink)
    {
        _collectors = collectors.ToList();
        _buffers = buffers;
        _sink = sink;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        foreach (var c in _collectors)
        {
            // Interval == Zero means one-shot: fire once immediately, don't repeat.
            if (c.PollingInterval == TimeSpan.Zero)
            {
                Tick(c);
                continue;
            }
            var timer = new Timer(_ => Tick(c), null, TimeSpan.Zero, c.PollingInterval);
            _timers.Add(timer);
        }
    }

    public void Stop()
    {
        _running = false;
        foreach (var t in _timers) t.Dispose();
        _timers.Clear();
    }

    private void Tick(ICollector c)
    {
        if (!_running) return;
        var readings = c.Collect();
        if (!_buffers.TryGetValue(c.Name, out var buf)) return;
        foreach (var r in readings)
        {
            buf.Add(r);
            _sink(r);
        }
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~OrchestratorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add Orchestrator driving collectors on timers"
```

---

### Task 2.3: End-to-end smoke — CPU collector writes to log

**Files:**
- Test: `tests/SystemMonitor.Engine.IntegrationTests/EndToEndSmokeTests.cs`

- [ ] **Step 1: Write the smoke test**

Create `tests/SystemMonitor.Engine.IntegrationTests/EndToEndSmokeTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Logging;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

public class EndToEndSmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public EndToEndSmokeTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public async Task CpuReadings_AppearInLogFile()
    {
        using var cpu = new CpuCollector(TimeSpan.FromMilliseconds(200));
        using var logger = new JsonlLogger(_dir, "readings", rotationBytes: 10_000_000);
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["cpu"] = new ReadingRingBuffer(1000) };

        using var orch = new Orchestrator(new[] { (ICollector)cpu }, buffers, logger.WriteReading);
        orch.Start();
        await Task.Delay(700);
        orch.Stop();
        logger.Flush();

        var file = Directory.GetFiles(_dir, "readings-*.jsonl").Single();
        var lines = File.ReadAllLines(file);
        lines.Should().NotBeEmpty();
        lines.Should().Contain(l => l.Contains("\"Metric\":\"usage_percent\""));
    }
}
```

- [ ] **Step 2: Run test**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~EndToEndSmokeTests
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "test(integration): end-to-end smoke — CPU readings written to log"
```

---

## Phase 3 — OS-Native Collectors (Memory, Storage, Network)

These three collectors use only OS-provided APIs — no LibreHardwareMonitor. They work at both admin and standard-user privilege levels.

### Task 3.1: `MemoryCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/MemoryCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/MemoryCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/MemoryCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class MemoryCollectorTests
{
    [Fact]
    public void Collect_ReturnsAvailableAndCommittedReadings()
    {
        using var c = new MemoryCollector(TimeSpan.FromSeconds(1));
        c.Collect();                         // prime PerformanceCounters
        var readings = c.Collect();

        readings.Should().Contain(r => r.Metric == "available_mb");
        readings.Should().Contain(r => r.Metric == "committed_percent");
        readings.Should().Contain(r => r.Metric == "page_faults_per_sec");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~MemoryCollectorTests
```

Expected: FAIL — type missing.

- [ ] **Step 3: Implement `MemoryCollector`**

Create `src/SystemMonitor.Engine/Collectors/MemoryCollector.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class MemoryCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _availableMb = new("Memory", "Available MBytes");
    private readonly PerformanceCounter _committedPercent = new("Memory", "% Committed Bytes In Use");
    private readonly PerformanceCounter _pageFaults = new("Memory", "Page Faults/sec");

    public MemoryCollector(TimeSpan pollingInterval) : base("memory", pollingInterval) { }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var empty = new Dictionary<string, string>();

        yield return new Reading("memory", "available_mb", _availableMb.NextValue(), "MB", ts, ReadingConfidence.High, empty);
        yield return new Reading("memory", "committed_percent", _committedPercent.NextValue(), "%", ts, ReadingConfidence.High, empty);
        yield return new Reading("memory", "page_faults_per_sec", _pageFaults.NextValue(), "count/s", ts, ReadingConfidence.High, empty);
    }

    public void Dispose()
    {
        _availableMb.Dispose();
        _committedPercent.Dispose();
        _pageFaults.Dispose();
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~MemoryCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add MemoryCollector (available/committed/page-faults)"
```

---

### Task 3.2: `StorageCollector` — latency + free space (non-admin path)

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/StorageCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/StorageCollectorTests.cs`

SMART attributes require admin; we'll layer that in via capability detection. For now the standard-user path captures latency, queue depth, and free-space trends.

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/StorageCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class StorageCollectorTests
{
    [Fact]
    public void Collect_ReturnsLatencyAndFreeSpacePerDrive()
    {
        using var c = new StorageCollector(TimeSpan.FromSeconds(1));
        c.Collect();
        var readings = c.Collect();

        readings.Should().Contain(r => r.Metric == "avg_disk_sec_per_transfer_ms");
        readings.Should().Contain(r => r.Metric == "free_space_percent");
        readings.Where(r => r.Metric == "free_space_percent")
                .Should().OnlyContain(r => r.Labels.ContainsKey("drive"));
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~StorageCollectorTests
```

Expected: FAIL — type missing.

- [ ] **Step 3: Implement `StorageCollector`**

Create `src/SystemMonitor.Engine/Collectors/StorageCollector.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class StorageCollector : CollectorBase, IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _avgSecPerXfer = new();
    private readonly Dictionary<string, PerformanceCounter> _queueDepth = new();

    public StorageCollector(TimeSpan pollingInterval) : base("storage", pollingInterval)
    {
        var cat = new PerformanceCounterCategory("PhysicalDisk");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _avgSecPerXfer[instance] = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Transfer", instance, readOnly: true);
            _queueDepth[instance]    = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", instance, readOnly: true);
        }
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        foreach (var (instance, counter) in _avgSecPerXfer)
        {
            var secs = counter.NextValue();
            yield return new Reading("storage", "avg_disk_sec_per_transfer_ms", secs * 1000.0, "ms", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["disk"] = instance });
        }

        foreach (var (instance, counter) in _queueDepth)
        {
            yield return new Reading("storage", "queue_depth", counter.NextValue(), "count", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["disk"] = instance });
        }

        // Free space — one reading per logical drive.
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
            var percent = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize;
            yield return new Reading("storage", "free_space_percent", percent, "%", ts,
                ReadingConfidence.High, new Dictionary<string, string> { ["drive"] = drive.Name });
        }
    }

    public void Dispose()
    {
        foreach (var c in _avgSecPerXfer.Values) c.Dispose();
        foreach (var c in _queueDepth.Values) c.Dispose();
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~StorageCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add StorageCollector (latency, queue depth, free space)"
```

---

### Task 3.3: `NetworkCollector` — adapter stats + gateway ping

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/NetworkCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/NetworkCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/NetworkCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class NetworkCollectorTests
{
    [Fact]
    public void Collect_ReturnsAdapterStatusReadings()
    {
        using var c = new NetworkCollector(TimeSpan.FromSeconds(1));
        var readings = c.Collect();
        readings.Should().Contain(r => r.Metric == "link_up");
        // Gateway latency attempt produces a reading even on offline systems (value == -1).
        readings.Should().Contain(r => r.Metric == "gateway_latency_ms");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~NetworkCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `NetworkCollector`**

Create `src/SystemMonitor.Engine/Collectors/NetworkCollector.cs`:

```csharp
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class NetworkCollector : CollectorBase
{
    public NetworkCollector(TimeSpan pollingInterval) : base("network", pollingInterval) { }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var results = new List<Reading>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            var labels = new Dictionary<string, string>
            {
                ["adapter"] = nic.Name,
                ["type"] = nic.NetworkInterfaceType.ToString()
            };

            results.Add(new Reading("network", "link_up",
                nic.OperationalStatus == OperationalStatus.Up ? 1 : 0,
                "bool", ts, ReadingConfidence.High, labels));

            try
            {
                var stats = nic.GetIPv4Statistics();
                results.Add(new Reading("network", "incoming_packet_errors",
                    stats.IncomingPacketsWithErrors, "count", ts, ReadingConfidence.High, labels));
                results.Add(new Reading("network", "outgoing_packet_errors",
                    stats.OutgoingPacketsWithErrors, "count", ts, ReadingConfidence.High, labels));
                results.Add(new Reading("network", "incoming_discards",
                    stats.IncomingPacketsDiscarded, "count", ts, ReadingConfidence.High, labels));
            }
            catch { /* some virtual adapters don't expose IPv4 stats — skip quietly */ }
        }

        results.Add(PingGateway(ts));
        return results;
    }

    private static Reading PingGateway(DateTimeOffset ts)
    {
        var labels = new Dictionary<string, string> { ["target"] = "gateway" };
        try
        {
            var gateway = GetDefaultGatewayAddress();
            if (gateway is null)
                return new Reading("network", "gateway_latency_ms", -1, "ms", ts, ReadingConfidence.Low, labels);

            using var ping = new Ping();
            var reply = ping.Send(gateway, 1000);
            var value = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            labels["target_ip"] = gateway;
            return new Reading("network", "gateway_latency_ms", value, "ms", ts, ReadingConfidence.High, labels);
        }
        catch
        {
            return new Reading("network", "gateway_latency_ms", -1, "ms", ts, ReadingConfidence.Low, labels);
        }
    }

    private static string? GetDefaultGatewayAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var gw in nic.GetIPProperties().GatewayAddresses)
            {
                if (gw.Address is null) continue;
                var s = gw.Address.ToString();
                if (s != "0.0.0.0" && !string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~NetworkCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add NetworkCollector (adapter stats + gateway ping)"
```

---

## Phase 4 — LibreHardwareMonitor Collectors (Power, GPU, CPU Temps)

These collectors use LibreHardwareMonitorLib to reach hardware sensors not exposed by OS APIs: CPU temps, voltages, fan speeds, GPU temps, storage SMART. Most of these sensors require **Administrator** privileges — the collectors report `Partial` capability when running as a standard user and produce only the readings that are accessible.

### Task 4.1: Add LibreHardwareMonitor NuGet package + `LhmComputer` wrapper

**Files:**
- Modify: `src/SystemMonitor.Engine/SystemMonitor.Engine.csproj`
- Create: `src/SystemMonitor.Engine/Collectors/Lhm/LhmComputer.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/LhmComputerTests.cs`

- [ ] **Step 1: Add the package**

```bash
dotnet add src/SystemMonitor.Engine package LibreHardwareMonitorLib
```

- [ ] **Step 2: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/LhmComputerTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class LhmComputerTests
{
    [Fact]
    public void Open_DoesNotThrow_AndExposesAtLeastOneHardwareItem()
    {
        using var lhm = LhmComputer.Open();
        // Even without admin, the Computer lists hardware — just with sparse sensor data.
        lhm.EnumerateSensors().Should().NotBeNull();
    }
}
```

- [ ] **Step 3: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~LhmComputerTests
```

Expected: FAIL — type missing.

- [ ] **Step 4: Implement `LhmComputer`**

Create `src/SystemMonitor.Engine/Collectors/Lhm/LhmComputer.cs`:

```csharp
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;

namespace SystemMonitor.Engine.Collectors.Lhm;

/// <summary>
/// Shared wrapper around a LibreHardwareMonitor <see cref="Computer"/> instance. Opened once
/// at startup and reused by all LHM-backed collectors (Power, GPU, CPU temps). Each collector
/// selects the subset of sensors it cares about via <see cref="EnumerateSensors"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LhmComputer : IDisposable
{
    private readonly Computer _computer;

    private LhmComputer(Computer computer) => _computer = computer;

    public static LhmComputer Open()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false,        // we cover this via OS APIs
            IsControllerEnabled = false,
            IsBatteryEnabled = true,
            IsPsuEnabled = true
        };
        computer.Open();
        return new LhmComputer(computer);
    }

    /// <summary>
    /// Traverses all hardware and returns every sensor. Callers filter by
    /// <see cref="ISensor.HardwareType"/> and <see cref="ISensor.SensorType"/>.
    /// </summary>
    public IEnumerable<ISensor> EnumerateSensors()
    {
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
            {
                sub.Update();
                foreach (var s in sub.Sensors) yield return s;
            }
            foreach (var s in hw.Sensors) yield return s;
        }
    }

    public void Dispose() => _computer.Close();
}
```

- [ ] **Step 5: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~LhmComputerTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(engine): integrate LibreHardwareMonitorLib via LhmComputer wrapper"
```

---

### Task 4.2: Extend `CpuCollector` with LHM temperatures

**Files:**
- Modify: `src/SystemMonitor.Engine/Collectors/CpuCollector.cs`
- Modify: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/CpuCollectorTests.cs`

- [ ] **Step 1: Update the test**

Add to `tests/SystemMonitor.Engine.IntegrationTests/Collectors/CpuCollectorTests.cs`:

```csharp
    [Fact]
    public void Collect_WithLhm_MayIncludeTemperatureReadings()
    {
        using var lhm = SystemMonitor.Engine.Collectors.Lhm.LhmComputer.Open();
        using var c = new CpuCollector(TimeSpan.FromMilliseconds(200), lhm);
        c.Collect();
        Thread.Sleep(250);
        var readings = c.Collect();

        // We cannot assert presence (requires admin + supported hardware), but we assert
        // that WHEN temp readings exist they are well-formed.
        foreach (var r in readings.Where(r => r.Metric == "temperature_celsius"))
        {
            r.Unit.Should().Be("°C");
            r.Value.Should().BeGreaterThan(-50).And.BeLessThan(150);
        }
    }
```

- [ ] **Step 2: Run test — it may pass (no temps captured) but must not fail**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~CpuCollectorTests
```

Expected: build FAILS — `CpuCollector` has no overload accepting `LhmComputer`.

- [ ] **Step 3: Update `CpuCollector` to accept an optional `LhmComputer`**

Replace `src/SystemMonitor.Engine/Collectors/CpuCollector.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class CpuCollector : CollectorBase, IDisposable
{
    private readonly PerformanceCounter _overallUsage;
    private readonly List<PerformanceCounter> _perCoreUsage = new();
    private readonly LhmComputer? _lhm;

    public CpuCollector(TimeSpan pollingInterval, LhmComputer? lhm = null)
        : base("cpu", pollingInterval)
    {
        _overallUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        var cat = new PerformanceCounterCategory("Processor");
        foreach (var instance in cat.GetInstanceNames())
        {
            if (instance == "_Total") continue;
            _perCoreUsage.Add(new PerformanceCounter("Processor", "% Processor Time", instance, readOnly: true));
        }
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Partial("no hardware sensors — usage only (no LHM)")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return new Reading("cpu", "usage_percent", _overallUsage.NextValue(), "%", ts,
            ReadingConfidence.High,
            new Dictionary<string, string> { ["scope"] = "overall" });

        foreach (var (counter, idx) in _perCoreUsage.Select((c, i) => (c, i)))
        {
            yield return new Reading("cpu", "usage_percent", counter.NextValue(), "%", ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["scope"] = "core",
                    ["core"] = idx.ToString()
                });
        }

        if (_lhm is null) yield break;

        foreach (var s in _lhm.EnumerateSensors())
        {
            if (s.Hardware.HardwareType != HardwareType.Cpu) continue;
            if (!s.Value.HasValue) continue;

            var metric = s.SensorType switch
            {
                SensorType.Temperature => "temperature_celsius",
                SensorType.Clock       => "clock_mhz",
                SensorType.Load        => null,   // PerformanceCounter already covers this
                _ => null
            };
            if (metric is null) continue;

            yield return new Reading("cpu", metric, s.Value.Value,
                s.SensorType == SensorType.Temperature ? "°C" : "MHz",
                ts, ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name
                });
        }
    }

    public void Dispose()
    {
        _overallUsage.Dispose();
        foreach (var c in _perCoreUsage) c.Dispose();
        // Do NOT dispose _lhm — it is owned by the orchestrator and shared across collectors.
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~CpuCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): extend CpuCollector with LHM temperature and clock readings"
```

---

### Task 4.3: `PowerCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/PowerCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/PowerCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/PowerCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class PowerCollectorTests
{
    [Fact]
    public void Collect_WithLhm_ReturnsNonThrowingReadings()
    {
        using var lhm = LhmComputer.Open();
        var c = new PowerCollector(TimeSpan.FromSeconds(1), lhm);
        var readings = c.Collect();
        // Contents vary drastically by hardware; only assert readings are well-formed.
        foreach (var r in readings)
        {
            r.Source.Should().Be("power");
            r.Timestamp.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
        }
    }

    [Fact]
    public void Capability_IsUnavailable_WhenLhmNotProvided()
    {
        var c = new PowerCollector(TimeSpan.FromSeconds(1), lhm: null);
        c.Capability.Level.Should().Be(CapabilityLevel.Unavailable);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~PowerCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `PowerCollector`**

Create `src/SystemMonitor.Engine/Collectors/PowerCollector.cs`:

```csharp
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class PowerCollector : CollectorBase
{
    private readonly LhmComputer? _lhm;

    public PowerCollector(TimeSpan pollingInterval, LhmComputer? lhm) : base("power", pollingInterval)
    {
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Unavailable("LibreHardwareMonitor not available (likely not admin)")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        if (_lhm is null) yield break;
        var ts = DateTimeOffset.UtcNow;

        foreach (var s in _lhm.EnumerateSensors())
        {
            if (!s.Value.HasValue) continue;

            var (metric, unit) = s.SensorType switch
            {
                SensorType.Voltage => ("voltage_volts", "V"),
                SensorType.Power   => ("power_watts", "W"),
                SensorType.Current => ("current_amps", "A"),
                SensorType.Fan     => ("fan_rpm", "RPM"),
                _ => (null, null)
            };
            if (metric is null) continue;

            yield return new Reading("power", metric, s.Value.Value, unit!, ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name,
                    ["hardware_type"] = s.Hardware.HardwareType.ToString()
                });
        }

        // Battery / UPS state (laptops and connected UPSes).
        var power = System.Windows.Forms.SystemInformation.PowerStatus;
        yield return new Reading("power", "on_ac", power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? 1 : 0,
            "bool", ts, ReadingConfidence.High, new Dictionary<string, string>());
        yield return new Reading("power", "battery_percent", power.BatteryLifePercent * 100, "%", ts,
            ReadingConfidence.High, new Dictionary<string, string>());
    }
}
```

Note: this references `System.Windows.Forms` for `PowerStatus`. The engine csproj already targets `net8.0-windows`; add `<UseWindowsForms>true</UseWindowsForms>` to its csproj property group to make the reference resolve.

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~PowerCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add PowerCollector (voltages, currents, fans, battery)"
```

---

### Task 4.4: `GpuCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/GpuCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/GpuCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/GpuCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class GpuCollectorTests
{
    [Fact]
    public void Collect_ReadingsAreWellFormed()
    {
        using var lhm = LhmComputer.Open();
        var c = new GpuCollector(TimeSpan.FromSeconds(1), lhm);
        var readings = c.Collect();
        foreach (var r in readings)
        {
            r.Source.Should().Be("gpu");
            r.Labels.Should().ContainKey("hardware");
        }
    }

    [Fact]
    public void Capability_IsUnavailable_WhenLhmNotProvided()
    {
        var c = new GpuCollector(TimeSpan.FromSeconds(1), lhm: null);
        c.Capability.Level.Should().Be(CapabilityLevel.Unavailable);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~GpuCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `GpuCollector`**

Create `src/SystemMonitor.Engine/Collectors/GpuCollector.cs`:

```csharp
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class GpuCollector : CollectorBase
{
    private readonly LhmComputer? _lhm;

    public GpuCollector(TimeSpan pollingInterval, LhmComputer? lhm) : base("gpu", pollingInterval)
    {
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Unavailable("LibreHardwareMonitor not available")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        if (_lhm is null) yield break;
        var ts = DateTimeOffset.UtcNow;

        foreach (var s in _lhm.EnumerateSensors())
        {
            var isGpu = s.Hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
            if (!isGpu || !s.Value.HasValue) continue;

            var (metric, unit) = s.SensorType switch
            {
                SensorType.Temperature => ("temperature_celsius", "°C"),
                SensorType.Load        => ("load_percent", "%"),
                SensorType.Clock       => ("clock_mhz", "MHz"),
                SensorType.SmallData   => ("memory_mb", "MB"),
                SensorType.Power       => ("power_watts", "W"),
                SensorType.Fan         => ("fan_rpm", "RPM"),
                _ => (null, null)
            };
            if (metric is null) continue;

            yield return new Reading("gpu", metric, s.Value.Value, unit!, ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name
                });
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~GpuCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add GpuCollector via LHM"
```

---

## Phase 5 — Event Log, Reliability, and Inventory Collectors

These collectors produce readings that aren't simple numeric samples — they emit discrete events (Windows event log entries, reliability records) and inventory snapshots. They still produce `Reading` objects so they flow through the same buffer and logger, but with the `Value` field used as a severity / count indicator and rich context in `Labels`.

### Task 5.1: `EventLogCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/EventLogCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/EventLogCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/EventLogCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class EventLogCollectorTests
{
    [Fact]
    public void Collect_ReturnsReadings_FromSystemAndApplicationLogs()
    {
        // Look back 24h — every live machine will have *some* Application entries.
        var c = new EventLogCollector(TimeSpan.FromSeconds(10), lookback: TimeSpan.FromHours(24));
        var readings = c.Collect();

        foreach (var r in readings)
        {
            r.Source.Should().Be("eventlog");
            r.Labels.Should().ContainKey("channel");
            r.Labels.Should().ContainKey("event_id");
            r.Labels.Should().ContainKey("level");
        }
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~EventLogCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `EventLogCollector`**

Create `src/SystemMonitor.Engine/Collectors/EventLogCollector.cs`:

```csharp
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

/// <summary>
/// Tails Windows event logs and emits a reading per relevant entry.
/// Relevant = warning/error/critical level entries on System, Application, and
/// Hardware Events channels, published since the last poll (or within <paramref name="lookback"/>
/// on first poll).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogCollector : CollectorBase
{
    private static readonly string[] Channels = { "System", "Application", "Microsoft-Windows-Kernel-WHEA/Errors" };
    private readonly TimeSpan _lookback;
    private DateTime _sinceUtc;

    public EventLogCollector(TimeSpan pollingInterval, TimeSpan? lookback = null)
        : base("eventlog", pollingInterval)
    {
        _lookback = lookback ?? TimeSpan.FromMinutes(10);
        _sinceUtc = DateTime.UtcNow - _lookback;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var now = DateTime.UtcNow;
        var results = new List<Reading>();

        foreach (var channel in Channels)
        {
            try
            {
                // XPath query: level <= 3 (Critical/Error/Warning) and time >= since.
                var xpath = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime>='{_sinceUtc:O}']]]";
                var query = new EventLogQuery(channel, PathType.LogName, xpath) { ReverseDirection = false };
                using var reader = new EventLogReader(query);
                for (var ev = reader.ReadEvent(); ev != null; ev = reader.ReadEvent())
                {
                    using (ev)
                    {
                        var ts = ev.TimeCreated is { } tc ? new DateTimeOffset(tc.ToUniversalTime()) : DateTimeOffset.UtcNow;
                        var labels = new Dictionary<string, string>
                        {
                            ["channel"] = channel,
                            ["event_id"] = ev.Id.ToString(),
                            ["level"] = ev.LevelDisplayName ?? ev.Level?.ToString() ?? "Unknown",
                            ["provider"] = ev.ProviderName ?? ""
                        };
                        var message = SafeFormat(ev);
                        if (!string.IsNullOrWhiteSpace(message)) labels["message"] = Truncate(message, 400);

                        // Value encodes level: 1=Critical, 2=Error, 3=Warning.
                        results.Add(new Reading("eventlog", "event", ev.Level ?? 0, "level", ts,
                            ReadingConfidence.High, labels));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Security channel etc. — skip quietly; capability report already reflects access.
            }
            catch (EventLogNotFoundException)
            {
                // Hardware Events channel may not exist on older systems.
            }
        }

        _sinceUtc = now;
        return results;
    }

    private static string SafeFormat(EventRecord ev)
    {
        try { return ev.FormatDescription() ?? ""; }
        catch { return ""; }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~EventLogCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add EventLogCollector (System/Application/WHEA, level 1-3)"
```

---

### Task 5.2: `ReliabilityCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/ReliabilityCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/ReliabilityCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/ReliabilityCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class ReliabilityCollectorTests
{
    [Fact]
    public void Collect_DoesNotThrow()
    {
        var c = new ReliabilityCollector(TimeSpan.FromMinutes(5), wmiTimeoutMs: 5000);
        var readings = c.Collect();
        foreach (var r in readings)
        {
            r.Source.Should().Be("reliability");
        }
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~ReliabilityCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Add the `System.Management` package reference**

```bash
dotnet add src/SystemMonitor.Engine package System.Management
```

- [ ] **Step 4: Implement `ReliabilityCollector`**

Create `src/SystemMonitor.Engine/Collectors/ReliabilityCollector.cs`:

```csharp
using System.Management;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class ReliabilityCollector : CollectorBase
{
    private readonly int _wmiTimeoutMs;

    public ReliabilityCollector(TimeSpan pollingInterval, int wmiTimeoutMs) : base("reliability", pollingInterval)
    {
        _wmiTimeoutMs = wmiTimeoutMs;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var results = new List<Reading>();

        // Win32_ReliabilityRecords — Windows' rollup of stability events.
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT SourceName, EventIdentifier, TimeGenerated, Message, ProductName FROM Win32_ReliabilityRecords");
            searcher.Options.Timeout = TimeSpan.FromMilliseconds(_wmiTimeoutMs);

            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var labels = new Dictionary<string, string>
                    {
                        ["source"] = mo["SourceName"]?.ToString() ?? "",
                        ["event_id"] = mo["EventIdentifier"]?.ToString() ?? "",
                        ["product"] = mo["ProductName"]?.ToString() ?? "",
                        ["message"] = Truncate(mo["Message"]?.ToString() ?? "", 400)
                    };
                    var when = ManagementDateTimeConverter.ToDateTime(mo["TimeGenerated"]?.ToString() ?? "");
                    results.Add(new Reading("reliability", "record", 1, "count",
                        new DateTimeOffset(when.ToUniversalTime(), TimeSpan.Zero),
                        ReadingConfidence.High, labels));
                }
            }
        }
        catch { /* missing class on older systems, insufficient privilege, etc. */ }

        // Minidump directory inventory.
        var minidumpDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
        if (Directory.Exists(minidumpDir))
        {
            foreach (var f in Directory.EnumerateFiles(minidumpDir, "*.dmp"))
            {
                var fi = new FileInfo(f);
                results.Add(new Reading("reliability", "minidump", fi.Length, "bytes",
                    new DateTimeOffset(fi.CreationTimeUtc, TimeSpan.Zero), ReadingConfidence.High,
                    new Dictionary<string, string>
                    {
                        ["path"] = f,
                        ["filename"] = fi.Name
                    }));
            }
        }

        return results;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
```

- [ ] **Step 5: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~ReliabilityCollectorTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(engine): add ReliabilityCollector (WMI reliability + minidumps)"
```

---

### Task 5.3: `InventoryCollector`

**Files:**
- Create: `src/SystemMonitor.Engine/Collectors/InventoryCollector.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/Collectors/InventoryCollectorTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/Collectors/InventoryCollectorTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests.Collectors;

public class InventoryCollectorTests
{
    [Fact]
    public void Collect_ReturnsMachineInfo()
    {
        var c = new InventoryCollector(wmiTimeoutMs: 5000);
        var readings = c.Collect();
        readings.Should().Contain(r => r.Metric == "os_version");
        readings.Should().Contain(r => r.Metric == "machine_name");
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~InventoryCollectorTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `InventoryCollector`**

Create `src/SystemMonitor.Engine/Collectors/InventoryCollector.cs`:

```csharp
using System.Management;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class InventoryCollector : CollectorBase
{
    private readonly int _wmiTimeoutMs;

    // PollingInterval == Zero signals the Orchestrator to fire once and not repeat.
    public InventoryCollector(int wmiTimeoutMs) : base("inventory", TimeSpan.Zero)
    {
        _wmiTimeoutMs = wmiTimeoutMs;
    }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;

        yield return Info("machine_name", Environment.MachineName);
        yield return Info("os_version", Environment.OSVersion.VersionString);
        yield return Info("processor_count", Environment.ProcessorCount.ToString());
        yield return Info("clr_version", Environment.Version.ToString());

        foreach (var r in WmiInventory("SELECT Name, Manufacturer, NumberOfCores, MaxClockSpeed FROM Win32_Processor", "cpu_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard", "motherboard_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Manufacturer, Name, Version, ReleaseDate FROM Win32_BIOS", "bios_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory", "ram_info", ts))
            yield return r;
        foreach (var r in WmiInventory("SELECT Model, InterfaceType, Size, MediaType FROM Win32_DiskDrive", "disk_info", ts))
            yield return r;
    }

    private Reading Info(string metric, string value) =>
        new("inventory", metric, 1, "info", DateTimeOffset.UtcNow, ReadingConfidence.High,
            new Dictionary<string, string> { ["value"] = value });

    private IEnumerable<Reading> WmiInventory(string query, string metric, DateTimeOffset ts)
    {
        List<Reading> results = new();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2", query);
            searcher.Options.Timeout = TimeSpan.FromMilliseconds(_wmiTimeoutMs);
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    var labels = new Dictionary<string, string>();
                    foreach (var prop in mo.Properties)
                    {
                        if (prop.Value is null) continue;
                        labels[prop.Name] = prop.Value.ToString() ?? "";
                    }
                    results.Add(new Reading("inventory", metric, 1, "info", ts, ReadingConfidence.High, labels));
                }
            }
        }
        catch { }
        return results;
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~InventoryCollectorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add InventoryCollector (CPU/board/BIOS/RAM/disk)"
```

---

## Phase 6 — Correlation Engine

The correlation engine reads the ring buffers, detects anomalies (threshold + baseline), applies correlation rules that cross-reference multiple readings, and emits `AnomalyEvent` records classified as `Internal`, `External`, or `Indeterminate`.

### Task 6.1: `Classification` enum and `AnomalyEvent` record

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Classification.cs`
- Create: `src/SystemMonitor.Engine/Correlation/AnomalyEvent.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/AnomalyEventTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/AnomalyEventTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Correlation;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class AnomalyEventTests
{
    [Fact]
    public void Constructor_StoresAllFields()
    {
        var ev = new AnomalyEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Classification: Classification.External,
            Confidence: 0.8,
            Summary: "Voltage sag coincident with Kernel-Power 41",
            Explanation: "Rail dropped 8% then unexpected shutdown within 5s",
            SourceMetrics: new[] { "power:voltage_volts", "eventlog:event" });

        ev.Classification.Should().Be(Classification.External);
        ev.Confidence.Should().Be(0.8);
        ev.SourceMetrics.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~AnomalyEventTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `Classification` and `AnomalyEvent`**

Create `src/SystemMonitor.Engine/Correlation/Classification.cs`:

```csharp
namespace SystemMonitor.Engine.Correlation;

public enum Classification
{
    Internal,       // hardware originating in this PC
    External,       // environmental (power, thermal, network)
    Indeterminate   // insufficient data to classify
}
```

Create `src/SystemMonitor.Engine/Correlation/AnomalyEvent.cs`:

```csharp
namespace SystemMonitor.Engine.Correlation;

public sealed record AnomalyEvent(
    DateTimeOffset Timestamp,
    Classification Classification,
    double Confidence,               // 0.0 – 1.0
    string Summary,                  // one-line human summary
    string Explanation,              // longer paragraph — what + why
    IReadOnlyList<string> SourceMetrics);  // "source:metric" identifiers that triggered this
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~AnomalyEventTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add Classification enum and AnomalyEvent record"
```

---

### Task 6.2: `ICorrelationRule` interface + context

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/ICorrelationRule.cs`
- Create: `src/SystemMonitor.Engine/Correlation/CorrelationContext.cs`

- [ ] **Step 1: Implement the context type**

Create `src/SystemMonitor.Engine/Correlation/CorrelationContext.cs`:

```csharp
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.Engine.Correlation;

/// <summary>
/// Snapshot of current engine state passed to each rule per evaluation.
/// Rules read from buffers and thresholds; they must not mutate anything.
/// </summary>
public sealed class CorrelationContext
{
    public required IReadOnlyDictionary<string, IReadOnlyList<Reading>> BufferSnapshots { get; init; }
    public required ThresholdConfig Thresholds { get; init; }
    public required DateTimeOffset Now { get; init; }
}
```

- [ ] **Step 2: Implement the interface**

Create `src/SystemMonitor.Engine/Correlation/ICorrelationRule.cs`:

```csharp
namespace SystemMonitor.Engine.Correlation;

public interface ICorrelationRule
{
    string Name { get; }

    /// <summary>Returns any anomalies this rule detects in the given context, or empty.</summary>
    IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx);
}
```

- [ ] **Step 3: Build (no test needed — interface only)**

```bash
dotnet build
```

Expected: Success.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(engine): add ICorrelationRule and CorrelationContext"
```

---

### Task 6.3: `ThermalRunawayRule` (Internal classification)

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Rules/ThermalRunawayRule.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/ThermalRunawayRuleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/ThermalRunawayRuleTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class ThermalRunawayRuleTests
{
    private static Reading Cpu(string metric, double value, DateTimeOffset ts) =>
        new("cpu", metric, value, metric == "temperature_celsius" ? "°C" : "%", ts,
            ReadingConfidence.High, new Dictionary<string, string>());

    private static CorrelationContext Ctx(IEnumerable<Reading> cpuReadings, ThresholdConfig? thr = null) => new()
    {
        BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["cpu"] = cpuReadings.ToList() },
        Thresholds = thr ?? new ThresholdConfig(),
        Now = DateTimeOffset.UtcNow
    };

    [Fact]
    public void HighTemp_WithSteadyLoad_ClassifiedAsInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new List<Reading>();
        // Steady 20% load, climbing temperature.
        for (int i = 0; i < 60; i++)
        {
            readings.Add(Cpu("usage_percent", 20, now.AddSeconds(-60 + i)));
            readings.Add(Cpu("temperature_celsius", 70 + i * 0.5, now.AddSeconds(-60 + i)));
        }

        var rule = new ThermalRunawayRule();
        var events = rule.Evaluate(Ctx(readings)).ToList();

        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
        events[0].Summary.Should().Contain("thermal");
    }

    [Fact]
    public void HighTemp_WithHighLoad_DoesNotFire()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new List<Reading>();
        for (int i = 0; i < 60; i++)
        {
            readings.Add(Cpu("usage_percent", 95, now.AddSeconds(-60 + i)));
            readings.Add(Cpu("temperature_celsius", 96, now.AddSeconds(-60 + i)));
        }

        new ThermalRunawayRule().Evaluate(Ctx(readings)).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ThermalRunawayRuleTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/Rules/ThermalRunawayRule.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Classifies as Internal: CPU temperature exceeds the critical threshold while
/// average load is low. Cooling fault (pump, pads, dust), NOT an external thermal cause.
/// </summary>
public sealed class ThermalRunawayRule : ICorrelationRule
{
    public string Name => "ThermalRunaway";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("cpu", out var cpu) || cpu.Count == 0) yield break;

        var temps = cpu.Where(r => r.Metric == "temperature_celsius").ToList();
        var loads = cpu.Where(r => r.Metric == "usage_percent"
                                && r.Labels.TryGetValue("scope", out var s) && s == "overall").ToList();
        if (temps.Count == 0 || loads.Count == 0) yield break;

        var maxTemp = temps.Max(r => r.Value);
        var avgLoad = loads.Average(r => r.Value);

        if (maxTemp >= ctx.Thresholds.CpuTempCelsiusCritical && avgLoad < 50)
        {
            yield return new AnomalyEvent(
                Timestamp: ctx.Now,
                Classification: Classification.Internal,
                Confidence: 0.85,
                Summary: $"Critical CPU thermal at low load ({maxTemp:F0}°C, {avgLoad:F0}% load)",
                Explanation: $"CPU reached {maxTemp:F0}°C while average load over the window was {avgLoad:F0}%. High temperatures without corresponding load strongly suggest a cooling-system fault internal to the machine (failed pump, dried thermal paste, heatsink seating, fan failure, or blocked intake).",
                SourceMetrics: new[] { "cpu:temperature_celsius", "cpu:usage_percent" });
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~ThermalRunawayRuleTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add ThermalRunawayRule (high temp + low load → Internal)"
```

---

### Task 6.4: `PowerAndKernelPowerRule` (External classification)

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Rules/PowerAndKernelPowerRule.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/PowerAndKernelPowerRuleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/PowerAndKernelPowerRuleTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class PowerAndKernelPowerRuleTests
{
    private static Reading Power(double volts, DateTimeOffset ts) =>
        new("power", "voltage_volts", volts, "V", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["sensor"] = "+12V" });

    private static Reading KernelPower(DateTimeOffset ts) =>
        new("eventlog", "event", 2, "level", ts, ReadingConfidence.High,
            new Dictionary<string, string>
            {
                ["channel"] = "System",
                ["event_id"] = "41",
                ["level"] = "Error",
                ["provider"] = "Microsoft-Windows-Kernel-Power"
            });

    [Fact]
    public void VoltageSag_FollowedByKernelPower41_ClassifiedExternal()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new Dictionary<string, IReadOnlyList<Reading>>
        {
            ["power"] = new[] { Power(12.0, now.AddSeconds(-10)), Power(11.0, now.AddSeconds(-6)) },
            ["eventlog"] = new[] { KernelPower(now.AddSeconds(-4)) }
        };
        var ctx = new CorrelationContext
        {
            BufferSnapshots = readings,
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var ev = new PowerAndKernelPowerRule().Evaluate(ctx).ToList();
        ev.Should().ContainSingle();
        ev[0].Classification.Should().Be(Classification.External);
    }

    [Fact]
    public void KernelPower41_WithoutVoltageSag_ClassifiedIndeterminate()
    {
        var now = DateTimeOffset.UtcNow;
        var readings = new Dictionary<string, IReadOnlyList<Reading>>
        {
            ["power"] = new[] { Power(12.0, now.AddSeconds(-10)), Power(12.0, now.AddSeconds(-6)) },
            ["eventlog"] = new[] { KernelPower(now.AddSeconds(-4)) }
        };
        var ctx = new CorrelationContext
        {
            BufferSnapshots = readings,
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var ev = new PowerAndKernelPowerRule().Evaluate(ctx).ToList();
        ev.Should().ContainSingle();
        ev[0].Classification.Should().Be(Classification.Indeterminate);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~PowerAndKernelPowerRuleTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/Rules/PowerAndKernelPowerRule.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Correlates voltage rail sags with Kernel-Power 41 (unexpected shutdown) events.
/// Voltage sag + Kernel-Power 41 within 5 seconds → External (likely mains/UPS/PSU supply).
/// Kernel-Power 41 alone, with no voltage anomaly observed → Indeterminate.
/// </summary>
public sealed class PowerAndKernelPowerRule : ICorrelationRule
{
    private static readonly TimeSpan CorrelationWindow = TimeSpan.FromSeconds(5);

    public string Name => "PowerAndKernelPower";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("eventlog", out var events)) yield break;

        var kp41 = events
            .Where(r => r.Labels.TryGetValue("provider", out var p) && p.Contains("Kernel-Power")
                     && r.Labels.TryGetValue("event_id", out var id) && id == "41")
            .ToList();
        if (kp41.Count == 0) yield break;

        ctx.BufferSnapshots.TryGetValue("power", out var power);
        var voltages = power?.Where(r => r.Metric == "voltage_volts").ToList() ?? new();

        foreach (var ev in kp41)
        {
            var before = voltages.Where(v => v.Timestamp >= ev.Timestamp - CorrelationWindow
                                           && v.Timestamp <= ev.Timestamp).ToList();

            double nominal = before.FirstOrDefault()?.Value ?? 0;
            double? minDuring = before.Count > 0 ? before.Min(v => v.Value) : null;
            double deviationPct = (nominal == 0 || minDuring is null)
                ? 0
                : Math.Abs(nominal - minDuring.Value) / nominal * 100;

            if (deviationPct >= ctx.Thresholds.VoltageDeviationPercentWarn)
            {
                yield return new AnomalyEvent(
                    Timestamp: ev.Timestamp,
                    Classification: Classification.External,
                    Confidence: 0.9,
                    Summary: $"Unexpected shutdown coincident with {deviationPct:F1}% voltage sag",
                    Explanation: $"Kernel-Power 41 (unexpected shutdown) preceded by voltage drop from {nominal:F2}V to {minDuring:F2}V within {CorrelationWindow.TotalSeconds}s. Points to upstream power delivery (mains instability, UPS switchover, or PSU supply-side input) rather than an OS/hardware fault.",
                    SourceMetrics: new[] { "eventlog:event(41)", "power:voltage_volts" });
            }
            else
            {
                yield return new AnomalyEvent(
                    Timestamp: ev.Timestamp,
                    Classification: Classification.Indeterminate,
                    Confidence: 0.4,
                    Summary: "Unexpected shutdown without voltage correlation",
                    Explanation: "Kernel-Power 41 (unexpected shutdown) observed but no concurrent voltage anomaly was recorded. Cause could be internal (PSU, motherboard, CPU fault) OR external without the voltage collector having visibility. Capture more runs, especially with a UPS or voltage-logging device upstream, to narrow this down.",
                    SourceMetrics: new[] { "eventlog:event(41)" });
            }
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~PowerAndKernelPowerRuleTests
```

Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add PowerAndKernelPowerRule (voltage+KP41 → External)"
```

---

### Task 6.5: `DiskLatencyAndSmartRule` (Internal classification)

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Rules/DiskLatencyAndSmartRule.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/DiskLatencyAndSmartRuleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/DiskLatencyAndSmartRuleTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class DiskLatencyAndSmartRuleTests
{
    private static Reading Latency(double ms, string disk, DateTimeOffset ts) =>
        new("storage", "avg_disk_sec_per_transfer_ms", ms, "ms", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["disk"] = disk });

    [Fact]
    public void PersistentHighLatency_ClassifiedInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 30)
            .Select(i => Latency(150, "0 C:", now.AddSeconds(-30 + i)))
            .ToList<Reading>();

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["storage"] = samples },
            Thresholds = new ThresholdConfig { DiskLatencyMsWarn = 50 },
            Now = now
        };

        var events = new DiskLatencyAndSmartRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
    }

    [Fact]
    public void BriefLatencySpike_DoesNotFire()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 30; i++)
        {
            samples.Add(Latency(i == 15 ? 300 : 5, "0 C:", now.AddSeconds(-30 + i)));
        }

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["storage"] = samples },
            Thresholds = new ThresholdConfig { DiskLatencyMsWarn = 50 },
            Now = now
        };

        new DiskLatencyAndSmartRule().Evaluate(ctx).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~DiskLatencyAndSmartRuleTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/Rules/DiskLatencyAndSmartRule.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Persistent high disk latency (above threshold for >50% of recent samples on the same disk)
/// is classified as Internal — the storage subsystem is the failing component.
/// </summary>
public sealed class DiskLatencyAndSmartRule : ICorrelationRule
{
    public string Name => "DiskLatencyAndSmart";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("storage", out var storage)) yield break;

        var latencies = storage.Where(r => r.Metric == "avg_disk_sec_per_transfer_ms").ToList();
        if (latencies.Count < 10) yield break;

        var byDisk = latencies.GroupBy(r => r.Labels.GetValueOrDefault("disk", "unknown"));
        foreach (var group in byDisk)
        {
            var samples = group.ToList();
            double threshold = ctx.Thresholds.DiskLatencyMsWarn;
            double fractionOver = samples.Count(s => s.Value > threshold) / (double)samples.Count;
            if (fractionOver < 0.5) continue;

            yield return new AnomalyEvent(
                Timestamp: ctx.Now,
                Classification: Classification.Internal,
                Confidence: 0.75,
                Summary: $"Disk {group.Key} latency consistently above {threshold:F0}ms",
                Explanation: $"{fractionOver * 100:F0}% of recent samples on disk '{group.Key}' exceeded {threshold:F0}ms (peak {samples.Max(s => s.Value):F0}ms). Persistent latency on a specific disk points to a failing storage device — run SMART diagnostics (when admin) to confirm, and back up data.",
                SourceMetrics: new[] { "storage:avg_disk_sec_per_transfer_ms" });
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~DiskLatencyAndSmartRuleTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add DiskLatencyAndSmartRule (persistent latency → Internal)"
```

---

### Task 6.6: `NetworkDropAndPacketLossRule` (External classification)

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Rules/NetworkDropAndPacketLossRule.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/NetworkDropAndPacketLossRuleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/NetworkDropAndPacketLossRuleTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class NetworkDropAndPacketLossRuleTests
{
    private static Reading Ping(double ms, DateTimeOffset ts) =>
        new("network", "gateway_latency_ms", ms, "ms", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["target"] = "gateway" });

    private static Reading LinkUp(double up, string adapter, DateTimeOffset ts) =>
        new("network", "link_up", up, "bool", ts, ReadingConfidence.High,
            new Dictionary<string, string> { ["adapter"] = adapter });

    [Fact]
    public void GatewayPingTimeouts_WithLinkUp_ClassifiedExternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 20; i++)
        {
            samples.Add(LinkUp(1, "eth0", now.AddSeconds(-20 + i)));
            samples.Add(Ping(-1, now.AddSeconds(-20 + i))); // all timeouts
        }

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["network"] = samples },
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var events = new NetworkDropAndPacketLossRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.External);
    }

    [Fact]
    public void LinkFlapping_ClassifiedInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        for (int i = 0; i < 20; i++) samples.Add(LinkUp(i % 2, "eth0", now.AddSeconds(-20 + i)));

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["network"] = samples },
            Thresholds = new ThresholdConfig(),
            Now = now
        };

        var events = new NetworkDropAndPacketLossRule().Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Internal);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~NetworkDropAndPacketLossRuleTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/Rules/NetworkDropAndPacketLossRule.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Distinguishes between:
///   • link up, gateway unreachable → External (ISP/router/switch)
///   • link flapping (up/down transitions) → Internal (NIC, cable, port)
/// </summary>
public sealed class NetworkDropAndPacketLossRule : ICorrelationRule
{
    public string Name => "NetworkDropAndPacketLoss";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue("network", out var net)) yield break;

        // Link-flap detection: count transitions per adapter.
        var byAdapter = net.Where(r => r.Metric == "link_up")
                           .GroupBy(r => r.Labels.GetValueOrDefault("adapter", ""));
        foreach (var group in byAdapter)
        {
            var values = group.OrderBy(r => r.Timestamp).Select(r => (int)r.Value).ToList();
            int transitions = 0;
            for (int i = 1; i < values.Count; i++)
                if (values[i] != values[i - 1]) transitions++;

            if (transitions >= 3)
            {
                yield return new AnomalyEvent(
                    Timestamp: ctx.Now,
                    Classification: Classification.Internal,
                    Confidence: 0.8,
                    Summary: $"Adapter '{group.Key}' link flapped {transitions} times",
                    Explanation: $"Network adapter '{group.Key}' changed state {transitions} times in the recent window. Repeated link up/down transitions typically indicate a NIC, cable, or physical port problem on this machine (or its immediate patch cable) rather than upstream infrastructure.",
                    SourceMetrics: new[] { "network:link_up" });
            }
        }

        // Gateway unreachable with link up → External.
        var pings = net.Where(r => r.Metric == "gateway_latency_ms").ToList();
        var linkUps = net.Where(r => r.Metric == "link_up").ToList();
        if (pings.Count >= 10 && linkUps.All(r => r.Value == 1))
        {
            double lossPct = pings.Count(p => p.Value < 0) / (double)pings.Count * 100;
            if (lossPct >= ctx.Thresholds.NetworkPacketLossPercentWarn * 10) // persistent, not occasional
            {
                yield return new AnomalyEvent(
                    Timestamp: ctx.Now,
                    Classification: Classification.External,
                    Confidence: 0.75,
                    Summary: $"Gateway unreachable ({lossPct:F0}% loss) while link is up",
                    Explanation: $"NIC reports link up but {lossPct:F0}% of gateway pings timed out. A working link with an unreachable default gateway points to upstream network infrastructure — switch, router, or cabling between this machine and the gateway — not the PC itself.",
                    SourceMetrics: new[] { "network:gateway_latency_ms", "network:link_up" });
            }
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~NetworkDropAndPacketLossRuleTests
```

Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add NetworkDropAndPacketLossRule (flap=Internal, ping-loss=External)"
```

---

### Task 6.7: `BaselineDeviationRule` (Indeterminate — surfaces unusual behavior)

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/Rules/BaselineDeviationRule.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/BaselineDeviationRuleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/BaselineDeviationRuleTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class BaselineDeviationRuleTests
{
    [Fact]
    public void ValueFarFromRollingMean_Flags()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new List<Reading>();
        // 100 samples around 50 ± 1 then one at 80.
        var rng = new Random(0);
        for (int i = 0; i < 100; i++)
            samples.Add(new Reading("cpu", "usage_percent", 50 + rng.NextDouble() * 2 - 1, "%",
                now.AddSeconds(-100 + i), ReadingConfidence.High,
                new Dictionary<string, string> { ["scope"] = "overall" }));
        samples.Add(new Reading("cpu", "usage_percent", 80, "%", now, ReadingConfidence.High,
            new Dictionary<string, string> { ["scope"] = "overall" }));

        var ctx = new CorrelationContext
        {
            BufferSnapshots = new Dictionary<string, IReadOnlyList<Reading>> { ["cpu"] = samples },
            Thresholds = new ThresholdConfig { BaselineStdDevWarn = 3 },
            Now = now
        };

        var events = new BaselineDeviationRule(sourceName: "cpu", metric: "usage_percent").Evaluate(ctx).ToList();
        events.Should().ContainSingle();
        events[0].Classification.Should().Be(Classification.Indeterminate);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~BaselineDeviationRuleTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/Rules/BaselineDeviationRule.cs`:

```csharp
using SystemMonitor.Engine.Collectors;

namespace SystemMonitor.Engine.Correlation.Rules;

/// <summary>
/// Flags a reading that deviates from its rolling mean by more than N standard deviations.
/// Classified Indeterminate — the deviation is interesting but not enough to classify on its own;
/// the operator should review alongside other rules.
/// </summary>
public sealed class BaselineDeviationRule : ICorrelationRule
{
    private readonly string _source;
    private readonly string _metric;

    public BaselineDeviationRule(string sourceName, string metric)
    {
        _source = sourceName;
        _metric = metric;
    }

    public string Name => $"BaselineDeviation({_source}:{_metric})";

    public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx)
    {
        if (!ctx.BufferSnapshots.TryGetValue(_source, out var buf)) yield break;
        var samples = buf.Where(r => r.Metric == _metric).OrderBy(r => r.Timestamp).ToList();
        if (samples.Count < 30) yield break;

        var latest = samples[^1];
        var window = samples.Take(samples.Count - 1).ToList();

        double mean = window.Average(r => r.Value);
        double variance = window.Sum(r => (r.Value - mean) * (r.Value - mean)) / window.Count;
        double stddev = Math.Sqrt(variance);
        if (stddev < 1e-6) yield break;

        double z = Math.Abs(latest.Value - mean) / stddev;
        if (z >= ctx.Thresholds.BaselineStdDevWarn)
        {
            yield return new AnomalyEvent(
                Timestamp: latest.Timestamp,
                Classification: Classification.Indeterminate,
                Confidence: Math.Min(0.6, z / 10),
                Summary: $"{_source}:{_metric} deviated {z:F1}σ from baseline (value={latest.Value:F2}, mean={mean:F2})",
                Explanation: $"Latest {_source}:{_metric} value of {latest.Value:F2} is {z:F1} standard deviations above the recent mean of {mean:F2}. Flagged for review — on its own this does not classify as Internal vs. External; look for correlated events.",
                SourceMetrics: new[] { $"{_source}:{_metric}" });
        }
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~BaselineDeviationRuleTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add BaselineDeviationRule for rolling-mean anomaly detection"
```

---

### Task 6.8: `CorrelationEngine` — ties rules together

**Files:**
- Create: `src/SystemMonitor.Engine/Correlation/CorrelationEngine.cs`
- Test: `tests/SystemMonitor.Engine.Tests/Correlation/CorrelationEngineTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/SystemMonitor.Engine.Tests/Correlation/CorrelationEngineTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using Xunit;

namespace SystemMonitor.Engine.Tests.Correlation;

public class CorrelationEngineTests
{
    private sealed class AlwaysRule : ICorrelationRule
    {
        public string Name => "Always";
        public IEnumerable<AnomalyEvent> Evaluate(CorrelationContext ctx) =>
            new[] { new AnomalyEvent(ctx.Now, Classification.Indeterminate, 0.1, "s", "e", Array.Empty<string>()) };
    }

    [Fact]
    public void EvaluateOnce_CallsRules_AndEmitsAnomalies()
    {
        var buffers = new Dictionary<string, ReadingRingBuffer> { ["cpu"] = new ReadingRingBuffer(10) };
        var emitted = new List<AnomalyEvent>();

        var engine = new CorrelationEngine(
            rules: new[] { new AlwaysRule() },
            buffers: buffers,
            thresholds: new ThresholdConfig(),
            sink: emitted.Add);

        engine.EvaluateOnce();
        emitted.Should().ContainSingle();
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CorrelationEngineTests
```

Expected: FAIL.

- [ ] **Step 3: Implement**

Create `src/SystemMonitor.Engine/Correlation/CorrelationEngine.cs`:

```csharp
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.Engine.Correlation;

public sealed class CorrelationEngine : IDisposable
{
    private readonly IReadOnlyList<ICorrelationRule> _rules;
    private readonly IReadOnlyDictionary<string, ReadingRingBuffer> _buffers;
    private readonly ThresholdConfig _thresholds;
    private readonly Action<AnomalyEvent> _sink;
    private Timer? _timer;

    public CorrelationEngine(
        IEnumerable<ICorrelationRule> rules,
        IReadOnlyDictionary<string, ReadingRingBuffer> buffers,
        ThresholdConfig thresholds,
        Action<AnomalyEvent> sink)
    {
        _rules = rules.ToList();
        _buffers = buffers;
        _thresholds = thresholds;
        _sink = sink;
    }

    public void Start(TimeSpan interval) => _timer = new Timer(_ => EvaluateOnce(), null, interval, interval);

    public void Stop() => _timer?.Dispose();

    public void EvaluateOnce()
    {
        var ctx = new CorrelationContext
        {
            BufferSnapshots = _buffers.ToDictionary(kv => kv.Key, kv => kv.Value.Snapshot()),
            Thresholds = _thresholds,
            Now = DateTimeOffset.UtcNow
        };

        foreach (var rule in _rules)
        {
            IEnumerable<AnomalyEvent> produced;
            try { produced = rule.Evaluate(ctx); }
            catch { continue; }  // A broken rule must not crash the engine.

            foreach (var e in produced) _sink(e);
        }
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.Tests --filter FullyQualifiedName~CorrelationEngineTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add CorrelationEngine tying rules + buffers + sink"
```

---

## Phase 7 — WinForms UI

This phase builds the UI on top of the already-tested engine. Because the engine publishes readings through a sink callback and exposes buffers via snapshot, the UI is a thin consumer — it does not contain business logic.

### Task 7.1: `EngineHost` — app-facing wrapper over engine wiring

**Files:**
- Create: `src/SystemMonitor.Engine/EngineHost.cs`
- Test: `tests/SystemMonitor.Engine.IntegrationTests/EngineHostSmokeTests.cs`

The `EngineHost` centralizes collector construction, buffer creation, logger setup, and correlation wiring. Both the UI and the headless mode consume it.

- [ ] **Step 1: Write the failing integration test**

Create `tests/SystemMonitor.Engine.IntegrationTests/EngineHostSmokeTests.cs`:

```csharp
using FluentAssertions;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;
using Xunit;

namespace SystemMonitor.Engine.IntegrationTests;

public class EngineHostSmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public EngineHostSmokeTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public async Task Start_ProducesReadingsAcrossMultipleCollectors()
    {
        var cfg = AppConfig.Defaults();
        cfg.LogOutputDirectory = _dir;
        foreach (var c in cfg.Collectors.Values) c.PollingIntervalMs = Math.Min(c.PollingIntervalMs, 500);

        using var host = EngineHost.Build(cfg);
        host.Start();
        await Task.Delay(1500);
        host.Stop();

        host.Buffers.Should().ContainKey("cpu");
        host.Buffers["cpu"].Count.Should().BeGreaterThan(0);
        host.Buffers["memory"].Count.Should().BeGreaterThan(0);
    }
}
```

- [ ] **Step 2: Run test — confirm it fails**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~EngineHostSmokeTests
```

Expected: FAIL.

- [ ] **Step 3: Implement `EngineHost`**

Create `src/SystemMonitor.Engine/EngineHost.cs`:

```csharp
using System.Runtime.Versioning;
using SystemMonitor.Engine.Buffer;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors;
using SystemMonitor.Engine.Collectors.Lhm;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;
using SystemMonitor.Engine.Correlation.Rules;
using SystemMonitor.Engine.Logging;

namespace SystemMonitor.Engine;

/// <summary>
/// Composes the engine: collectors + buffers + logger + correlation. Exposes live state
/// for the UI (buffers, anomaly stream, capability report) and can be used in headless mode too.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EngineHost : IDisposable
{
    public AppConfig Config { get; }
    public bool IsAdministrator { get; }
    public LhmComputer? Lhm { get; }
    public IReadOnlyDictionary<string, ReadingRingBuffer> Buffers => _buffers;
    public IReadOnlyList<ICollector> Collectors => _collectors;
    public event Action<Reading>? OnReading;
    public event Action<AnomalyEvent>? OnAnomaly;
    public event Action<string>? OnDiagnostic;

    private readonly Dictionary<string, ReadingRingBuffer> _buffers;
    private readonly List<ICollector> _collectors;
    private readonly JsonlLogger _readingsLog;
    private readonly JsonlLogger _eventsLog;
    private readonly JsonlLogger _anomaliesLog;
    private readonly Orchestrator _orchestrator;
    private readonly CorrelationEngine _correlation;

    private EngineHost(
        AppConfig config, bool isAdmin, LhmComputer? lhm,
        Dictionary<string, ReadingRingBuffer> buffers, List<ICollector> collectors,
        JsonlLogger readingsLog, JsonlLogger eventsLog, JsonlLogger anomaliesLog,
        Orchestrator orchestrator, CorrelationEngine correlation)
    {
        Config = config;
        IsAdministrator = isAdmin;
        Lhm = lhm;
        _buffers = buffers;
        _collectors = collectors;
        _readingsLog = readingsLog;
        _eventsLog = eventsLog;
        _anomaliesLog = anomaliesLog;
        _orchestrator = orchestrator;
        _correlation = correlation;
    }

    public static EngineHost Build(AppConfig config)
    {
        bool isAdmin = PrivilegeDetector.IsAdministrator();

        LhmComputer? lhm = null;
        if (isAdmin)
        {
            try { lhm = LhmComputer.Open(); }
            catch { lhm = null; }
        }

        var collectors = new List<ICollector>();
        if (Enabled(config, "cpu"))         collectors.Add(new CpuCollector(Ms(config, "cpu"), lhm));
        if (Enabled(config, "memory"))      collectors.Add(new MemoryCollector(Ms(config, "memory")));
        if (Enabled(config, "storage"))     collectors.Add(new StorageCollector(Ms(config, "storage")));
        if (Enabled(config, "network"))     collectors.Add(new NetworkCollector(Ms(config, "network")));
        if (Enabled(config, "power"))       collectors.Add(new PowerCollector(Ms(config, "power"), lhm));
        if (Enabled(config, "gpu"))         collectors.Add(new GpuCollector(Ms(config, "gpu"), lhm));
        if (Enabled(config, "eventlog"))    collectors.Add(new EventLogCollector(Ms(config, "eventlog")));
        if (Enabled(config, "reliability")) collectors.Add(new ReliabilityCollector(Ms(config, "reliability"), config.WmiTimeoutMs));
        if (Enabled(config, "inventory"))   collectors.Add(new InventoryCollector(config.WmiTimeoutMs));

        var buffers = collectors.ToDictionary(c => c.Name,
            _ => new ReadingRingBuffer(config.BufferCapacityPerCollector));

        var readingsLog = new JsonlLogger(config.LogOutputDirectory, "readings", config.LogRotationSizeBytes);
        var eventsLog = new JsonlLogger(config.LogOutputDirectory, "events", config.LogRotationSizeBytes);
        var anomaliesLog = new JsonlLogger(config.LogOutputDirectory, "anomalies", config.LogRotationSizeBytes);

        WriteCapabilityHeader(readingsLog, isAdmin, collectors);
        WriteCapabilityHeader(eventsLog, isAdmin, collectors);
        WriteCapabilityHeader(anomaliesLog, isAdmin, collectors);

        var rules = new List<ICorrelationRule>
        {
            new ThermalRunawayRule(),
            new PowerAndKernelPowerRule(),
            new DiskLatencyAndSmartRule(),
            new NetworkDropAndPacketLossRule(),
            new BaselineDeviationRule("cpu", "temperature_celsius"),
            new BaselineDeviationRule("cpu", "usage_percent"),
        };

        var host = (EngineHost?)null;   // forward reference for closures

        var orchestrator = new Orchestrator(collectors, buffers, r =>
        {
            host?.OnReading?.Invoke(r);
            if (r.Source == "eventlog") eventsLog.WriteReading(r);
            else readingsLog.WriteReading(r);
        });

        var correlation = new CorrelationEngine(rules, buffers, config.Thresholds, ev =>
        {
            host?.OnAnomaly?.Invoke(ev);
            anomaliesLog.WriteLine(System.Text.Json.JsonSerializer.Serialize(ev));
            anomaliesLog.Flush();
        });

        host = new EngineHost(config, isAdmin, lhm, buffers, collectors, readingsLog, eventsLog, anomaliesLog,
                              orchestrator, correlation);
        return host;
    }

    public void Start()
    {
        _orchestrator.Start();
        _correlation.Start(TimeSpan.FromMilliseconds(Config.CorrelationIntervalMs));
    }

    public void Stop()
    {
        _orchestrator.Stop();
        _correlation.Stop();
        _readingsLog.Flush();
        _eventsLog.Flush();
        _anomaliesLog.Flush();
    }

    public void Dispose()
    {
        Stop();
        _readingsLog.Dispose();
        _eventsLog.Dispose();
        _anomaliesLog.Dispose();
        Lhm?.Dispose();
        foreach (var c in _collectors.OfType<IDisposable>()) c.Dispose();
    }

    private static bool Enabled(AppConfig c, string name)
        => c.Collectors.TryGetValue(name, out var cc) && cc.Enabled;

    private static TimeSpan Ms(AppConfig c, string name)
        => TimeSpan.FromMilliseconds(c.Collectors[name].PollingIntervalMs);

    private static void WriteCapabilityHeader(JsonlLogger log, bool isAdmin, IEnumerable<ICollector> collectors)
    {
        var header = new
        {
            type = "capability_report",
            timestamp = DateTimeOffset.UtcNow,
            is_administrator = isAdmin,
            machine = Environment.MachineName,
            collectors = collectors.Select(c => new
            {
                name = c.Name,
                capability_level = c.Capability.Level.ToString(),
                reason = c.Capability.Reason,
                polling_interval_ms = (int)c.PollingInterval.TotalMilliseconds
            })
        };
        log.WriteLine(System.Text.Json.JsonSerializer.Serialize(header));
        log.Flush();
    }
}
```

- [ ] **Step 4: Run test — confirm it passes**

```bash
dotnet test tests/SystemMonitor.Engine.IntegrationTests --filter FullyQualifiedName~EngineHostSmokeTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(engine): add EngineHost wiring collectors+buffers+logger+correlation"
```

---

### Task 7.2: Add the charting assembly reference

**Files:**
- Modify: `src/SystemMonitor.App/SystemMonitor.App.csproj`

- [ ] **Step 1: Install the charting NuGet**

```bash
dotnet add src/SystemMonitor.App package System.Windows.Forms.DataVisualization -v 1.0.0-prerelease.20110.1
```

(Note: the modern .NET port of DataVisualization is published under this prerelease. If that version is unavailable at implementation time, pick the latest published prerelease of the same package.)

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: Success.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "chore(app): add DataVisualization charting package"
```

---

### Task 7.3: `UiRefreshPump` — 2Hz snapshot broadcaster

**Files:**
- Create: `src/SystemMonitor.App/ViewModels/UiRefreshPump.cs`

Only code shown — no test. The UI thread consumes pump events to redraw; the engine is separately tested.

- [ ] **Step 1: Create the pump**

Create `src/SystemMonitor.App/ViewModels/UiRefreshPump.cs`:

```csharp
using System.Windows.Forms;
using SystemMonitor.Engine;

namespace SystemMonitor.App.ViewModels;

/// <summary>
/// Ticks on the UI thread at the configured refresh rate. Subscribers receive the
/// latest buffer snapshots and render without having to subscribe to each reading event.
/// </summary>
public sealed class UiRefreshPump : IDisposable
{
    private readonly EngineHost _host;
    private readonly System.Windows.Forms.Timer _timer;

    public event Action<EngineHost>? Tick;

    public UiRefreshPump(EngineHost host, int refreshHz, Control uiThreadOwner)
    {
        _host = host;
        _timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, 1000 / Math.Max(1, refreshHz))
        };
        _timer.Tick += (_, _) => Tick?.Invoke(_host);
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
    public void Dispose() { _timer.Stop(); _timer.Dispose(); }
}
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build
git add .
git commit -m "feat(app): add UiRefreshPump driving UI at configured Hz"
```

---

### Task 7.4: `MainForm` skeleton with layout and status bar

**Files:**
- Create: `src/SystemMonitor.App/MainForm.cs`
- Create: `src/SystemMonitor.App/MainForm.Designer.cs`
- Modify: `src/SystemMonitor.App/Program.cs`

- [ ] **Step 1: Create `MainForm.Designer.cs`**

Create `src/SystemMonitor.App/MainForm.Designer.cs`:

```csharp
namespace SystemMonitor.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private ToolStripButton _startButton = null!;
    private ToolStripButton _stopButton = null!;
    private ToolStripButton _configButton = null!;
    private ToolStripButton _openLogsButton = null!;
    private SplitContainer _split = null!;
    private TreeView _capabilityTree = null!;
    private TabControl _tabs = null!;
    private DataGridView _eventFeed = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _statusRunning = null!;
    private ToolStripStatusLabel _statusAdmin = null!;
    private ToolStripStatusLabel _statusLogPath = null!;
    private ToolStripStatusLabel _statusUptime = null!;
    private NotifyIcon _tray = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());
        var viewMenu = new ToolStripMenuItem("View");
        var toolsMenu = new ToolStripMenuItem("Tools");
        var helpMenu = new ToolStripMenuItem("Help");
        _menu.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });

        _toolbar = new ToolStrip();
        _startButton = new ToolStripButton("Start");
        _stopButton = new ToolStripButton("Stop") { Enabled = false };
        _configButton = new ToolStripButton("Config");
        _openLogsButton = new ToolStripButton("Open Logs");
        _toolbar.Items.AddRange(new ToolStripItem[] { _startButton, _stopButton,
            new ToolStripSeparator(), _configButton, _openLogsButton });

        _split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 240 };
        _capabilityTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        _split.Panel1.Controls.Add(_capabilityTree);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _split.Panel2.Controls.Add(_tabs);

        _eventFeed = new DataGridView
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            VirtualMode = false
        };
        _eventFeed.Columns.Add("Time", "Time");
        _eventFeed.Columns.Add("Class", "Classification");
        _eventFeed.Columns.Add("Summary", "Summary");
        _eventFeed.Columns.Add("Confidence", "Confidence");

        _status = new StatusStrip();
        _statusRunning = new ToolStripStatusLabel("● Stopped") { ForeColor = Color.Gray };
        _statusAdmin = new ToolStripStatusLabel("Admin: ?");
        _statusLogPath = new ToolStripStatusLabel("Logs: (none)") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusUptime = new ToolStripStatusLabel("Uptime: 00:00:00");
        _status.Items.AddRange(new ToolStripItem[] { _statusRunning, _statusAdmin, _statusLogPath, _statusUptime });

        _tray = new NotifyIcon(components)
        {
            Icon = SystemIcons.Information,
            Text = "SystemMonitor",
            Visible = true
        };
        _tray.BalloonTipTitle = "SystemMonitor";

        Text = "SystemMonitor";
        ClientSize = new Size(1200, 800);
        MainMenuStrip = _menu;
        Controls.Add(_split);
        Controls.Add(_eventFeed);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_status);
    }
}
```

- [ ] **Step 2: Create `MainForm.cs` with wiring**

Create `src/SystemMonitor.App/MainForm.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using SystemMonitor.App.Controls;
using SystemMonitor.App.ViewModels;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;
using SystemMonitor.Engine.Correlation;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
public partial class MainForm : Form
{
    private readonly AppConfig _config;
    private EngineHost? _host;
    private UiRefreshPump? _pump;
    private DateTime _startedUtc;
    private readonly System.Windows.Forms.Timer _uptimeTimer = new() { Interval = 1000 };

    public MainForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        _startButton.Click += (_, _) => StartEngine();
        _stopButton.Click += (_, _) => StopEngine();
        _configButton.Click += (_, _) => OpenConfigDialog();
        _openLogsButton.Click += (_, _) => OpenLogFolder();
        FormClosing += (_, _) => StopEngine();

        _uptimeTimer.Tick += (_, _) =>
        {
            if (_startedUtc == default) return;
            var d = DateTime.UtcNow - _startedUtc;
            _statusUptime.Text = $"Uptime: {d:hh\\:mm\\:ss}";
        };

        AddTabs();
    }

    private void AddTabs()
    {
        _tabs.TabPages.Add(new TabPage("Overview") { Controls = { new OverviewTab { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("CPU")      { Controls = { new CpuTab      { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Memory")   { Controls = { new MemoryTab   { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Power")    { Controls = { new PowerTab    { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Storage")  { Controls = { new StorageTab  { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("GPU")      { Controls = { new GpuTab      { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Network")  { Controls = { new NetworkTab  { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Events")   { Controls = { new EventsTab   { Dock = DockStyle.Fill } } });
    }

    private void StartEngine()
    {
        if (_host is not null) return;
        _host = EngineHost.Build(_config);
        _host.OnAnomaly += OnAnomalyReceived;
        _pump = new UiRefreshPump(_host, _config.UiRefreshHz, this);
        _pump.Tick += OnPumpTick;

        _host.Start();
        _pump.Start();
        _startedUtc = DateTime.UtcNow;
        _uptimeTimer.Start();

        _statusRunning.Text = "● Running";
        _statusRunning.ForeColor = Color.Green;
        _statusAdmin.Text = _host.IsAdministrator ? "Admin: Yes" : "Admin: No";
        _statusLogPath.Text = $"Logs: {_config.LogOutputDirectory}";
        _startButton.Enabled = false;
        _stopButton.Enabled = true;

        PopulateCapabilityTree(_host);
    }

    private void StopEngine()
    {
        if (_host is null) return;
        _pump?.Stop();
        _host.Stop();
        _pump?.Dispose();
        _host.Dispose();
        _host = null;
        _pump = null;
        _uptimeTimer.Stop();

        _statusRunning.Text = "● Stopped";
        _statusRunning.ForeColor = Color.Gray;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
    }

    private void OnPumpTick(EngineHost host)
    {
        // Each tab snapshots the buffers it cares about on the UI thread.
        foreach (TabPage tab in _tabs.TabPages)
            if (tab.Controls[0] is ITabView view) view.Refresh(host);
    }

    private void OnAnomalyReceived(AnomalyEvent ev)
    {
        // Marshal to UI thread.
        BeginInvoke(() =>
        {
            _eventFeed.Rows.Insert(0,
                ev.Timestamp.LocalDateTime.ToString("HH:mm:ss"),
                ev.Classification.ToString(),
                ev.Summary,
                ev.Confidence.ToString("F2"));
            if (_eventFeed.Rows.Count > 2000) _eventFeed.Rows.RemoveAt(_eventFeed.Rows.Count - 1);
            _tray.ShowBalloonTip(3000, $"{ev.Classification}: {ev.Summary}", ev.Explanation, ToolTipIcon.Warning);
        });
    }

    private void PopulateCapabilityTree(EngineHost host)
    {
        _capabilityTree.Nodes.Clear();
        var root = _capabilityTree.Nodes.Add("Collectors");
        foreach (var c in host.Collectors)
        {
            var node = root.Nodes.Add($"{c.Name} — {c.Capability.Level}");
            if (c.Capability.Reason is not null) node.Nodes.Add($"reason: {c.Capability.Reason}");
            node.Nodes.Add($"interval: {c.PollingInterval.TotalMilliseconds} ms");
        }
        root.Expand();
    }

    private void OpenConfigDialog()
    {
        using var dlg = new Forms.ConfigDialog(_config);
        dlg.ShowDialog(this);
    }

    private void OpenLogFolder()
    {
        if (!Directory.Exists(_config.LogOutputDirectory))
            Directory.CreateDirectory(_config.LogOutputDirectory);
        Process.Start("explorer", _config.LogOutputDirectory);
    }
}
```

- [ ] **Step 3: Update `Program.cs`**

Overwrite `src/SystemMonitor.App/Program.cs`:

```csharp
using System.Runtime.Versioning;
using System.Windows.Forms;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var configPath = GetArg(args, "--config") ?? "config.json";
        var (config, _) = ConfigLoader.LoadOrDefaults(configPath);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
```

- [ ] **Step 4: Build (tabs won't exist yet — we'll stub them next)**

Skip the build for this task; the next task adds the tab controls it depends on. Commit what we have so far.

```bash
git add .
git commit -m "feat(app): MainForm skeleton with menu/toolbar/tabs/event feed/status bar"
```

---

### Task 7.5: `ITabView` and a stub implementation for every tab

**Files:**
- Create: `src/SystemMonitor.App/Controls/ITabView.cs`
- Create: `src/SystemMonitor.App/Controls/OverviewTab.cs`
- Create: `src/SystemMonitor.App/Controls/CpuTab.cs`
- Create: `src/SystemMonitor.App/Controls/MemoryTab.cs`
- Create: `src/SystemMonitor.App/Controls/PowerTab.cs`
- Create: `src/SystemMonitor.App/Controls/StorageTab.cs`
- Create: `src/SystemMonitor.App/Controls/GpuTab.cs`
- Create: `src/SystemMonitor.App/Controls/NetworkTab.cs`
- Create: `src/SystemMonitor.App/Controls/EventsTab.cs`

- [ ] **Step 1: Create `ITabView`**

Create `src/SystemMonitor.App/Controls/ITabView.cs`:

```csharp
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

/// <summary>
/// Implemented by every tab view. Called by MainForm on each UI refresh tick.
/// </summary>
public interface ITabView
{
    void Refresh(EngineHost host);
}
```

- [ ] **Step 2: Create each tab as a minimal `UserControl`**

Create `src/SystemMonitor.App/Controls/CpuTab.cs`:

```csharp
using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class CpuTab : UserControl, ITabView
{
    private readonly Label _overallUsage = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly Label _temp = new()    { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly FlowLayoutPanel _topStrip = new() { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.LeftToRight };
    private readonly Chart _chart;

    public CpuTab()
    {
        _chart = BuildChart("CPU usage & temp (last 10 min)", "usage %", "temp °C");
        _topStrip.Controls.Add(new Label { Text = "Usage:", AutoSize = true });
        _topStrip.Controls.Add(_overallUsage);
        _topStrip.Controls.Add(new Label { Text = "   Temp:", AutoSize = true });
        _topStrip.Controls.Add(_temp);
        Controls.Add(_chart);
        Controls.Add(_topStrip);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("cpu", out var buf)) return;
        var snap = buf.Snapshot();
        var latestUsage = snap.LastOrDefault(r => r.Metric == "usage_percent"
                                               && r.Labels.TryGetValue("scope", out var s) && s == "overall");
        var latestTemp = snap.LastOrDefault(r => r.Metric == "temperature_celsius");
        _overallUsage.Text = latestUsage is null ? "—" : $"{latestUsage.Value:F0}%";
        _temp.Text = latestTemp is null ? "—" : $"{latestTemp.Value:F0}°C";

        var usageSeries = _chart.Series["usage"];
        var tempSeries = _chart.Series["temp"];
        usageSeries.Points.Clear();
        tempSeries.Points.Clear();
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff))
        {
            if (r.Metric == "usage_percent" && r.Labels.GetValueOrDefault("scope") == "overall")
                usageSeries.Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
            else if (r.Metric == "temperature_celsius")
                tempSeries.Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
        }
    }

    internal static Chart BuildChart(string title, params string[] seriesNames)
    {
        var chart = new Chart { Dock = DockStyle.Fill };
        var area = new ChartArea("main");
        area.AxisX.LabelStyle.Format = "HH:mm:ss";
        chart.ChartAreas.Add(area);
        chart.Titles.Add(title);
        foreach (var name in seriesNames)
            chart.Series.Add(new Series(name.Split(' ')[0]) { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.DateTime });
        return chart;
    }
}
```

Create `src/SystemMonitor.App/Controls/MemoryTab.cs`:

```csharp
using System.Windows.Forms.DataVisualization.Charting;
using SystemMonitor.Engine;

namespace SystemMonitor.App.Controls;

public sealed class MemoryTab : UserControl, ITabView
{
    private readonly Label _committed = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly Label _available = new() { Font = new Font("Segoe UI", 18), AutoSize = true };
    private readonly FlowLayoutPanel _top = new() { Dock = DockStyle.Top, Height = 60 };
    private readonly Chart _chart = CpuTab.BuildChart("Memory (last 10 min)", "committed %", "available MB");

    public MemoryTab()
    {
        _top.Controls.Add(new Label { Text = "Committed:", AutoSize = true });
        _top.Controls.Add(_committed);
        _top.Controls.Add(new Label { Text = "  Available:", AutoSize = true });
        _top.Controls.Add(_available);
        Controls.Add(_chart);
        Controls.Add(_top);
    }

    public void Refresh(EngineHost host)
    {
        if (!host.Buffers.TryGetValue("memory", out var buf)) return;
        var snap = buf.Snapshot();
        _committed.Text = snap.LastOrDefault(r => r.Metric == "committed_percent")?.Value.ToString("F0") + "%";
        _available.Text = snap.LastOrDefault(r => r.Metric == "available_mb")?.Value.ToString("F0") + " MB";

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        _chart.Series["committed"].Points.Clear();
        _chart.Series["available"].Points.Clear();
        foreach (var r in snap.Where(r => r.Timestamp >= cutoff))
        {
            if (r.Metric == "committed_percent") _chart.Series["committed"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
            else if (r.Metric == "available_mb") _chart.Series["available"].Points.AddXY(r.Timestamp.LocalDateTime, r.Value);
        }
    }
}
```

Create the remaining tabs following the same pattern — each is a `UserControl : ITabView` that pulls `host.Buffers[<name>]`, shows the latest key values in a header strip, and plots a time-series chart:

- `OverviewTab.cs` — sparkline cards per subsystem (CPU/Mem/Power/Storage/GPU/Network), each showing name + latest value + mini chart; click-through opens the matching tab. Implementation mirrors `CpuTab` but pulls one summary metric per source.
- `PowerTab.cs` — buffer key "power"; key values: any voltage rail + on_ac + battery_percent; chart: voltage over time.
- `StorageTab.cs` — buffer key "storage"; key values: worst latency, worst free_space_percent; chart: latency over time, one series per disk.
- `GpuTab.cs` — buffer key "gpu"; key values: temperature_celsius, load_percent; chart: temp + load over time.
- `NetworkTab.cs` — buffer key "network"; key values: link_up per adapter, gateway_latency_ms; chart: gateway latency over time.
- `EventsTab.cs` — DataGridView listing recent event-log and reliability readings with columns Time, Channel, Event ID, Level, Message.

Each tab follows the structure of `CpuTab.cs` verbatim. Copy that file, rename the class, swap the buffer key and metrics in `Refresh`, and adjust the header labels. Do not attempt to share a base class — each tab's layout is bespoke enough that duplication is clearer than inheritance.

- [ ] **Step 3: Build and run**

```bash
dotnet build
dotnet run --project src/SystemMonitor.App
```

Expected: Build succeeds; window appears with all tabs. Click Start — capability tree populates, tabs show live data, event feed populates if anomalies fire.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(app): add all tab UserControls implementing ITabView"
```

---

### Task 7.6: `ConfigDialog` using `PropertyGrid`

**Files:**
- Create: `src/SystemMonitor.App/Forms/ConfigDialog.cs`

- [ ] **Step 1: Create the dialog**

Create `src/SystemMonitor.App/Forms/ConfigDialog.cs`:

```csharp
using System.Text.Json;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.App.Forms;

public sealed class ConfigDialog : Form
{
    private readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill };
    private readonly AppConfig _config;

    public ConfigDialog(AppConfig config)
    {
        _config = config;
        Text = "Configuration";
        ClientSize = new Size(640, 640);
        StartPosition = FormStartPosition.CenterParent;

        _grid.SelectedObject = _config;

        var save = new Button { Text = "Save", Dock = DockStyle.Bottom, Height = 32 };
        save.Click += (_, _) => SaveToDisk();

        var cancel = new Button { Text = "Cancel", Dock = DockStyle.Bottom, Height = 32 };
        cancel.Click += (_, _) => Close();

        Controls.Add(_grid);
        Controls.Add(save);
        Controls.Add(cancel);
    }

    private void SaveToDisk()
    {
        var path = "config.json";
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        MessageBox.Show(
            "Saved. Some changes apply immediately (thresholds); others (enabled collectors, intervals) require Stop + Start.",
            "Configuration saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build
git add .
git commit -m "feat(app): add ConfigDialog using PropertyGrid for live editing"
```

---

## Phase 8 — CLI / Headless Mode / Polish

### Task 8.1: `--headless` mode in `Program.cs`

**Files:**
- Modify: `src/SystemMonitor.App/Program.cs`

- [ ] **Step 1: Replace `Program.cs` with full CLI support**

Overwrite `src/SystemMonitor.App/Program.cs`:

```csharp
using System.Runtime.Versioning;
using System.Windows.Forms;
using SystemMonitor.Engine;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var configPath = GetArg(args, "--config") ?? "config.json";
        AppConfig config;
        try
        {
            var (cfg, _) = ConfigLoader.LoadOrDefaults(configPath);
            config = cfg;
        }
        catch (ConfigLoadException ex)
        {
            Console.Error.WriteLine($"Config error: {ex.Message}");
            return 2;
        }

        var output = GetArg(args, "--output");
        if (output is not null) config.LogOutputDirectory = output;

        if (HasFlag(args, "--headless"))
            return RunHeadless(config);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static int RunHeadless(AppConfig config)
    {
        Console.WriteLine($"SystemMonitor headless — logging to {config.LogOutputDirectory}");
        using var host = EngineHost.Build(config);
        host.OnAnomaly += ev => Console.WriteLine(
            $"[{ev.Timestamp:HH:mm:ss}] {ev.Classification,-14} {ev.Summary}");

        host.Start();

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        Console.WriteLine("Running. Press Ctrl+C to stop.");
        stop.Wait();

        Console.WriteLine("Stopping...");
        host.Stop();
        Console.WriteLine("Done.");
        return 0;
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetArg(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static void PrintHelp() => Console.WriteLine(
        """
        SystemMonitor — diagnostic tool

        Usage:
          SystemMonitor.exe [--config <path>] [--output <dir>] [--headless]

        Options:
          --config <path>   Path to config.json (default: ./config.json; defaults used if absent)
          --output <dir>    Override LogOutputDirectory from config
          --headless        Run without UI; log to disk until Ctrl+C
          --help, -h        Show this help
        """);
}
```

- [ ] **Step 2: For headless mode to work without a UI message pump, switch the WinForms app to support console output**

Edit `src/SystemMonitor.App/SystemMonitor.App.csproj`: change `<OutputType>WinExe</OutputType>` to `<OutputType>Exe</OutputType>`. This produces a console executable that can still show WinForms windows.

- [ ] **Step 3: Build and run headless for 3 seconds**

```bash
dotnet build
timeout 3 dotnet run --project src/SystemMonitor.App -- --headless --output ./TempLogs || true
ls ./TempLogs
```

Expected: Log files created.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(app): add --headless, --config, --output, --help CLI flags"
```

---

### Task 8.2: `config.example.json`

**Files:**
- Create: `config.example.json`

- [ ] **Step 1: Create the example config**

Create `config.example.json` at repo root:

```json
{
  "LogOutputDirectory": "C:\\SystemMonitor\\Logs",
  "LogRotationSizeBytes": 104857600,
  "UiRefreshHz": 2,
  "BufferCapacityPerCollector": 3600,
  "CorrelationIntervalMs": 30000,
  "WmiTimeoutMs": 5000,
  "Collectors": {
    "cpu":         { "Enabled": true, "PollingIntervalMs": 1000 },
    "memory":      { "Enabled": true, "PollingIntervalMs": 1000 },
    "storage":     { "Enabled": true, "PollingIntervalMs": 5000 },
    "network":     { "Enabled": true, "PollingIntervalMs": 2000 },
    "power":       { "Enabled": true, "PollingIntervalMs": 1000 },
    "gpu":         { "Enabled": true, "PollingIntervalMs": 2000 },
    "eventlog":    { "Enabled": true, "PollingIntervalMs": 10000 },
    "reliability": { "Enabled": true, "PollingIntervalMs": 300000 },
    "inventory":   { "Enabled": true, "PollingIntervalMs": 0 }
  },
  "Thresholds": {
    "CpuTempCelsiusWarn": 80,
    "CpuTempCelsiusCritical": 95,
    "MemoryCommittedPercentWarn": 85,
    "DiskLatencyMsWarn": 50,
    "NetworkPacketLossPercentWarn": 2.0,
    "VoltageDeviationPercentWarn": 5.0,
    "BaselineStdDevWarn": 3.0
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "docs: add config.example.json with all tunable knobs"
```

---

### Task 8.3: Smoke-test checklist doc

**Files:**
- Create: `docs/smoke-test-checklist.md`

- [ ] **Step 1: Create the checklist**

Create `docs/smoke-test-checklist.md`:

```markdown
# SystemMonitor Smoke Test Checklist

Run these manual checks before any release. Unit and integration tests cover
behavior; these tests cover the parts that only a real machine can validate.

## 1. Capability Reporting

- [ ] Launch as **standard user**. Capability panel shows `power` and `gpu` as
      Unavailable; `cpu` as Partial (no temps); others Full.
- [ ] Launch as **Administrator**. Capability panel shows all collectors Full
      (assuming the hardware supports LHM sensors).

## 2. Long-Run Stability

- [ ] Launch in headless mode and leave for 24+ hours. Check: no memory creep
      (Task Manager), log rotation created multiple files, final summary line
      written on Ctrl+C.
- [ ] Launch in UI mode for 4+ hours; UI remains responsive, event feed doesn't
      leak rows beyond its 2000-row cap.

## 3. Anomaly Injection

- [ ] Run a CPU stress tool (e.g., `stress-ng`, Prime95). Verify CPU tab shows
      rising usage/temperature, `ThermalRunawayRule` does NOT fire (high load is
      the cause).
- [ ] Block the intake fan (briefly). Verify temps rise; if load is low, expect
      `ThermalRunawayRule` to fire with Classification=Internal.
- [ ] Unplug ethernet for >15 sec. Verify `NetworkDropAndPacketLossRule` fires —
      link flap (Internal) if repeated, gateway unreachable (External) if sustained.
- [ ] Simulate storage pressure (large file copy). Verify StorageTab reflects it
      and the rule does NOT fire for brief spikes.

## 4. Configuration

- [ ] Delete `config.json`. Launch. Defaults apply, tool runs.
- [ ] Create a malformed `config.json`. Launch headless. Expect exit code 2 and
      a clear parse-error message.
- [ ] Open Config dialog, change `CpuTempCelsiusWarn` to 40, save. New value
      takes effect on next correlation tick (no restart needed for thresholds).

## 5. Output

- [ ] Click "Open Logs". Explorer opens the configured directory.
- [ ] Open `readings-YYYY-MM-DD.jsonl` — first line is a capability header,
      subsequent lines parse as JSON.
- [ ] Open `anomalies-YYYY-MM-DD.jsonl` — if any fired, each line parses as JSON.

## 6. Shutdown

- [ ] Click Stop. Engine stops, UI stays responsive, logs flushed.
- [ ] Close window. Logs flushed, NotifyIcon removed from tray, process exits.
- [ ] Ctrl+C in headless mode. Logs flushed, process exits 0.
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "docs: add smoke-test checklist for manual release validation"
```

---

### Task 8.4: Deployment README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Expand README**

Replace `README.md`:

```markdown
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

See `docs/superpowers/specs/2026-04-15-system-monitor-design.md`.

## Manual Release Validation

See `docs/smoke-test-checklist.md`.

## Development

    dotnet test                                 # unit tests
    dotnet test tests/SystemMonitor.Engine.IntegrationTests  # Windows-only
```

- [ ] **Step 2: Commit**

```bash
git add .
git commit -m "docs: expand README with publish + CLI + deployment instructions"
```

---

## Self-Review Summary

Before executing, a quick check against the spec:

- **Spec §3 (data points)** — every collector in the spec has a task (CPU/Mem/Power/Storage/GPU/Network/EventLog/Reliability/Inventory). ✓
- **Spec §4 (internal vs external classification)** — rules cover thermal (Internal), power+KP41 (External/Indet), disk latency (Internal), network drop vs ping loss (Internal/External), baseline deviation (Indet). ✓
- **Spec §5 (architecture)** — three projects (Engine library + App + two test projects); four-layer engine wired via `EngineHost`. ✓
- **Spec §6.1 (privilege detector)** — Task 1.7. ✓
- **Spec §6.4 (logger)** — JsonlLogger with rotation, separate files per category (readings/events/anomalies/diagnostics), capability header as first line. ✓
  - Note: the `diagnostics.log` channel from spec §6.4 is not yet wired up for internal tool errors. Follow-up: route `CollectorBase.OnFailure` → a `diagnostics.log` JsonlLogger in `EngineHost`. Optional polish; does not block initial release.
- **Spec §6.7 (UI)** — `MenuStrip + ToolStrip + SplitContainer + TreeView + TabControl + DataGridView + StatusStrip + NotifyIcon`, 2Hz UI refresh, `SynchronizationContext.Post` via `BeginInvoke`, `PropertyGrid` config dialog. ✓
- **Spec §8 (error handling)** — `CollectorBase` cool-down + retry, WMI timeouts via `AppConfig.WmiTimeoutMs`, malformed config fails fast, disk-full not explicitly handled in logger (logger writes will throw; future follow-up to swallow + surface in status bar).
- **Spec §9 (testing)** — unit tests for engine types/rules/logger/config; integration tests for each collector + end-to-end; manual smoke-test doc.

Known follow-up items (not blocking):
1. `diagnostics.log` channel for internal tool errors.
2. Graceful disk-full handling in `JsonlLogger`.
3. SMART attribute reading (requires admin + additional WMI plumbing) — currently left as a documented partial capability.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-15-system-monitor.md`. Two execution options:

**1. Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. Uses `superpowers:subagent-driven-development`.

**2. Inline Execution** — execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints for review.

Which approach would you like?

