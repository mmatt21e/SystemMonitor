# Phase 1.6b Plan — dbghelp Stack Walking + Symbol Server + Kernel-Power Correlation

**Date:** 2026-04-23
**Status:** Draft — enqueued for execution after Phase 1 cert arrives and ships.
**Parent:** `2026-04-23-enterprise-grade-phase1.md` (Phase 1.6 foundation shipped as `6ca0520`).

**Goal:** Go from "BugCheck 0x139 with params" (today, offline, ~50 lines) to "BugCheck 0x139 KERNEL_SECURITY_CHECK_FAILURE in ndis.sys+0x2A4F" (resolved driver name, optional symbol). Also upgrade minidump anomaly classification from default `Internal` to `External` when a Kernel-Power 41 event occurred within a 60-second window.

**Non-goals:**
- Full `!analyze -v`-equivalent output (thousands of lines of WinDbg heuristics — out of scope forever).
- User-mode minidump support. `MinidumpReader` already rejects `MDMP` — keep it that way.
- Remote symbol resolution for third-party drivers not on the Microsoft symbol server. If NVIDIA's `nvlddmkm.pdb` isn't downloadable, the faulting module name is enough.
- Phase 2 work: OTLP export, alerting, fleet aggregation.

## Scope

### 1.6b-1 — Faulting module resolution (no dbghelp, just parse module list)

**Problem:** BugCheck parameters often include a faulting instruction pointer (e.g., param2 of a `0x50 PAGE_FAULT_IN_NONPAGED_AREA` is the faulting address). We know the kernel dump's `PsLoadedModuleList` field (offset `0x20` in DUMP_HEADER64) points into the dump's memory region. If we can locate and walk that list, we get `(base_address, size, module_name)` tuples — enough to map an arbitrary fault address to a driver name.

**Deliverable:**
- `KernelModuleList.TryRead(stream, dumpHeader) → IReadOnlyList<ModuleRange>`.
- `MinidumpAnalyzer.Analyze(path) → MinidumpInfo` enriched with `FaultingModule` and `FaultAddress` fields when resolvable.
- `MinidumpInfo.FaultingModule` is `null` when the BugCheck params don't include an address, or when the address isn't inside any loaded module range.

**Risk:** The `_LDR_DATA_TABLE_ENTRY` structure layout varies across Windows versions. Target Windows 10+ / 11 only; document the version range.

**Estimate:** 2–3 days. This is pure binary parsing, unit-testable with synthesized dump fixtures.

### 1.6b-2 — dbghelp P/Invoke for symbol resolution (optional, gated)

**Problem:** Faulting module is the 80% answer. The last 20% — symbol within that module — needs PDBs.

