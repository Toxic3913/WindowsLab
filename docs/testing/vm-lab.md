# Lab VM — Windows 11 Pro (mutating tests)

Date: 2026-09-05  
Decision: [D015](../architecture/decision-log.md)

**Never apply tweaks on PC-HUGO first.** The host is the daily driver (Cursor, Steam, Docker, VMware, VPN).

The product itself stays **hardware-agnostic** ([hardware-agnostic.md](../requirements/hardware-agnostic.md)). This VM is only the **default test fabric**.

## Machine to create

| Field | Value | Why |
| --- | --- | --- |
| Name | `WindowsLab-Test-25H2` | Dedicated; do not reuse a dirty VM |
| Hypervisor | VMware Workstation (already on this PC) | Existing ISO + tooling |
| Guest | Windows 11 **Pro** 25H2 x64, Spanish ISO | Matches host SKU/build class |
| ISO | `D:\VM\COMPARTIDO\Win11_25H2_Spanish_x64_v2.iso` | Already present |
| VM files | `D:\VM\WindowsLab-Test-25H2\` | Keep C: free |
| Firmware | UEFI, Secure Boot **on**, vTPM **on** | Typical Win11 Pro |
| vCPU | **4** | Host i5-12600K can spare this |
| RAM | **8192 MB** | Host has ~16 GB; leave RAM for host + VMware |
| Disk | **80 GB** thin on D: | Enough for Windows + tools |
| Network | NAT (VMnet8 is already up on the host) | Isolated enough |
| Display | 1× virtual; **no GPU passthrough** in Phase 1–4 | OS tweaks, not FPS |
| Snapshots | `snap-clean-25h2` after OOBE + updates + VMware Tools | Always revert after a tweak test |

Do **not** use `D:\VM\Windows 11 x64` as the golden image until it is snapshotted clean and documented. Treat it as possibly dirty.

Nested Hyper-V / WSL **inside** the guest: off for the golden snapshot. Add a second snapshot `snap-virt` later if we test those modules.

## Guest baseline (golden snapshot)

1. OOBE, local admin, WORKGROUP
2. Windows Update until current (record build in the snapshot notes)
3. VMware Tools
4. Enable System Protection on C: (inside the VM, C: will have plenty of space)
5. Do **not** install Steam, Docker, or Cursor on the golden image
6. Snapshot `snap-clean-25h2`

Optional later clones:

| Snapshot / clone | Purpose |
| --- | --- |
| `snap-dev` | Git, VS Build Tools, .NET 10 SDK — for Developer profile tests |
| `snap-game` | DirectX runtime only — still no host GPU |
| Host PC-HUGO | Audit-only + optional PresentMon later (operator opt-in) |

## How a tweak test runs

1. Revert `snap-clean-25h2` (or clone it)
2. Copy `windowslab` build in
3. `windowslab audit --os` / full audit when Phase 2 exists
4. Apply / verify / rollback
5. Revert snapshot — **always**

## Adapting to any hardware (product rule)

The VM is 4 vCPU / 8 GB / virtual GPU. Real PCs will be laptops, 32 GB, AMD, ARM64 later, no D: drive, Home edition, etc.

Therefore:

- Probes return `Ok | Denied | Unsupported | Error`
- Tweaks declare `editions`, `minBuild`, `requires[]` (e.g. `gpu.vendor=nvidia`, `ram.gb>=16`, `chassis=desktop`)
- Recommendations use those facts
- Missing D: → data root fallback (D014)
- Missing admin → audit still works (as on PC-HUGO)

No `if (cpu == "i5-12600K")` in production code.
