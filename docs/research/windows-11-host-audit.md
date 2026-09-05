# Windows 11 host audit (read-only)

Date: 2026-09-05 23:19 +02:00  
Host: PC-HUGO  
Elevation: **standard user** (many probes require admin)

## Identity (reliable)

WMI `WindowsProductName` reports **Windows 10 Pro**. Registry `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`:

| Field | Value | Reliability |
| --- | --- | --- |
| ProductName | Windows 10 Pro | Known quirk: Windows 11 still uses this string in several APIs. **Do not trust ProductName alone.** |
| DisplayVersion | 25H2 | Reliable |
| CurrentBuild.UBR | 26200.9168 | Reliable |
| EditionID | Professional | Reliable |
| InstallationType | Client | Reliable |
| CompositionEditionID | Enterprise | Interesting: Pro composition can include Enterprise packages. **NEEDS_RESEARCH** for feature availability. |
| OsBuildNumber (Get-ComputerInfo) | 26200 | Reliable |
| Install date | 2025-12-30 | Reliable |
| Locale | es-ES | Reliable |
| Domain | WORKGROUP | Reliable |
| PC type | Desktop | Reliable |
| HypervisorPresent | True | Reliable (Hyper-V/VBS/VMware can all set this) |

**Product decision:** detect Windows 11 via build >= 22000 plus `DisplayVersion`, not `ProductName`. Grade: **OFFICIAL** (Windows 11 version history / build 22000).

## Hardware

### CPU

| Item | Observed | How | Grade |
| --- | --- | --- | --- |
| Name | 12th Gen Intel Core i5-12600K | Win32_Processor | Reliable |
| Architecture | x64 (CIM Architecture=9) | CIM | Reliable |
| Sockets | 1 (inferred) | NumberOfProcessors not fully dumped | Strong |
| Cores / threads | 10 / 16 | CIM | Reliable |
| P-cores / E-cores | **not exposed by Win32_Processor** | Intel 12600K is 6P+4E (12 threads P + 4 E) | Spec sheet is **STRONG EVIDENCE**; live P/E split needs CPUID / Intel PCM / ETW. **NEEDS_RESEARCH** for a first-party API |
| Base frequency | 3686 MHz advertised | MaxClockSpeed | Approximate (WMI base, not live P-core turbo) |
| Live boost | not collected | needs perf counters / RAPL / vendor | External/admin |
| L2 / L3 | 9728 KB / 20480 KB | CIM | Reliable as reported |
| Instructions (AVX, etc.) | not collected | CPUID | Needs engine, not raw PowerShell |
| VirtualizationFirmwareEnabled | False | CIM | **Unreliable here**: hypervisor is present, firmware virt is enabled in practice. Do not treat this CIM flag as Secure Boot/VT-x truth. |

### GPU

| Adapter | VRAM (WMI AdapterRAM) | Driver | Notes |
| --- | --- | --- | --- |
| NVIDIA GeForce RTX 3060 | ~4.0 GB reported | 32.0.16.1656 (2026-08-20), signed NVIDIA | AdapterRAM is a 32-bit field and **cannot report > ~4 GB**. RTX 3060 desktop is 12 GB. Treat AdapterRAM as **estimated / truncated**. Need DXGI / NVAPI for real VRAM. |
| Intel UHD Graphics 770 | ~2.0 GB reported | 32.0.101.6129 (2024-10-18) | iGPU present (hybrid). Display path currently on NVIDIA 1920x1080 @ 143 Hz. |

Temperatures: **not available** from CIM in this session. Needs LibreHardwareMonitor, NVIDIA NVML, or admin + kernel helper. **External/driver.**

### RAM

Two modules, Corsair CMW16GX4M2D3600C18, dual channel (Controller0-ChannelA + Controller1-ChannelA), 8 GB × 2 = **15.78 GB** visible.

| Item | Observed | Reliability |
| --- | --- | --- |
| ConfiguredClockSpeed | 3600 | Reliable (XMP-ish) |
| Speed | 3467 | CIM often reports JEDEC vs XMP inconsistently |
| Voltage | 1350 mV | CIM, usually OK |
| Timings (C18 from part number) | part number says C18 | Part number decode is **STRONG**; live tCL/tRCD via SPD needs vendor tool / undocumented |
| Channels | 2 | Reliable from locators |

Free physical at snapshot: ~4.2 GB of ~16 GB. **Do not interpret free RAM as headroom quality.**

### Storage

Two **WD SN750** (`WDS500G3X0C-00SJG0`) NVMe SSDs, GPT, Healthy, firmware `111130WD`.

- Disk 0: boot/system, C: NTFS ~498 GB (~67 GB free) — **tight free space on C:**
- Disk 1: D: NTFS ~500 GB (~339 GB free)
- Hidden recovery/EFI-style NTFS volumes present

SMART / `Get-StorageReliabilityCounter`: **failed in this session** (parameter binding / access). Admin + Storage cmdlets or `smartctl` / vendor NVMe log. **Needs admin or external tool.**

TRIM: not queried. `fsutil behavior query DisableDeleteNotify` typically needs admin. **NEEDS_RESEARCH** on unelevated detect.

### Motherboard / firmware

- Board: Gigabyte **Z690 UD DDR4**
- BIOS: AMI **F3**, 2021-10-29 — **very old relative to 2026**. Updating BIOS is out of scope for auto-tweaks; audit should **warn**.
- TPM query: **access denied** (needs admin)
- Secure Boot: **access denied**
- Serial strings: "Default string" (Gigabyte default)

