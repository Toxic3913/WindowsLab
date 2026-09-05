# AUDIT ENGINE

Date: 2026-09-05

## Purpose

Produce a **typed, permission-aware snapshot** of the machine without changing it. This is Phase 2's product.

The existing `ola.txt` baseline is the coverage checklist, not the implementation.

## Probe contract

```
ProbeId, Category, ElevationRequired, Timeout, 
Run() -> { Status: Ok|Denied|Unsupported|Error|Timeout, Data, Evidence, Sources[] }
```

Never throw past the runner. A denied TPM probe is a successful audit with a gap.

Distinguish `false`, `zero`, `not supported`, `access denied`, and `not reported`.

### Two audit intensities

1. **Passive** — CIM/API/registry reads only. Default.
2. **Observational** — PDH sampling, ETW start, Windows Update `Search()`, power diagnostic HTML. Not a config change, but **not zero-impact**. Opt-in.

Do not start ETW in passive mode. `Win32_Product` is forbidden even for “inventory”: querying it can trigger MSI repairs ([Microsoft](https://learn.microsoft.com/en-us/troubleshoot/windows-server/admin-development/windows-installer-reconfigured-all-applications)).

`Confirm-SecureBootUEFI` **requires elevation (OFFICIAL)**. `Win32_DeviceGuard`: Learn examples use an elevated session; **this host returned data unelevated** (VBS=2). Probe USER first; if denied, retry elevated. Do not assume every SKU matches PC-HUGO.

## Probe catalog (v1)

### Hardware

| Probe | Source | Admin? | Notes |
| --- | --- | --- | --- |
| cpu.wmi | Win32_Processor | no | No P/E split |
| cpu.topology | GetLogicalProcessorInformationEx | no | `EfficiencyClass` = relative perf class, **not** guaranteed “P/E” marketing labels. Instruction sets still need CPUID |
| gpu.dxgi | DXGI adapters | no | Real VRAM |
| gpu.wmi | Win32_VideoController | no | Names/drivers; ignore AdapterRAM |
| ram.smbios | Win32_PhysicalMemory | no | Timings UNKNOWN |
| storage.physical | Get-PhysicalDisk / Storage APIs | no/partial | MediaType, bus |
| storage.smart | StorageReliabilityCounter / NVMe log | often yes | UNKNOWN unelevated |
| board.bios | Win32_BaseBoard / BIOS | no | Warn old BIOS |
| tpm | Get-Tpm | yes | |
| secureBoot | Confirm-SecureBootUEFI | yes | |

### Windows

Version via `CurrentVersion` registry + `Environment.OSVersion` + `RtlGetVersion` if needed. **Ignore ProductName.**

Optional features, BCD, BitLocker: admin probes.

Services, tasks, AppX (current user), Uninstall registry, process snapshot, startup (need **multiple sources**: Run keys, Startup approved, logon tasks — Win32_StartupCommand is incomplete).

### Security

Device Guard CIM (works unelevated here), Defender, firewall, RDP flag, VBS/HVCI decode using Microsoft enum tables.

### Developer

Presence of git, gh, node, npm, pnpm, python, uv, docker, wsl, kubectl, pwsh, code, cursor, winget, dotnet, VMware paths, VirtualBox, Hyper-V cmdlets.

### Performance snapshot (not a benchmark)

PDH: CPU %, committed bytes, available bytes, disk queue, GPU engine if counters exist. Label as **point-in-time**, not a score.

## Reliability legend (must appear in UI)

Every field tagged: `reliable | estimated | vendor_sdk | admin | unreliable_ps`.

Examples from this host:

- RTX 3060 VRAM via WMI: estimated/truncated
- P/E cores via WMI: unavailable
- ProductName: unreliable
- VirtualizationFirmwareEnabled: unreliable
- Device Guard status: reliable (this host)
- SMART: unavailable this session

## Output

- `audit.json` (schema-versioned)
- Human report Markdown/HTML
- Diff against previous audit (`windowslab audit --diff`)

## Anti-patterns

- `Win32_Product`
- Treating free RAM as a health score
- Health score 0–100 without documented weights (Inspectrax-style scores are **optional** and must show formula)
- Collecting product keys
