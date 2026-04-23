# Enterprise-Grade Phase 1 Plan

**Date:** 2026-04-23
**Status:** Draft
**Goal:** Close the six table-stakes gaps that prevent SystemMonitor from being deployable in enterprise environments, without losing the "drop on a problem machine" identity.

**Context:** Market research on 2026-04-23 compared SystemMonitor to RMM agents (NinjaOne, ConnectWise, Kaseya), observability agents (Datadog, SCOM, Splunk UF), and OEM diagnostics (Dell SupportAssist, HP, Lenovo Vantage). Six gaps are non-negotiable for enterprise buyers; everything else is Phase 2+. See `docs/plans/2026-04-23-enterprise-grade-research-notes.md` for source list and reasoning.

**Non-goals for Phase 1:**
- Central server / SaaS console
- OTLP / SIEM integration
- Alerting transports (SMTP, webhook, Teams, SNMP)
- ML baselining
- Fleet dashboard
- OEM warranty enrichment

These belong in Phase 2; scheduling them now is scope creep.

---

## Phase 1.1 — Authenticode code signing

**Problem:** Unsigned binary. Intune LOB deployment rejects unsigned; SmartScreen warns; enterprise allowlisting (App Control for Business) blocks unsigned by default.

**Deliverables:**
- Authenticode signature on `SystemMonitor.App.exe` and `SystemMonitor.Engine.dll` (EV cert preferred; standard OV acceptable for v1).
- `signtool` integration in a release script (not in `dotnet publish` directly — signing stays separate from build).
- Timestamp countersignature (RFC 3161) so the binary stays valid after the cert expires.
- README section documenting how to verify: `signtool verify /pa /all SystemMonitor.App.exe`.

**Out of scope:** HSM/cloud-signing automation, reproducible builds.

**Estimate:** 1–2 days once a cert is procured. Cert procurement (EV + hardware token) is the critical path — typically 5–10 business days.

---

## Phase 1.2 — MSI installer with silent-install support

**Problem:** Single-exe publish is fine for "drop on a sick PC" but blocks Intune/GPO/SCCM rollout.

