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
