# GitHub and ecosystem landscape

Date: 2026-09-05  
Method: GitHub REST API + project docs. Stars are point-in-time.

WindowsLab must not copy code from these projects. This is an architecture and idea survey.

## Tier A — must study

| Project | Stars | License | Lang | Why it matters | Do not copy |
| --- | --- | --- | --- | --- | --- |
| [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) | 61,948 | MIT | PowerShell | Catalog + GUI + compile-to-one-file | irm\|iex, static undo, ISO creator |
| [Raphire/Win11Debloat](https://github.com/Raphire/Win11Debloat) | 56,749 | MIT | PowerShell | CLI, Sysprep/audit-mode, per-user apply, still actively pushed 2026-09-04 | Blanket telemetry/service disables as defaults |
| [farag2/Sophia-Script-for-Windows](https://github.com/farag2/Sophia-Script-for-Windows) | 9,698 | MIT | PowerShell | Module (not one script), 25H2/26H2 tags, GUI optional, **0 open issues** at fetch — unusually disciplined | Function-call API is not a portable tweak graph |
| [microsoft/PowerToys](https://github.com/microsoft/PowerToys) | 138,429 | MIT | C / C# | Process model, WinUI, signed shipping, GPO, settings, plugins (Command Palette) | Product scope (productivity, not OS mutation) |
| [Sysinternals](https://learn.microsoft.com/en-us/sysinternals/) | n/a | proprietary + some OSS | mixed | Autoruns, Process Explorer, RAMMap — **ground truth UIs** for startup/RAM | Bundling Sysinternals binaries without license review |

## Tier B — conceptually close, lower fame or different job

| Project | Stars | License | Notes |
| --- | --- | --- | --- |
| [SysAdminDoc/Debloat-Win11](https://github.com/SysAdminDoc/Debloat-Win11) | 5 | MIT | Hardware-aware, DryRun, JSON undo manifest, guarded defaults. **This is closer to WindowsLab's safety story than WinUtil**, despite 5 stars. Last push 2026-08-12. Companion: [Restore-WindowsDefaults](https://github.com/SysAdminDoc/Restore-WindowsDefaults) (~0 stars) — import undo manifests, restore Defender/WU. |
| [denfry/WindowsCleaner](https://github.com/denfry/WindowsCleaner) | ~1 | MIT | Registry-driven engines, real `-WhatIf`/`ShouldProcess`, Pester CI. Best **PowerShell architecture** to study; do not copy code. |
| [undergroundwires/privacy.sexy](https://github.com/undergroundwires/privacy.sexy) | ~6.0k | AGPL-3.0 | Catalog → generated script UX. AGPL: do not link into WindowsLab. |
| [GameTechDev/PresentMon](https://github.com/GameTechDev/PresentMon) | ~2.5k | MIT | ETW present analysis — wrap, don’t reimplement. |
| [MarkHopper24/Task-Scheduler-Studio](https://github.com/MarkHopper24/Task-Scheduler-Studio) | ~13 | MIT | WinUI 3 + COM Task Scheduler + optional MCP. Pattern for Services/Tasks UI. |
| [smartmontools/smartmontools](https://github.com/smartmontools/smartmontools) | ~1.4k | GPL-2.0 | `smartctl -j` pipeline; GPL plugin, not core. |
| [x1n-Q/Inspectrax](https://github.com/x1n-Q/Inspectrax) | UNKNOWN (WinUI 3) | unknown until cloned | Diagnostics dashboard, health score, JSON/PDF, **no fake booster** — conceptual GUI peer |
| [Builtbybel FluentCleaner](https://github.com/builtbybel) (family) | varies | often MIT | WinUI 3 cleaner using Winapp2.ini; cleanup ≠ tweak engine |
| [Frenchouioui/Vitals](https://github.com/Frenchouioui/hardwaremonitoringWINUI3) | low | unknown | WinUI 3 + LibreHardwareMonitor + **PawnIO kernel driver** — sensors need a signed driver; do not take that dependency lightly |
| [cechout/fluent-hwinfo](https://github.com/cechout/fluent-hwinfo) | 2 | MIT | WinUI 3 + LHM + LiveCharts; same driver caution |

## Tier C — privacy / custom OS / commercial (study the failure modes)

| Name | Type | Lesson |
| --- | --- | --- |
| privacy.sexy | Web → script generator | Nice UX for selecting ops; generated scripts still need evidence grades |
| O&O ShutUp10++ | Proprietary | Policy-style privacy toggles; closed source — **do not depend** |
| Winaero Tweaker | Proprietary | Hidden Windows settings encyclopedia; licensing blocks reuse |
| Atlas / ReviOS playbooks | Playbook on stock Windows (AME) | More auditable than mystery ISOs; still **out of scope** (D008). AME GUI closed. |
| Wintoys | Store/third-party (installer on this PC at `D:\VM\COMPARTIDO\Wintoys Installer.exe`) | Competitor UX; closed. Do not reverse engineer |

## Benchmark and diagnostic tools (use, don't clone)

| Tool | Role | License caution |
| --- | --- | --- |
| Windows Performance Recorder / Analyzer | OFFICIAL ETW | ADK redistributable rules |
| DiskSpd | OFFICIAL-ish Microsoft disk IO | MIT on GitHub historically |
| PresentMon | Intel, industry standard frame times | MIT |
| CapFrameX / FrameView | Overlay/capture | License per vendor |
| 3DMark / Cinebench | Synthetic | **Not redistributable** |
| CrystalDiskInfo / smartmontools | SMART | GPL for smartmontools — plugin, not core |
| HWiNFO | Sensors | Freeware EULA; installer already in D:\VM\COMPARTIDO |
| WinSAT | Inbox | Weak, outdated; do not use as proof |

## Package management

winget is already on this host (v1.29.290). Chocolatey is not installed. WinUtil's dual winget/choco catalog is optional for WindowsLab **Applications** module; it is not the core product.

## GUI stacks in the wild

- PowerShell + WPF: WinUtil, many internal tools — fastest to hack, weakest types.
- WinUI 3: PowerToys, Inspectrax, FluentCleaner, Vitals — current native look.
- WPF .NET 8/10: still best for some admin tools; unpackaged is easy.

See [../TECHNOLOGY-DECISION.md](../TECHNOLOGY-DECISION.md).

## Conceptual reuse vs reject

**Reuse conceptually**

- Declarative tweak/app catalogs (WinUtil, Sophia)
- DryRun + undo manifest (Debloat-Win11, WindowsCleaner writeups)
- Hardware-aware defaults (Debloat-Win11)
- Unelevated settings + elevation broker (PowerToys)
- Plugin isolation (PowerToys Command Palette / GPO)
- Honest diagnostics without "boost" (Inspectrax pitch)

**Reject**

- Popularity as evidence
- Prefetch deletion as optimization (local BAT toolkit + many scripts)
- `GC.Collect` RAM boost (local `toolkit-Rendimiento.bat`)
- Disable Defender / HVCI / WU as default
- Custom ISO TPM bypass (WinUtil Creator)
- irm \| iex as install

## Compatibility note

Sophia advertises 25H2/26H2. WinUtil documents a Start Menu tweak that **will not work on newer Windows**. Catalogs rot. WindowsLab compatibility must be **per tweak**, tested on VMs, not a global "supports Windows 11".