**Deliverables:**
- WiX v4 (or Advanced Installer) project producing `SystemMonitor.msi`.
- Per-machine install under `%ProgramFiles%\SystemMonitor\`.
- CLI switches: `/quiet`, `/log <path>`, optional `INSTALLSERVICE=1` for Phase 1.3 service mode.
- Start-menu shortcut + optional desktop shortcut.
- Uninstaller cleans up everything except the log directory (logs are forensic evidence — never auto-delete).
- MSI is Authenticode-signed (Phase 1.1 dependency).

**Out of scope:** MSIX packaging, Windows Store listing, ADMX policy template (Phase 2.12).

**Estimate:** 3–5 days.

---

## Phase 1.3 — Windows Service mode

**Problem:** Today's `--headless` is a console process tied to a user session. Disconnect the user, lose the run. Enterprise agents run as Windows Services.

**Deliverables:**
- New `SystemMonitor.Service` project (Worker Service template, references `SystemMonitor.Engine` unchanged).
- Installed by the MSI when `INSTALLSERVICE=1`; controlled via `sc.exe` / Services.msc / `Stop-Service`.
- Service account: `LocalSystem` (required for SMART, voltage rails, Security event log) with `WITH_SERVICE_SID_TYPE=UNRESTRICTED` restricted SID for least-privilege file ACLs.
- Service recovery: restart on failure (first / second / subsequent) — relies on SCM, not homegrown supervisors.
- Probe mode (today's exe) keeps working unchanged. Both modes coexist.
- Optional attach-UI: `SystemMonitor.App.exe --attach` connects to the running service via named pipe to view live data (stretch — defer to Phase 1.6 if scope pressure).

**Out of scope:** auto-update channel, gRPC/HTTP control surface, multi-instance on one host.

**Estimate:** 4–6 days (plus 2 days for the named-pipe attach UI if kept).

---

## Phase 1.4 — Tamper-evident logs + optional at-rest encryption

**Problem:** Plain JSONL has no integrity guarantee. SOC 2 auditors in 2026 expect cryptographic chain of custody for audit-adjacent logs. Insurance / incident response buyers ask for it.

**Deliverables:**
- Each JSONL line includes a `hmac` field chaining to the previous line's HMAC (HMAC-SHA256 with a run-scoped key written to the capability header).
- A `SystemMonitor.App.exe --verify <path>` command walks the chain and reports any tampered / missing / reordered lines.
- Optional AES-256-GCM encryption mode: when `logging.encryptAtRest=true` and a keyfile is configured, files are written as length-prefixed ciphertext frames. `--verify` handles both modes.
- Plaintext JSONL remains the default — grep-friendliness is load-bearing.

**Out of scope:** KMS integration, per-line encryption (we encrypt at frame boundaries for streaming).

**Estimate:** 3–4 days.

---

## Phase 1.5 — Privacy / PII mode

**Problem:** Inventory captures hostname, username, MAC, motherboard serial, drive serials. Under GDPR these are personal data. Current design has no redaction path and no retention ceiling.

**Deliverables:**
- Config flag `privacy.mode`: `full` (today's behavior) | `redacted` (hash with a run-salted HMAC) | `anonymous` (drop entirely).
- Covers: hostname, username, domain, MAC addresses, IPs, BIOS/motherboard/drive serials, SMBIOS UUID.
- Config flag `logging.retentionDays`: delete `readings-*.jsonl`, `events-*.jsonl`, `anomalies-*.jsonl` older than N days on startup and after rotation. Default: unlimited (opt-in only — never silently delete forensic data).
- Capability header records which privacy mode was active — downstream analysts know whether to expect real hostnames or hashes.

**Out of scope:** DPIA templates, automated consent capture, GDPR subject-access-request tooling.

**Estimate:** 2–3 days.

---

## Phase 1.6 — Automated minidump analysis

**Problem:** `ReliabilityCollector` inventories minidumps but doesn't analyze them. Competing tools (WhoCrashed, Dell SupportAssist internally) resolve BugCheck code + faulting driver via `dbghelp.dll` and the Microsoft symbol server. This is the single highest-value *technical* upgrade — it turns "a dump exists" into "KERNEL_SECURITY_CHECK_FAILURE in ndis.sys at timestamp X."

**Deliverables:**
- `MinidumpAnalyzer` component wrapping `dbghelp.dll` (P/Invoke) — runs `!analyze -v`-equivalent.
- Extracts: BugCheck code + symbol, faulting module, top-of-stack frame with symbols, time of crash.
- Symbol resolution via `_NT_SYMBOL_PATH=srv*<cachedir>*https://msdl.microsoft.com/download/symbols`; cache directory is configurable.
- Offline mode: if outbound HTTPS fails (many target machines are airgapped or degraded), emit the unresolved BugCheck code + module name only — never crash the tool, never stall the engine.
- Emits a new `anomaly.kind=minidump` event with `classification=Internal` by default; `External` if BugCheck is known-environmental (e.g., `WHEA_UNCORRECTABLE_ERROR` with correlated voltage event).
- New correlation rule: minidump arriving within 60s of a Kernel-Power 41 → prefer the existing power correlation over the generic Internal classification.

**Out of scope:** full-crashdump (not minidump) analysis, kernel driver verifier integration, automated driver rollback suggestions.

**Estimate:** 5–7 days. Symbol-server integration is the risky part; budget time for airgap fallback testing.

---

## Sequencing & dependencies

```
1.1 Code signing ──┬─> 1.2 MSI ──> 1.3 Service mode
                   │
                   └─> (all later MSI-delivered features)

1.4 Log HMAC ─── independent, any time
1.5 Privacy mode ── independent, any time
1.6 Minidump analysis ── independent (pure engine work)
```

Recommended order: **1.5 → 1.4 → 1.6 → 1.1 → 1.2 → 1.3**. Engine-only work first (ships value immediately, validates on existing portable exe), then packaging work (which requires the cert to land). This keeps the team unblocked while cert procurement is in flight.

## Total estimate

~20–30 engineering days plus cert procurement lead time. Realistic calendar: 6–8 weeks with one engineer and normal review cycles.

## Exit criteria for "Phase 1 complete"

- Signed MSI installs cleanly via Intune and via `msiexec /quiet`.
- Service mode survives a reboot and resumes logging.
- `--verify` detects a deliberately corrupted log line.
- Privacy mode verified by grep: no hostname, username, or serial appears in `readings-*.jsonl` under `privacy.mode=anonymous`.
- A synthetic BSOD (driver verifier forced fault) produces a minidump anomaly with a resolved faulting module name within 5 minutes.
- Probe mode (today's UX) still works unchanged.

## What this unlocks for Phase 2

With Phase 1 done, Phase 2 (OTLP export, alerting, report generator, patch/driver correlation, aggregation endpoint) becomes additive rather than foundational. None of Phase 2 is blocked by further Phase 1 work.
