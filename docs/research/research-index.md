# Research index

Canonical file. Also linked from `docs/RESEARCH-INDEX.md`.

| ID | Question | Date | Sources | Conclusion | Decision | Confidence |
| --- | --- | --- | --- | --- | --- | --- |
| R001 | What lives in C:\DEV? | 2026-09-05 | filesystem | BACKUP + new WindowsLab | Home the repo here | high |
| R002 | Is there already a WindowsLab? | 2026-09-05 | D:\OPTIMIZACIONES\ola.txt | Yes: read-only baseline script | Reimplement in engine | high |
| R003 | Host OS identity? | 2026-09-05 | registry, CIM | 11 Pro 25H2 26200.9168; ProductName lies | Detect by build | high |
| R004 | WinUtil architecture? | 2026-09-05 | official architecture page, tweaks.json, GitHub API | PS+WPF+JSON compile | Do not clone | high |
| R005 | GUI stack? | 2026-09-05 | WASDK Learn, PowerToys, host SDKs | WinUI 3 + .NET 10 | D002 | medium-high |
| R006 | HAGS official apply path? | 2026-09-05 | WDDM DDI OFFICIAL; Settings/Q&A for toggle; HwSchMode COMMUNITY | Detect carefully; do not auto-write registry in v1 | NEEDS_RESEARCH for apply | medium |
| R007 | SysMain disable? | 2026-09-05 | MMAgent official; Q&A community | Not default on NVMe | Myth/unverified as universal win | medium |
| R008 | Free RAM = performance? | 2026-09-05 | Windows Internals consensus + RAMMap | MYTH | UI education | high |
| R009 | Checkpoint-Computer limits? | 2026-09-05 | Learn | One SR point per 24h via cmdlet | Layered backup | high |
| R010 | Win32_Product? | 2026-09-05 | Microsoft + this host slowness | Do not use | Uninstall registry | high |
| R011 | GPU VRAM via WMI? | 2026-09-05 | this host AdapterRAM ~4GB on 3060 | Truncated | DXGI | high |
| R012 | P/E cores via WMI? | 2026-09-05 | Win32_Processor 10/16 only | Insufficient | CPUID | high |
| R013 | irm\|iex install? | 2026-09-05 | winutil README | Popular, unsafe | Never primary | high |
| R014 | Local BAT toolkit quality? | 2026-09-05 | toolkit-Rendimiento.bat | Theater + destructive | Do not port | high |
| R015 | Test fabric? | 2026-09-05 | D:\VM | VMware 11 + 25H2 ISO | D010 | high |
| R016 | Device Guard unelevated? | 2026-09-05 | this host | Works; VBS=2, HVCI not running | Use CIM | high |
| R017 | Get-MMAgent unelevated? | 2026-09-05 | this host | Access denied | Admin probe | high |
| R018 | BIOS age? | 2026-09-05 | Win32_BIOS F3 2021-10-29 | Stale | Audit warning only | high |
| R019 | Debloat-Win11 worth studying? | 2026-09-05 | GitHub 5 stars, docs | DryRun + undo manifest | Conceptual yes | medium |
| R020 | Game Mode FPS proof? | 2026-09-05 | Learn processor PPM + Settings | Control is OFFICIAL; FPS gain EXPERIMENTAL | detect; A/B only | medium |
| R021 | Input latency measurement? | 2026-09-05 | — | No reliable inbox API found | UNKNOWN | low |
| R022 | Code signing cert on host? | 2026-09-05 | not searched in cert stores | UNKNOWN | NEEDS_RESEARCH Phase 13 | low |
| R023 | System Protection enabled? | 2026-09-05 | not queried | UNKNOWN | Probe in Phase 2 | low |
| R024 | CompositionEditionID Enterprise on Pro? | 2026-09-05 | registry | Present | NEEDS_RESEARCH feature implications | low |
| R025 | DirectStorage on 3060 + SN750? | 2026-09-05 | hardware capable in principle | Detect GPU/OS support; games opt in | Do not "enable DirectStorage" as OS tweak | medium |
| R026 | GUI WPF vs WinUI after specialist review? | 2026-09-05 | WASDK Learn; WPF .NET 10; Administrator Protection | Keep WinUI 3; Phase 5 go/no-go prototype | D011 | medium |
| R027 | Device Guard CIM official elevation vs this host? | 2026-09-05 | Learn (elevated session); PC-HUGO unelevated success | Probe as USER first; label Denied if ACL blocks | dual-path | medium |
| R028 | Confirm-SecureBootUEFI elevation? | 2026-09-05 | Microsoft Learn | **ADMIN required (OFFICIAL)** | matches this host Access Denied | high |
| R029 | P/E cores API? | 2026-09-05 | GetLogicalProcessorInformationEx EfficiencyClass | Native Win32, not marketing P/E labels | cpu.topology | high |
| R030 | HAGS D3DKMT caps? | 2026-09-05 | DirectX blog OFFICIAL; D3DKMT_WDDM_2_7_CAPS “reserved” | Detect via Settings/dxdiag; do not use reserved struct as contract | D006 gaming detect-only | high |
| R031 | OverlayTestMode=5 / MPO disable? | 2026-09-05 | MPO DDI OFFICIAL; registry COMMUNITY | Detect capability; no auto registry | blocked default | high |
| R032 | Windowed-game optimizations? | 2026-09-05 | Microsoft Support | OFFICIAL Settings toggle | detect + optional LOW | high |
| R033 | DirectStorage BypassIO? | 2026-09-05 | fsutil bypassIo; IFS docs | Detect blockers; not an OS enable tweak | R025 | high |
| R034 | timeBeginPeriod global timer? | 2026-09-05 | timeBeginPeriod Learn (per-process since 2004) | MYTH as universal FPS; never force system-wide | blocked | high |
| R035 | wbadmin system image? | 2026-09-05 | Deprecated features list | Optional legacy; not CRITICAL sole path | D012 | high |
| R036 | reg export completeness? | 2026-09-05 | reg export/import Learn | No ACL; does not delete newer values | exact inverse | high |
| R037 | Win32_Product MSI repair? | 2026-09-05 | Microsoft troubleshoot article | Query can reconfigure apps — not read-only | already banned | high |
| R038 | Win10 still worth mutate support? | 2026-09-05 | Win10 EOS 2025-10-14 | Audit-only; confirms D006 | D006 | high |
| R039 | GHA nested Hyper-V? | 2026-09-05 | runner-images #9285 | Standard windows-latest: **no** nested virt | CI unit-only | high |
| R040 | denfry/WindowsCleaner architecture? | 2026-09-05 | GitHub (~1 star), ShouldProcess | Conceptual: declarative registry + WhatIf | study, don’t copy code | medium |