PCIe topology: not enumerated. Would use `Get-PnpDevice`, `Get-NetAdapterHardwareInfo`, or PCI tree. Partial via GPU PnP IDs (`VEN_10DE&DEV_2504` = GA106 3060).

## Security posture (partial)

`Win32_DeviceGuard` (worked **without** admin on this host):

| Property | Value | Meaning (OFFICIAL: Microsoft Learn Device Guard WMI) |
| --- | --- | --- |
| VirtualizationBasedSecurityStatus | 2 | VBS enabled **and running** |
| SecurityServicesRunning | {0} | No Credential Guard / HVCI running |
| SecurityServicesConfigured | {0} | None configured |
| CodeIntegrityPolicyEnforcementStatus | 2 | Enforced |
| UsermodeCodeIntegrityPolicyEnforcementStatus | 0 | Off |

Interpretation: **VBS is on**, memory integrity / Credential Guard **do not appear to be running**. AvailableSecurityProperties includes hypervisor, Secure Boot capability, NX, SMM mitigations, etc. Exact decoding of the truncated `{1, 2, 5, 6...}` list was not fully printed.

BitLocker: **access denied**.

Defender (Get-MpComputerStatus, worked unelevated):

- Real-time, AM, antispyware, IOAV, NIS, AV: **enabled**
- Tamper protection: **False**
- Full scan age: 4294967295 → **never** (sentinel)
- Quick scan age: 0
- Signatures updated 2026-09-05

Firewall: Domain/Private/Public **Enabled**.

RDP: `fDenyTSConnections = 1` (RDP disabled). Reliable HKLM read.

## Windows features we could not list

`Get-WindowsOptionalFeature -Online` requires elevation. The existing baseline script in `ola.txt` would fail this section unelevated.

GPO: not collected. `gpresult /h` needs rights; local vs domain (this is WORKGROUP).

BCD: `bcdedit /enum {current}` failed because PowerShell wrapped `/enum` incorrectly in this session (`/encodedCommand`). Needs `bcdedit.exe /enum "{current}"` as admin typically. **Not collected.**

## Power / memory manager

- Active plan: **Máximo rendimiento** (custom GUID `63871353-0840-4c89-8559-49150e475361`, not the inbox High Performance GUID)
- S3 sleep, hibernate, fast startup **available**
- S0 low-power idle **not supported** ("firmware")
- Hybrid sleep **not supported** ("hypervisor")
- `C:\hiberfil.sys` **absent** at audit time
- Pagefile `C:\pagefile.sys` PeakUsage 2388 MB; MaximumSize 0 in Win32_PageFileSetting often means **system-managed**
- `Get-MMAgent`: **access denied** (admin). Memory compression state **UNKNOWN** on this host until elevated.

## Services / tasks / apps / processes (counts)

| Object | Count |
| --- | --- |
| Services | 311 total, 135 running |
| Scheduled tasks | 191 |
| AppX (current user) | 138 |
| Win32_Product | 162 (**do not use this class in product** — it can trigger MSI repair; slow) |
| Startup (Win32_StartupCommand) | 13 (incomplete vs Task Manager Startup) |
| Processes | 298 snapshot |

SysMain: Running, Automatic  
WSearch: Running, Automatic  
DiagTrack: Running, Automatic

## Gaming-related registry (HKCU, unelevated)

- `HKCU\SOFTWARE\Microsoft\GameBar\AutoGameModeEnabled = 1`
- `HKCU\System\GameConfigStore\GameDVR_Enabled = 1`
- `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode` **empty / absent** in the property dump

HAGS state: **UNKNOWN** from this read. UI Settings path is the documented user method; registry `HwSchMode` 1/2 appears in Microsoft Q&A (**COMMUNITY**, not a kernel-mode spec we treat as OFFICIAL apply). Driver DDI `DXGK_FEATURE_HWSCH` is **OFFICIAL** (WDDM).

## Networking extras

Fortinet VPN stack installed. DNS on Wi-Fi is Google Public DNS. Ethernet 2.5G down.

## Permission matrix (this host, this session)

| Data | Unelevated | Admin |
| --- | --- | --- |
| ComputerInfo, CPU, RAM, disks, GPU names | Yes | Yes |
| Device Guard CIM | Yes | Yes |
| Defender status | Yes | Yes |
| Firewall profiles | Yes | Yes |
| TPM / Secure Boot | No | Yes |
| BitLocker | No | Yes |
| Optional features | No | Yes |
| Get-MMAgent | No | Yes |
| Get-VM | cmdlets exist, access denied | likely Yes if Hyper-V admin |
| Storage reliability / SMART | No (this session) | likely Yes |
| bcdedit | typically No | Yes |
| Registry HKLM policies | partial | Yes |

## Implications for WindowsLab

1. The Audit Engine must **degrade gracefully** and label each probe `ok | denied | unsupported | error`.
2. Windows 11 detection must not use `ProductName`.
3. GPU VRAM must not use `Win32_VideoController.AdapterRAM`.
4. P/E-core topology needs a dedicated probe, not WMI cores.
5. C: has low free space — pre-check for backups, ISO work, and restore points.
6. BIOS F3 (2021) is an audit finding, not an auto-tweak.
7. VBS running + hypervisor present + VMware + WSL + Docker: **do not recommend disabling hypervisor** for "gaming FPS" without a measured, profile-gated EXPERIMENTAL path.
8. Never query `Win32_Product`.
