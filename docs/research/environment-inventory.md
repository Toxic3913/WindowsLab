# Environment inventory (read-only)

Date: 2026-09-05  
Machine: PC-HUGO  
Operator: PC-HUGO\Toxic  
Session elevation: **not administrator**

This audit did **not** modify Windows, move files, rename files, run optimization scripts, or apply tweaks.

## C:\DEV

| Path | Notes |
| --- | --- |
| `C:\DEV` | Exists. Almost empty. |
| `C:\DEV\BACKUP\Cursor\settings.json` | 46-byte Cursor settings backup. Not a WindowsLab project. |
| `C:\DEV\WindowsLab` | Created 2026-09-05 as the planning repository. **Moved to `D:\WindowsLab` on 2026-09-06** (D016). |

No Node/Python/PowerShell/Docker project trees under `C:\DEV` besides this new repo. No pre-existing Git repos under `C:\DEV` before this phase.

## D:\ (data / projects volume)

NTFS volume on the second WD SN750 NVMe (~500 GB, ~339 GB free at audit time).

| Path | What it is |
| --- | --- |
| `D:\DEV-IA` | AI/dev workspace: Ollama stack, npm caches, Next.js projects (`PROYECTO-FANTASY`, `PROYECTO-SHOPPER`). Git repo: `D:\DEV-IA\PROYECTO-FANTASY\la-liga-fantasy-analyzer`. |
| `D:\NEXUS` | Node.js fitness/app project with docs and GitHub workflows. |
| `D:\ProviSport.NET` | Present (not fully inventoried). |
| `D:\OPTIMIZACIONES\ola.txt` | **Existing WindowsLab baseline collector** (PowerShell, read-only by design). See [local-toolkits.md](local-toolkits.md). |
| `D:\Herramientas-W\` | 37 `.bat` Windows helper scripts. Mix of Settings launchers and destructive cleaners. |
| `D:\VM\Windows 11 x64\` | **VMware Workstation VM** (`.vmx` / split `.vmdk`). Primary candidate for tweak testing. |
| `D:\VM\COMPARTIDO\` | Shared folder: `Win11_25H2_Spanish_x64_v2.iso` (~8.5 GB), HWiNFO, Wintoys, LibreOffice, Chrome. |
| `D:\steam` | Steam library. |
| `D:\.pnpm-store` | pnpm store. |

No `D:\DEV` directory.

## User profile paths checked

| Path | Exists |
| --- | --- |
| `C:\Users\Toxic\Documents` | Yes |
| `C:\Users\Toxic\source` | No |
| `C:\Users\Toxic\Projects` | No |
| `C:\Users\Toxic\dev` | No |
| `C:\Users\Toxic\Desktop\WindowsLab` | No (the baseline script would create this if run; we did not run it) |

## Developer toolchain (host)

| Tool | Status |
| --- | --- |
| Git | 2.53.0.windows.2 |
| GitHub CLI | 2.100.0 (not authenticated in this session) |
| Node | v24.13.0 |
| npm | present (warns about unknown `devdir` config) |
| pnpm | 11.3.0 (WinGet package) |
| Python | 3.13.12 (`python`); `py` launcher also reports 3.15.0a7 |
| uv | 0.11.28 |
| Docker CLI | 29.5.2 |
| Docker Compose | v5.1.4 |
| Docker engine | **not running** (pipe `dockerDesktopLinuxEngine` missing) |
| kubectl | client v1.34.1 (from Docker Desktop) |
| PowerShell 5.1 | 5.1.26100.9168 (this audit used it) |
| PowerShell 7 | 7.6.5 (`pwsh`) |
| VS Code | 1.136.1 |
| Cursor | 3.18.25 |
| winget | v1.29.290 |
| Chocolatey | missing |
| Scoop | missing |
| .NET SDK | 8.0.417 and **10.0.103** |
| .NET Desktop runtime | 8.0.23 and 10.0.3 |
| Rust / Cargo / Go | missing |
| WSL | installed; default Ubuntu, version 2, **Stopped**; also `docker-desktop` distro Stopped |
| Hyper-V | cmdlets present; `Get-VM` permission denied without elevation; `vEthernet (Default Switch)` and `vEthernet (WSL)` adapters **Up** |
| VMware | `C:\Program Files (x86)\VMware` exists; VM on D:\ |
| VirtualBox | `C:\Program Files\Oracle\VirtualBox` exists |
| Kubernetes cluster | UNKNOWN (engine down; only client binary seen) |

## Virtualization / network adapters (relevant to recommendations)

Present: Fortinet SSL VPN adapters (disconnected), Realtek 2.5GbE (disconnected), Realtek 8812BU USB Wi-Fi (Up, DNS 8.8.8.8 / 8.8.4.4), multiple VMware VMnet adapters, Hyper-V Default Switch, WSL Hyper-V adapter.

WinHTTP: direct access, no proxy.

## Existing WindowsLab artifact

`D:\OPTIMIZACIONES\ola.txt` is already named **WindowsLab - 00-Baseline**. It is a read-only collector writing XML/CSV/JSON under `%USERPROFILE%\Desktop\WindowsLab\Baselines\<timestamp>\`. It was **not executed** in this phase.

Conceptual reuse: this is the seed of the Audit Engine. It must be rewritten in the shared C# engine (typed models, permission-aware probes, no `Get-WindowsOptionalFeature` failure as a hard stop when unelevated).

## Existing BAT toolkit — what not to copy

`D:\Herramientas-W\toolkit-Rendimiento.bat` treats `[System.GC]::Collect()` as a "RAM Optimizer" (theater), deletes Prefetch, runs `netsh winsock reset` / `int ip reset` as "Internet Boost", and enables Game Mode via two HKCU values. `Limpiador-Profundo-Windows.bat` deletes Prefetch, Recent files, recycle bin, then runs `sfc /scannow` and kills Explorer.

These scripts are local history, not architecture to adopt.

## Docker / compose / k8s project files under C:\DEV

None found.

## Conclusion

The real development surface is **D:\\** plus a rich host toolchain. **`D:\WindowsLab`** is the git home (D016). The installed program stays on C:. Phase 1 should not scatter new code into `D:\Herramientas-W` or `D:\OPTIMIZACIONES`.
