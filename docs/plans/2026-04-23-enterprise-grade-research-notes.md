# Enterprise-Grade Research Notes

**Date:** 2026-04-23
**Purpose:** Source material and reasoning behind `2026-04-23-enterprise-grade-phase1.md`. Kept separate so the plan stays terse and the evidence stays auditable.

## Competitive landscape

SystemMonitor sits between three categories:

1. **RMM agents** — NinjaOne, ConnectWise Automate, Kaseya, Datto. Fleet-oriented, policy-driven, alert-on-threshold. 10-year head start, full MDM stacks, established sales channels. Competing head-on = losing battle.
2. **Observability agents** — Datadog (incl. Hardware Sentry), Microsoft SCOM, Splunk Universal Forwarder. OTLP-first in 2026. Built for SOC/NOC buyers. Hardware health is a feature, not the product.
3. **OEM diagnostics** — Dell SupportAssist, HP Support Assistant, Lenovo Vantage. Vendor-locked. Principled Technologies (Dell-funded, treat with skepticism) showed Dell detected a failing disk proactively, HP TechPulse missed it, Lenovo Vantage recommended a non-actionable "repair bad sectors."

**Where SystemMonitor is differentiated:** Internal-vs-External classification with confidence + human-readable explanation. RMMs alert; observability tools visualize; OEM tools scan. None *explain root cause*.

## The six Phase 1 gaps and why each made the cut

1. **Code signing** — Intune LOB + App Control for Business + SmartScreen all gate on this. Without it, no enterprise deployment path exists.
2. **MSI installer** — Intune/GPO/SCCM expect MSI or MSIX. `dotnet publish` single exe is a probe artifact, not a deployment artifact.
3. **Windows Service mode** — Every peer agent runs as a service. Console `--headless` dies with the user session.
4. **Log integrity** — SOC 2 auditors in 2026 expect cryptographic chain of custody. Cheap to add, expensive to retrofit later when a customer asks.
5. **PII mode** — Hostname, username, MAC, serials are personal data under GDPR. EU DPAs issued ~€1.2B in fines in 2025. One-flag anonymization is cheap insurance.
6. **Automated minidump analysis** — The single biggest *technical* gap. `!analyze` via `dbghelp.dll` is well-understood. Turns inventoried dumps into diagnosed faults. Closest competitors (WhoCrashed) are single-purpose GUIs; no one ships this *inside* a correlation engine.

## Phase 2 candidates (deferred)

Not in Phase 1, but ranked for when Phase 1 ships:

- OTLP / OpenTelemetry exporter (metrics + logs) — one integration, works with Datadog/Grafana/Splunk/Azure Monitor.
- Windows Event Forwarding / Syslog / Splunk HEC sink — for buyers not yet on OTel.
- HTML/PDF report generator — design §10 already flagged this; it's the artifact IT hands to vendor support.
- Alerting transports — webhook + SMTP first (cheapest ROI), Teams/Slack templates, SNMP for NOC.
- Patch / driver onset correlation — "anomalies began N days after KB5079473" (real-world March 2026 regression).
- Intune / GPO ADMX policy template — `.admx`/`.adml` + Intune Settings Catalog JSON.
- Optional lightweight aggregation endpoint — single-binary HTTPS receiver, no SaaS.

## Phase 3 candidates (only if Phase 2 shows traction)

- OEM warranty enrichment (Dell/HP/Lenovo serial APIs).
- ML baselining (z-score, seasonal decomposition) — replaces fixed thresholds.
- UPS / smart-PDU telemetry ingest (APC PowerChute, Eaton IPM) for authoritative mains-voltage ground truth.
- Extended network path diagnostics (traceroute, MTR, Wi-Fi RSSI/retries, switch-port SNMP).
- WER (Windows Error Reporting) integration — live failure bucket IDs.

## Out of scope (category jumps, not feature additions)

- SaaS console / multi-tenant backend
- RBAC system
- EDR or security product capabilities
- Fleet dashboard web UI

These would dilute the root-cause-analysis wedge.

## Risks flagged

- **Identity risk** — adding service + installer + central endpoint moves toward RMM territory. Mitigation: preserve portable-exe "probe mode" as a first-class deployment option alongside service mode.
- **Signing cost + overhead** — EV cert (~$300–600/yr) + hardware token or cloud HSM + release-process changes. Not optional.
- **GDPR exposure shift on centralization** — local-only keeps the user as controller. Central endpoint makes you (or the customer) a processor with DPIA obligations. Keep any central endpoint optional and self-hosted; no SaaS until legal story is solved.
- **Airgap symbol-server problem** — minidump analysis needs outbound HTTPS to msdl.microsoft.com. Many target machines are degraded/airgapped. Support offline-symbols mode from day one.
- **Scope creep via rules** — keep correlation rules data-driven (config/JSON), not code, so new rules don't ship binaries.
- **Buyer persona ambiguity** — IT help desk vs. SOC vs. field engineer vs. OEM support vs. MSP each wants a different Phase 2 set. Recommended first persona: **field engineers + help-desk escalation** (the "weird PC won't stop crashing" operator). That justifies Phase 1 + report generator + patch correlation before anything else.

## Sources

Ranked roughly by reliability (primary/official docs first, commercial listicles last):

- https://learn.microsoft.com/en-us/defender-endpoint/manage-tamper-protection-intune
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/deployment/use-signed-policies-to-protect-appcontrol-against-tampering
- https://learn.microsoft.com/en-us/intune/intune-service/fundamentals/monitor-audit-logs
- https://learn.microsoft.com/en-us/answers/questions/937550/whea-logger-a-fatal-hardware-error-has-occurred
- https://docs.datadoghq.com/opentelemetry/compatibility/
- https://www.datadoghq.com/solutions/opentelemetry/
- https://secureprivacy.ai/blog/gdpr-compliance-2026
- https://www.konfirmity.com/blog/soc-2-data-retention-guide
- https://windowsforum.com/threads/mastering-windows-crash-logs-event-viewer-reliability-monitor-and-minidumps.387972/
- https://windowsforum.com/threads/windows-11-kb5079473-march-2026-patch-tuesday-crashes-and-instability.405248/
- https://www.resplendence.com/whocrashed
- https://www.ninjaone.com/blog/monitor-client-hardware-health-metrics-using-rmm/
- https://www.ninjaone.com/blog/remote-monitoring-management-definition/
- https://www.ninjaone.com/blog/best-hardware-monitoring-software/
- https://expertinsights.com/it-management/the-top-rmm-solutions-for-msps
- https://deskday.com/top-rmm-tools-for-msps/
- https://www.principledtechnologies.com/Dell/ProSupport-Plus-drive-failure-detection-comparison-0621-v2.pdf (Dell-funded — directional only)
- https://www.principledtechnologies.com/Dell/ProSupport-Plus-comparison-0620.pdf (Dell-funded — directional only)
