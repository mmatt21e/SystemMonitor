# Minidump corpus for Phase 1.6b tests

This directory holds real Windows kernel minidumps used as integration fixtures
for the faulting-module resolver (Phase 1.6b-1) and the dbghelp symbol resolver
(Phase 1.6b-2). Until those phases ship, the directory will be empty — the
Phase 1.6b-3 correlation rule unit-tests synthesise dumps on the fly and does
not depend on this corpus.

## Acquiring dumps

Three sources, in increasing realism:

### 1. Synthesised via Sysinternals NotMyFault (VM only)

Easiest, most deterministic. Use `scripts/corpus/generate-dumps.ps1` inside a
disposable Hyper-V or VirtualBox VM — it invokes `notmyfault64.exe` with each
supported crash type, waits for the reboot, and leaves the resulting `.dmp`
files in `C:\Windows\Minidump\` (or wherever `Startup and Recovery` is
configured to write small memory dumps). After a clean pass, copy the dumps
out of the VM into this directory.

**Never run NotMyFault on your host workstation.** It forces a BSOD.

Before generating:

- Set `HKLM\SYSTEM\CurrentControlSet\Control\CrashControl\CrashDumpEnabled = 3`
  (small memory dump; the default on Windows 10/11).
- Confirm `C:\Windows\Minidump\` is the output path.
- Disable overwriting so multiple dumps are kept: `OverwriteExistingLogFile = 0`
  (registry), or via `sysdm.cpl` → Advanced → Startup and Recovery.

### 2. Driver Verifier stress runs (VM only)

More realistic module diversity than NotMyFault. In a VM, enable Driver
Verifier with `verifier /standard /all` and install your target driver(s).
Let the machine run until it faults. Repeat with different drivers to widen
the coverage of real `_LDR_DATA_TABLE_ENTRY` layouts.

### 3. Field / crowdsourced dumps

Colleagues with recent BSODs, your own Intune deployment once live, OSR or
similar support forums. Before checking any such dump into git, run it
through the redaction checklist below.

## Naming convention

```
<windows-build>-<bugcheck-code>-<seq>.dmp
```

Examples:
- `win11-22h2-0x139-01.dmp`
- `win10-22h2-0x50-03.dmp`

A `manifest.jsonl` will live alongside the dumps with one line per entry:
expected BugCheck code, expected faulting module (once 1.6b-1 lands), source
(notmyfault / verifier / field), and Windows build. Integration tests read
the manifest and assert against it.

## Redaction checklist (mandatory before commit)

Kernel dumps contain whatever was in paging pool at crash time. Small memory
dumps (256 KB, the default) are already narrow-window but can still leak:

- **Hostname.** Replace in embedded strings if present. A hex-editor search
  for the machine's NetBIOS name is sufficient for small dumps.
- **Usernames.** Profile paths in kernel objects may include the logged-in
  user's name. Search and replace.
- **Volume labels and drive letters** if they carry identifying names.
- **Network configuration strings** (SSIDs, domain names).

If a dump can't be confidently redacted, do not commit it — generate a fresh
one via NotMyFault/Verifier in a VM you own.

## Privacy mode reminder

The engine's `PiiRedactor` applies to runtime readings (labels, hostnames),
not to `.dmp` file contents. Anything in this directory is what a reviewer
will see verbatim — treat every file like it's going public.
