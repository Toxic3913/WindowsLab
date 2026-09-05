# REQUIREMENTS

Date: 2026-09-05

## Problem

Operators (this user: developer + gamer + homelab) need a Windows 11 tool that audits and changes the OS **without** becoming another irreversible debloater.

## Users

| Persona | Need |
| --- | --- |
| Self (PC-HUGO) | Gaming + Cursor/VS Code + Docker/WSL/VMware |
| Future: similar power users | Same product, different hardware |

No enterprise MDM requirement in v1.

## Functional requirements (v1–v4)

| ID | Requirement | Phase |
| --- | --- | --- |
| F1 | Read-only audit with denied-probe labeling | 2 |
| F2 | Typed inventory (CPU, GPU VRAM via DXGI, RAM, disks, OS build) | 2 |
| F3 | Backup store + restore | 3 |
| F4 | Tweak catalog with detect/apply/rollback/verify | 4 |
| F5 | Risk + evidence + reboot flags | 4 |
| F6 | Recommendation engine from facts + profile | 4–6 |
| F7 | WinUI dashboard shell | 5 |
| F8 | CLI parity for audit/backup/tweak | 11 (CLI can start earlier as the engine's first client — **bring CLI in Phase 1**) |
| F9 | Before/after disk + CPU protocols | 6 |
| F10 | Gaming module with PresentMon optional | 7 |
| F11 | Security/privacy catalog (OFFICIAL only by default) | 8 |
| F12 | Developer detector | 9 |
| F13 | Plugin loader | 10 |
| F14 | Logging SQLite | 1 |
| F15 | Tests + VM protocol | 12, also continuous |

**Decision:** CLI is not delayed to Phase 11 in practice. Phase 11 is **CLI completeness and polish**. Phase 1 ships a stub `windowslab --help` and `windowslab audit` prototype.

## Non-functional

- Local-only
- Spanish + English UI later; v1 English logs, Spanish-capable messages OK (host locale es-ES)
- Must run on Windows 11 25H2 x64
- Must not require kernel drivers
- Must degrade without admin
- Must not use `irm | iex`

## Out of scope v1

ISO customization, product-key tools, fleet remoting, custom OS, disabling Defender by default, RAM boosters.

## Acceptance of Phase 0

This document plus architecture/roadmap answer the 16 questions in the operator brief. Phase 0 is confirmed; Phase 1 implements Core + Cli only.
