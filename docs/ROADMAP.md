# ROADMAP

Date: 2026-09-23

**WindowsLab 1.2.0** is the current public product slice: audit, stacks, curated apply with backup, live Performance, five-destination UI, expanded apps, and curated external workload stop (Steam/Riot/Overwolf…). System apply on daily hosts stays opt-in (D020). Mutating tests stay on [testing/vm-lab.md](testing/vm-lab.md).

Agent model notes use Cursor's available slugs: cheap `composer-2.5-fast` for boilerplate, `gpt-5.6-sol-medium` for specialized design, current session model for architecture/critical review. Do **not** default to the most expensive model.

## 1.2.0 — External workloads + production polish (current)

| | |
| --- | --- |
| Objective | Detect/stop Steam·Riot·Overwolf·LibreOffice… on real PCs; remove leftover Beta/read-only docs |
| Dependencies | 1.1.0 |
| Tasks | D028 workload catalog + Más UI + CLI; version 1.2.0; SECURITY/EULA/CLI README |
| Deliverables | Installer/portable 1.2; `workload list|stop` |
| Tests | Unit suite; workload catalog load; App build |
| Risks | Stopping Vanguard/Gaming Services may need elevation; overlays respawn if relaunched |
| Acceptance | Más shows active Steam/Overwolf on PC-HUGO; Stop closes allowlisted procs; never Defender/Search/SysMain |
| Agent | medium |

## 1.1.0 — Five-destination UI + catalog curation (done)

| | |
| --- | --- |
| Objective | Discoverable IA (Inicio/Ajustes/Apps/Rendimiento/Más), repo packaging, curated registry catalog |
| Dependencies | 1.0.0 |
| Tasks | D024 UI, D025 GitHub surface, D026 catalog prune/add; version 1.1.0 |
| Deliverables | Installer/portable labeled 1.1; release on `v*` tags |
| Tests | Unit suite green; presets resolve; App build |
| Risks | SmartScreen unsigned |
| Acceptance | Five nav destinations; montaje → check → apply; GitHub Release assets |
| Agent | medium |

## 1.0.0 — Public product (done)

| | |
| --- | --- |
| Objective | Ship as everyday Windows 11 configure tool: menus, stacks, live dashboard, apply with gates |
| Dependencies | Beta 0.4 features |
| Tasks | Rebrand (no Beta UX), stacks empresa/pruebas, menu bar, version 1.0.0, EULA/README |
| Deliverables | Installer/portable labeled 1.0; D020/D021 safety retained |
| Tests | Preset stacks load; CLI/App build; unit suite green |
| Risks | SmartScreen unsigned; Tamper Protection on Defender tweak |
| Acceptance | Home shows Empresa/Pruebas; subtitle 1.0; system apply still gated on host |
| Agent | medium |

## BETA 0.4 — Live dashboard (done)

| | |
| --- | --- |
| Objective | Local-only live CPU/RAM/disk + processes grouped Windows/Microsoft/External |
| Dependencies | Beta 0.3 |
| Tasks | `LiveDashboardSnapshot`, `ProcessSampler`, WPF Performance page (2s timer), CLI `live` |
| Deliverables | Nav Performance; `windowslab-cli live --output json`; no process kill UX |
| Tests | Classify heuristics; live JSON smoke; App build |
| Risks | AccessDenied on MainModule; first CPU sample ~0 |
| Acceptance | Open Performance → gauges refresh ~2s; External filter shows third-party; leave page stops timer |
| Agent | medium |

## BETA 0.3 — System apply (done)

| | |
| --- | --- |
| Objective | Backup manifests, elevated Worker, HKLM + service/task/power apply, rollback, restore-point attempt |
| Dependencies | Beta 0.2 |
| Tasks | `WindowsLab.Backup`, `WindowsLab.Worker`, `SystemApplyEngine`, CLI `--apply` / `backup list` / rollback, GUI Respaldos + AllowSystemApply |
| Deliverables | Apply on VM with UAC; host gated; lab `--lab-apply` still works |
| Tests | Backup store unit tests; eligibility tests; VM smoke checklist |
| Risks | UAC cancel; RP 24h limit; unsigned SmartScreen |
| Acceptance | Lab VM: apply HKLM sample + rollback from ProgramData backup; host refuses system apply without opt-in |
| Agent | medium + independent review for handlers |

## BETA 0.2 — Theme + Applications (done)

| | |
| --- | --- |
| Objective | Dual theme (dark/light/system); curated apps via winget; multi-axis browser/tool recommendations |
| Dependencies | Beta 0.1 |
| Tasks | `catalog/applications`, `WindowsLab.Applications`, `app` CLI, WPF Aplicaciones page, theme dictionaries |
| Deliverables | Same as Beta 0.1 + app list/recommend/install + theme toggle |
| Tests | App scoring by profile; catalog load; theme round-trip |
| Risks | winget UAC; unsigned SmartScreen |
| Acceptance | Unelevated audit; lab apply HKCU; `app install --yes` with confirmation; theme persists |
| Agent | medium |

## PHASE 0 — Research

| | |
| --- | --- |
| Objective | Evidence, host audit, architecture, this docs tree |
| Dependencies | none |
| Tasks | Done in this session |
| Deliverables | `docs/**` |
| Tests | n/a |
| Risks | Stale GitHub stars; unelevated gaps (TPM) |
| Acceptance | Operator can answer the 16 questions |
| Agent | Reasoning model (this session) |

