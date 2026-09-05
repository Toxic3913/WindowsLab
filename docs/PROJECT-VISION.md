# PROJECT VISION — WindowsLab

Date: 2026-09-05  
Status: Phase 0 — approved for documentation only

## What we will build

WindowsLab is a **professional, modular, auditable Windows 11 workstation lab**: a GUI + CLI that can inspect a machine, recommend changes based on hardware and role, apply only approved changes through a backup/verify/rollback pipeline, and prove whether a change helped.

It is inspired by tools such as Chris Titus Tech's [winutil](https://github.com/ChrisTitusTech/winutil), but it is **not a clone**. WinUtil is a popular installer/tweaker. WindowsLab is an **evidence-backed operations platform**.

## Why it exists

Existing tools optimize for speed of clicking. They often:

- apply popular tweaks without detecting current state
- store a static "original" value instead of the live previous state
- skip backup, verification, and before/after measurement
- couple tweak IDs to GUI control names
- distribute via `irm | iex`
- treat "disable SysMain / Search / Defender" as generic wins

This machine is a **gaming + developer + virtualization desktop**. Blind debloat can break Docker, WSL, Hyper-V, VMware, Game Mode, or security features. WindowsLab must know that.

## Product principles

1. **Audit before change.** Read-only inspection is the default.
2. **Recommend, never spray.** No "apply all essential tweaks" as a first-class action.
3. **Backup is mandatory** for MEDIUM and above.
4. **Detect live state.** Desired vs actual, not assumed defaults.
5. **Verify after apply.** If verification fails, offer rollback.
6. **Measure when claiming improvement.** FPS, latency, IOPS, boot time — only with a defined method.
7. **Evidence grades on every tweak.** OFFICIAL / STRONG / COMMUNITY / EXPERIMENTAL / UNVERIFIED / MYTH / UNKNOWN.
8. **Security is not an optional tab.** Disabling Defender, HVCI, or Secure Boot is never a default recommendation.
9. **One engine, two surfaces.** GUI and CLI share the same engine.
10. **Plugins are untrusted until verified.**

## What success looks like

An operator can:

- run `windowslab audit` and get a signed, structured report of this PC
- see recommendations for **Developer**, **Gaming**, **Virtualization**, or **Balanced** profiles
- apply one tweak, see the backup id, see verification, and roll it back
- run a before/after disk or PresentMon capture and get "inconclusive" when the data does not support a claim
- test changes on the existing VMware Windows 11 VM before touching the host

## What we will not build in v1

- A WinUtil clone with checkbox walls and ISO customization
- Custom Windows ISOs / "debloat Windows Setup" (WinUtil Win11 Creator) — too high risk, out of scope
- A "RAM booster" that calls `GC.Collect()` or empties the standby list by default
- Remote C2, fleet MDM, or cloud sync of machine state
- Kernel drivers of our own (LibreHardwareMonitor/PawnIO may be optional plugins later)

## Host this vision is designed against

See [research/windows-11-host-audit.md](research/windows-11-host-audit.md). Short version: Windows 11 Pro 25H2 (build 26200.9168), i5-12600K, RTX 3060 + UHD 770, 16 GB DDR4-3600 dual channel, two WD SN750 NVMe, VMware + Hyper-V + WSL2 + Docker Desktop + Steam, developer toolchain present.