**Deliverable:**
- `SymbolResolver` class wrapping `dbghelp.dll` via P/Invoke: `SymInitialize`, `SymLoadModuleEx`, `SymFromAddr`, `SymCleanup`.
- Symbol path: `srv*<cacheDir>*https://msdl.microsoft.com/download/symbols`. `cacheDir` defaults to `%ProgramData%\SystemMonitor\Symbols`; configurable.
- Gated by `AppConfig.Diagnostics.ResolveSymbols` (default `false` — symbol server hits are network operations; don't do them on airgapped machines without explicit opt-in).
- 10-second per-call timeout. On timeout or failure, proceed with `FaultingModule` name only — never block the engine or produce a partial result.

**Risks:**
- `dbghelp.dll` is thread-hostile. All calls must serialize on a single thread / lock. Plan: dedicated worker thread + queue.
- Symbol server downloads can be slow (several MB per PDB). First-time resolution of an unfamiliar driver can take 30–60s. The timeout + offline fallback handles this.
- Microsoft symbol server may throttle or rate-limit in some corporate networks that MITM HTTPS. Detect 403/407 and surface clearly.

**Estimate:** 3–4 days. P/Invoke is the risky part — budget time for thread-safety bugs.

### 1.6b-3 — Kernel-Power 41 correlation rule

**Problem:** Today every minidump emits with `classification=Internal`. But many BSODs on unstable mains power (generator, brownouts, failing PSU on a different rail) immediately follow a Kernel-Power 41 "system rebooted without cleanly shutting down" event. Currently the minidump reading and the event log reading are independent — nothing joins them.

**Deliverable:**
- New `CorrelationRule` in `SystemMonitor.Engine/Correlation/Rules/MinidumpAndPowerRule.cs`.
- Trigger: a `reliability/minidump` reading and a `eventlog/event` reading with `provider=Microsoft-Windows-Kernel-Power` and `event_id=41` whose timestamps are within 60 seconds.
- Emit `AnomalyEvent` with `classification=External`, confidence `High`, summary `"Kernel-Power 41 within 60s of BugCheck 0x<code> — likely external power event, not a driver fault"`.
- Override the generic `Internal` classification emitted by any existing minidump rule.

**Estimate:** 1 day. Rule authoring is table-stakes given the existing `CorrelationEngine` test harness.

## Sequencing

```
1.6b-1 (module list) ──┬─> 1.6b-2 (dbghelp symbols)
                       │
                       └─> 1.6b-3 (Kernel-Power rule)  [independent]
```

1.6b-1 unblocks 1.6b-2 (symbols need a module to load). 1.6b-3 is independent — it can ship before or after.

## Exit criteria

- Running against a known-good corpus of 5+ real kernel minidumps produces a `FaultingModule` for every dump (target: 100%).
- For modules whose PDBs are on the Microsoft symbol server, `FaultingSymbol` resolves (target: 80%+ for first-party Windows drivers; no guarantee for third-party).
- Offline mode: disabling `ResolveSymbols` produces the same output as 1.6b-1 alone — no network calls, no hang.
- Airgap test: block outbound HTTPS, confirm analysis still completes within 15 seconds per dump with `FaultingModule` populated and `FaultingSymbol` null.
- Kernel-Power 41 rule: synthetic event + minidump within 60s → anomaly classified `External` with the documented summary; outside 60s → minidump classified `Internal` per existing behaviour.

## What this does NOT give us (be honest with buyers)

- If a third-party driver (NVIDIA, Realtek, AMD, Broadcom) faults and its PDB isn't on Microsoft's symbol server, you'll see `nvlddmkm+0x2A4F` — not a resolved symbol. Vendors don't generally publish PDBs. That's the ceiling for any non-WinDbg approach, including this one.
- No stack unwinding. We report the faulting instruction pointer only. Full stack walking needs the thread context stream + the image relocations — significantly more P/Invoke surface and not worth the weight for Phase 1.6b. WinDbg is still the right tool for deep analysis.
- No bug-bucketing / known-issue cross-reference. `KERNEL_SECURITY_CHECK_FAILURE` is often driver-corrupted-pool — useful context a human adds, not something we lookup-table in-code.

## Tests

Unit (offline, deterministic):
- `KernelModuleList` with synthesized `_LDR_DATA_TABLE_ENTRY` entries at known offsets — one entry, many entries, malformed entries, address inside / outside range.
- `SymbolResolver` with dbghelp mock or fixture-based testing (harder — probably integration-only).
- `MinidumpAndPowerRule` with synthetic readings, within-window and outside-window.

Integration (Windows-only, real hardware / real crash dumps):
- Curated corpus of 3–5 anonymised real kernel dumps checked into `tests/fixtures/minidumps/` (redacted hostnames, timestamps preserved). Each dump asserts expected `BugCheckName` + `FaultingModule`.
- Airgap simulation: disable outbound HTTPS, run analyzer, assert `FaultingSymbol` null and no exception / no hang.

## Risks flagged

- **dbghelp threading.** Single-threaded API. Getting this wrong produces heap corruption, not a clean error. Mitigation: dedicated thread, one request at a time, 10s timeout.
- **Symbol cache disk usage.** Microsoft PDBs are tens of MB each. After a few dozen distinct dumps the cache dir can hit gigabytes. Add `AppConfig.Diagnostics.SymbolCacheMaxBytes` with LRU eviction.
- **Version drift.** `_LDR_DATA_TABLE_ENTRY` layout on Windows 12 (or whatever comes next) may break module list parsing. Reference Volatility3's `windows.pe` module — they track the per-version layout table authoritatively. When a parse fails, fall back to 1.6-era "BugCheck code only" output rather than crashing.
- **Network sensitivity.** Many target machines are airgapped, field-deployed, or on restricted networks. `ResolveSymbols=false` must be the default; opt-in only.
- **PDB authenticity.** We trust Microsoft's symbol server. If the machine's HTTPS chain is MITM'd (common on corporate proxies), we might load a tampered PDB. `dbghelp` validates PDB signatures against image CodeView records, so a mismatched PDB is rejected — but document the trust boundary.

## Open questions (answer during execution)

1. Where does the symbol cache live when the service runs as LocalSystem? `%ProgramData%\SystemMonitor\Symbols` is the obvious pick; confirm the service account has write access (it does — LocalSystem has free rein).
2. Should we also parse Kernel-Power 42 (sleep/wake) and 142 (wake-from-sleep anomaly)? Likely yes but out of scope for 1.6b — fold into a separate rule if a real case appears.
3. Do we emit the raw fault address even when we can't resolve the module? Yes — leaking an address isn't a privacy concern, and it's debugging-useful.

## When to pick this up

After Phase 1 cert lands and an initial Intune deployment runs. Real data from the field (which BugCheck codes actually show up, which modules resolve, which PDBs are missing) is more valuable than spec-driven design here. Don't build 1.6b before you have 10+ dumps from real deployments to test against.