## PHASE 1 — Foundation

| | |
| --- | --- |
| Objective | Solution, CI, Core models, schema, CLI stub, SQLite log |
| Dependencies | Phase 0 confirmation |
| Tasks | `dotnet new`, `WindowsLab.Core`, JSON schema for audit + tweak, `windowslab --help`, GitHub Actions build, `.editorconfig` |
| Deliverables | Compiling sln, empty catalog, AGENTS.md for implementers |
| Tests | Unit tests for OS version detection (ProductName quirk) |
| Risks | WinUI project templates vs unpackaged |
| Acceptance | `dotnet test` green on host |
| Agent | `composer-2.5-fast` for project files; medium for schema |

**Phase 1 first slice (done).** SQLite log is deferred; Beta 0 does not require it.

## BETA 0 — Read-only product (superseded by 0.1/0.2/0.3)

| | |
| --- | --- |
| Objective | Installable dashboard + lab apply HKCU (D018). Dual installer, WPF shell, self-contained win-x64 |
| Dependencies | Phase 1 |
| Tasks | Audit probes, curated catalog, recommend, CLI JSON, WPF, `eng/publish.ps1` |
| Deliverables | `WindowsLab.exe` (GUI), `windowslab-cli.exe` (CLI), `WindowsLab-Setup.exe`, portable zip |
| Tests | Probe fakes, catalog load, scoring, apply blocked (exit 13) |
| Risks | SmartScreen unsigned; WASDK avoided (D017) |
| Acceptance | Unelevated audit on host; Apply does not write; setup.exe or zip runs without the .NET SDK |
| Agent | medium |

## PHASE 2 — Audit Engine


| | |
| --- | --- |
| Objective | Probe runner covering ola.txt + host-audit gaps (DXGI VRAM, topology, denied labels) |
| Dependencies | Phase 1 |
| Tests | Fakes for CIM; one live unelevated test tagged `[Live]` |
| Risks | CIM differences 25H2 |
| Agent | medium for probes; fast for DTOs |
| Acceptance | JSON audit on PC-HUGO unelevated with VBS=2, dual GPU names, 16 GB RAM |

## PHASE 3 — Backup/Recovery

| | |
| --- | --- |
| Objective | Backup manifest, reg export, task XML, restore-point attempt, restore |
| Dependencies | Phase 2 |
| Tests | VM only for restore-point |
| Risks | 24h restore-point limit; C: ~67 GB free on PC-HUGO — backups/VSS prefer D: (D014) |
| Agent | medium |

## PHASE 4 — Tweak Engine

| | |
| --- | --- |
| Objective | Executor + 5 OFFICIAL/LOW sample tweaks (e.g. show file extensions — documented) |
| Dependencies | Phase 3 |
| Tests | VM apply/rollback |
| Risks | Temptation to import WinUtil JSON |
| Agent | medium + independent review |
| Acceptance | apply + rollback of one HKCU tweak without backup; one HKLM with backup |

## PHASE 5 — Dashboard

| | |
| --- | --- |
| Objective | WinUI shell, navigation to all routes (stubs allowed) |
| Dependencies | Phase 2 at least |
| Tests | Smoke launch |
| Agent | fast for XAML; medium for MVVM |

## PHASE 6 — Performance

| | |
| --- | --- |
| Objective | Perf counters, memory education UI, DiskSpd protocol |
| Dependencies | 2, 4 |
| Agent | medium |

## PHASE 7 — Gaming

| | |
| --- | --- |
| Objective | Detect Game Mode/HAGS/overlays; PresentMon optional; no FPS lies |
| Dependencies | 6 |
| Agent | medium |

## PHASE 8 — Security/Privacy

| | |
| --- | --- |
| Objective | OFFICIAL policy tweaks only; Defender status; HVCI explain |
| Dependencies | 4, security review |
| Agent | medium + security-reviewer |

## PHASE 9 — Developer/Homelab

| | |
| --- | --- |
| Objective | Detect toolchain; recommend long paths, WSL keepalive **only if documented**; do not break Hyper-V/VMware |
| Dependencies | 2 |
| Agent | fast/medium |

## PHASE 10 — Plugin System

| | |
| --- | --- |
| Objective | Host + one sample plugin (read-only docker or nvidia-smi wrapper) |
| Dependencies | 1 |
| Agent | medium |

## PHASE 11 — CLI polish

| | |
| --- | --- |
| Objective | Full command set, `--format json` stability, man-like help |
| Dependencies | 2–8 |
| Agent | fast |

## PHASE 12 — Testing

| | |
| --- | --- |
| Objective | VM pipeline documented + automated as far as VMware allows |
| Dependencies | all engines |
| Agent | medium |

## PHASE 13 — Release

| | |
| --- | --- |
| Objective | Signed unpackaged zip, CHANGELOG, winget manifest draft, no iex |
| Dependencies | 12 |
| Agent | medium |

## Parallelism after confirmation

Phase 1 is serial. After Core exists: Audit (2) and CLI skeleton (11 early) parallel. GUI (5) after audit JSON exists. Tweaks (4) after backup (3).
