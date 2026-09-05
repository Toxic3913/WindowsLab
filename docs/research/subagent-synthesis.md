# Subagent synthesis (Phase 0)

Date: 2026-09-05  
Status: merged into canonical docs. This file records **who contributed what** so we do not re-research.

Specialist reviews (do not re-run unless the question changed):

| Review | ID | Merged into |
| --- | --- | --- |
| Plugin / CLI / testing | [Plugin CLI testing strategy](1f70bd22-fba1-4482-b88f-7b7520bc0579) | PLUGIN-SYSTEM, CLI-DESIGN, TESTING-STRATEGY, D013 |
| Performance / benchmarks | [Performance and benchmark design](db94d214-60fa-4add-86a3-cd5254713440) | BENCHMARK-ENGINE |
| GUI stack | [GUI stack architecture compare](196f2b79-4b21-4058-9dd5-773015486081) | TECHNOLOGY-DECISION, D011 (WPF dissent; WinUI 3 kept pending prototype) |
| Backup / security | [Security hardening backup model](b35338e4-799d-4009-85e2-4051092337b7) | BACKUP-RECOVERY, D012 |
| WinUtil | [WinUtil architecture analysis](f2d7a5ce-fd22-4c4d-96dd-697b511aedad) | winutil-analysis.md |
| Gaming | [Gaming Windows module research](1234715e-7c95-44a3-ab18-f8647b83eb71) | gaming-module.md |
| Audit APIs | [Windows internals audit APIs](2084b40c-bc98-4398-832d-fc7b1e9e3e34) | AUDIT-ENGINE, research-index R027–R034 |
| GitHub landscape | [GitHub related tools survey](59a7fca6-39cd-4bd9-9e22-242858ca1f7f) | github-landscape.md |

## Conflicts resolved in the decision log

1. **WinUI 3 vs WPF** — D002 stands; D011 requires a Phase 5 prototype before more GUI investment.
2. **Device Guard elevation** — official examples use elevated PowerShell; PC-HUGO succeeded unelevated. Dual-path probes (R027).
3. **Pester vs xUnit** — product is C#; Pester remains optional for PS adapters. Sandbox/VM protocol still applies.

## Still UNKNOWN (do not invent)

- AppContainer viability for plugin workers
- NVIDIA/AMD SDK redistribution
- `wsb exec` stdout capture timeline
- Code-signing certificate on this host
- System Protection enabled on C:
- CompositionEditionID Enterprise implications on Pro
- Exact reboot-pending key set we will ship
