# Local toolkits (do not execute)

Date: 2026-09-05  
These files were **read**, not run.

## `D:\OPTIMIZACIONES\ola.txt` — WindowsLab 00-Baseline

PowerShell `#requires -RunAsAdministrator` collector. Comments state it does not disable services, remove apps, or write policy.

### What it already collects

System (ComputerInfo, systeminfo, OS/CS CIM), CPU/RAM/disks/GPU/PnP, hypervisor flags, Secure Boot, TPM, Device Guard, Defender, firewall, adapters/DNS/TCP global, services, scheduled task list **plus XML export of each task**, AppX current and AllUsers, Win32 programs via **Uninstall registry** (good — not Win32_Product), processes, startup CIM, powercfg list/query, WSearch, telemetry services by name match, a few policy keys, optional features, signed drivers, JSON/CSV summary.

Output root: `%USERPROFILE%\Desktop\WindowsLab\Baselines\<timestamp>\` (not yet present).

### Gaps vs the Phase 0 audit engine design

- Requires admin even for probes that work unelevated on this host (Device Guard, Defender).
- No probe status (`denied`).
- No GPU VRAM via DXGI, no SMART, no P/E cores, no MMAgent, no pagefile semantics, no Event Log summary, no disk queue.
- `Get-WindowsOptionalFeature` will fail unelevated.
- Writes lots of CSV/XML rather than a typed document.

**Decision:** treat this script as the **functional spec of v0 audit coverage**. Reimplement in the engine. Do not ship `ola.txt` as-is. Do not move/rename the user's file.

## `D:\Herramientas-W\` — BAT menu toolkit

37 scripts, dated 2026-06-11, branded Instagram `Tecnico_informatico_tenerife`. Many items only `start ms-settings:...` (harmless launchers). Several are high-risk or theatrical:

| Script | Problem |
| --- | --- |
| `toolkit-Rendimiento.bat` | `GC.Collect` as RAM optimizer (**MYTH**); Prefetch delete; `winsock reset` + `int ip reset` as Internet Boost (requires reboot, can break VPN); Game Mode HKCU; `powercfg -setactive SCHEME_MIN` |
| `Limpiador-Profundo-Windows.bat` | Prefetch + Recent + Recycle + `cleanmgr` + **`sfc /scannow`** + kill Explorer |
| `Buscador-Clave-Producto-Windows.bat` | Product-key harvesting — **out of scope**, do not ingest |
| Network reset / USB lock / policy editor enablers | Need individual evidence review |

**Decision:** WindowsLab may later offer a **Settings launcher** module (OFFICIAL URI schemes). It must not reproduce Prefetch deletion, GC RAM boost, or silent winsock reset.

## `D:\VM`

VMware Windows 11 x64 VM + Spanish 25H2 ISO in COMPARTIDO. This is the **primary integration-test target** (see TESTING-STRATEGY).
