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

---

## Operator procedure (VMware Workstation)

Do **not** reuse `D:\VM\Windows 11 x64`. Create a new folder `D:\VM\WindowsLab-Test-25H2\`.

ISO on this host (confirmed present): `D:\VM\COMPARTIDO\Win11_25H2_Spanish_x64_v2.iso`.

### A. Create the VM

1. File → New Virtual Machine → **Custom (advanced)**.
2. Hardware compatibility: default (Workstation 17.x is fine).
3. Guest OS: **I will install the operating system later** (skip Easy Install — it tends to force a Microsoft account).
4. Guest: Microsoft Windows → **Windows 11 x64**.
5. Name: `WindowsLab-Test-25H2`. Location: `D:\VM\WindowsLab-Test-25H2`.
6. Firmware: **UEFI**. Check **Secure Boot**.
7. Processors: **4** cores (1 socket × 4 cores). Nested VT-x/AMD-V: **off** for the golden image.
8. Memory: **8192 MB**.
9. Network: **NAT**. After creation, **uncheck Connected** and **Connect at power on** until OOBE is done (local account).
10. SCSI: LSI Logic SAS (default) is OK. Disk: **NVMe** if the wizard offers it (closer to modern PCs); otherwise SCSI is fine.
11. Disk: **80 GB**, store as a **single file**, **allocate later** (thin).
12. Finish. Then **VM → Settings**:
    - Options → Access Control / encryption: encrypt if Workstation requires it for **vTPM**. Add **Trusted Platform Module**.
    - Hardware → CD/DVD: Use ISO `D:\VM\COMPARTIDO\Win11_25H2_Spanish_x64_v2.iso`, Connect at power on.
    - Display: 3D graphics can stay default; **no** GPU passthrough.
    - Do **not** share host folders until after `snap-clean-25h2`.

### B. Install Windows (no bypass)

1. Power on. Boot from the Microsoft ISO. Language: Spanish. Edition: **Windows 11 Pro**.
2. Keep **Secure Boot + vTPM**. Do **not** use Rufus “remove TPM”, `bypassnro`, or Shift+F10 registry hacks ([D008](../architecture/decision-log.md)).
3. Product key: skip if you will activate later with a license you own. Do not use loaders.
4. When OOBE asks for network: leave the adapter **disconnected**. Prefer **cuenta local**. If 25H2 still forces MSA, finish with a throwaway Microsoft account and switch to local in Settings → Accounts (supported). Do not run OOBE bypass scripts.
5. Name the device `WLAB-TEST`. Local admin password you will remember. Region `Spain`, keyboard `es-ES`.
6. Decline extra telemetry beyond Required. Do **not** turn off Defender, SmartScreen, UAC, or Windows Update.

### C. First boot baseline (still no tweaks)

1. Connect NAT. Install **VMware Tools** (VM → Install VMware Tools) and reboot.
2. Windows Update until current. Note build + UBR in the snapshot description.
3. System Protection **on** for C: (plenty of free space in this guest).
4. Optional: copy the WindowsLab portable zip (`artifacts\zip\WindowsLab-portable-win-x64.zip`) onto the desktop. Run **audit / checklist / Configurar** only. **Do not expect Apply** until Phase 4 (Beta 0 exits 13).
5. Do **not** install Steam, Docker, Cursor, or GPU “tweaks” on this image.
6. Snapshot **`snap-clean-25h2`**. This is the only golden restore point.

### D. How to test later

1. Clone or revert `snap-clean-25h2`.
2. Apply packs **inside the clone**, never on the golden snapshot.
3. After a test: revert. Optional named clones: `snap-dev`, `snap-game` (see table above).

### E. OVA (clone the **VM**, not a physical PC)

After `snap-clean-25h2`: File → **Export to OVF**. Save `D:\VM\COMPARTIDO\WindowsLab-Test-25H2.ova`.

Use that OVA only to recreate the **lab VM** on VMware (this PC or another hypervisor). It is **not** a Windows installer.

### F. Physical disk with no OS (do this separately)

Do **not** write the VMDK/OVA onto a real HDD. The guest has VMware storage/NIC/GPU, VMware Tools, and a different SID story. That image will not be a good Windows 11 on bare metal.

Supported path for a real PC:

1. Boot that PC from the **same official ISO** (`Win11_25H2_Spanish_x64_v2.iso`) on USB (Rufus/Media Creation **without** TPM/Secure Boot bypass). Firmware: UEFI + Secure Boot + real TPM.
2. Fresh install, local account, updates, then run WindowsLab and the Configurar packs **on that hardware**.
3. If you later want a repeatable **physical** golden image: Sysprep `/generalize` on a **physical** reference PC (or Hyper-V Gen2 with generic drivers), capture a WIM with DISM, apply with `dism /apply-image`. That is Microsoft imaging, not a “debloat ISO”. Out of WindowsLab Beta 0 scope.

WindowsLab is the lab that **measures and (later) applies tweaks on an already-installed Windows**. It is not an OS distro.
