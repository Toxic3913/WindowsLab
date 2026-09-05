# ARCHITECTURE

Date: 2026-09-05  
Depends on: [TECHNOLOGY-DECISION.md](TECHNOLOGY-DECISION.md), [TWEAK-ENGINE.md](TWEAK-ENGINE.md), [AUDIT-ENGINE.md](AUDIT-ENGINE.md)

## System context

WindowsLab is a **local-first** Windows 11 lab. It does not phone home. It reads the OS, writes only after approval, and records an append-only audit log.

```
                    ┌─────────────────────────────┐
                    │  Profiles / Recommendations │
                    └──────────────▲──────────────┘
                                   │
┌────────┐   ┌─────────────┐   ┌───┴────┐   ┌───────────┐
│  GUI   │──▶│  CLI / IPC  │──▶│ Engine │──▶│  Catalog  │
└────────┘   └─────────────┘   └───┬────┘   │ (tweaks,  │
                                   │        │  checks)  │
                    ┌──────────────┼────────┴───────────┘
                    ▼              ▼
              ┌──────────┐   ┌──────────┐   ┌─────────┐
              │  Audit   │   │  Backup  │   │  Bench  │
              │  probes  │   │  store   │   │  runner │
              └──────────┘   └──────────┘   └─────────┘
                    │              │
                    ▼              ▼
              Windows 11      %ProgramData%\WindowsLab  (C:)
```

## Processes

| Process | Integrity | Role |
| --- | --- | --- |
| `WindowsLab.App` | Medium | WinUI dashboard, never mutates HKLM |
| `windowslab.exe` | Medium or High | CLI; can request elevation |
| `WindowsLab.Worker` | High | Mutating operations only |
| Plugin hosts | Medium/High per capability | Vendor GPU, Docker, etc. |

## Logical modules (map to UI routes)

Dashboard, Audit, Performance, Optimization, Gaming, Security, Privacy, Hardware, Storage, Network, Applications, Services, Scheduled Tasks, Drivers, Windows Update, Backups, Recovery, Benchmarks, Developer, Plugins, Logs, Settings.

The GUI is a **shell**. Each module is a Core service + view. Missing modules in v1 still exist as empty routes with "not implemented" rather than fake data.

## Engine services

| Service | Responsibility |
| --- | --- |
| `IAuditRunner` | Probe graph, permission-aware, timeout, artifact sink |
| `IInventory` | Hardware + OS snapshot (typed) |
| `ITweakCatalog` | Load/validate schema, compatibility filter |
| `ITweakExecutor` | detect → (backup) → apply → verify |
| `IBackupStore` | Restore points, registry exports, task XML, firewall, powercfg |
| `IRecommendationEngine` | Profile + facts → ranked recommendations |
| `IBenchmarkRunner` | Before/after protocols |
| `IAuditLog` | SQLite events |
| `IPluginLoader` | Verify signature, spawn host |
| `IDeveloperDetector` | Git, Node, Docker, WSL, … |

## Data at rest

Default (D016): **`%ProgramData%\WindowsLab`** on C:. Source/docs/tests are **`D:\WindowsLab`**. Details: [architecture/data-root.md](architecture/data-root.md).

```
%ProgramData%\WindowsLab\
  backups\<id>\
  logs\windowslab.db
  reports\
  plugins\
  tmp\

%LocalAppData%\WindowsLab\
  settings.json
  ui-state.json
```

Catalog **source** stays in git (`catalog/`). Runtime copies optional.

No secrets in settings. Worker uses Windows identity, not stored passwords.

System Restore for **C:** may store VSS shadows **on D:** (`vssadmin add shadowstorage /For=C: /On=D:`) only after explicit operator approval. Enabling protection *on D:* does not protect the OS volume.

## Catalog vs code

Tweaks that can be expressed as registry/service/task/firewall/power **stay data**. Tweaks that need DXGI, ETW, or multi-step orchestration are **C# handlers** registered by `id`. There is **no generic `InvokeScript` string** in the default catalog.

## Folder structure

Canonical tree: [architecture/folder-structure.md](architecture/folder-structure.md). Module contracts: [architecture/modules.md](architecture/modules.md).

## APIs

Internal JSON-RPC over named pipe:

- `audit.run`
- `tweak.list|detect|apply|rollback`
- `backup.create|restore|list`
- `benchmark.run`
- `report.export`

CLI is a thin client of the same RPC (in-process when already elevated).

## Compatibility strategy

Each tweak declares `minBuild`, `maxBuild` (optional), editions, and `requires`. The engine refuses apply when the host build is outside range. Windows 11 25H2 (26200) is the **primary**. Windows 10 is **best-effort detect-only** unless a tweak is explicitly marked.

## What we learned from this host

- Unelevated Device Guard CIM works — audit is useful without UAC.
- `ProductName` lies — detection lives in Core.
- Dual GPU + VMware + Hyper-V + WSL means modules **must not fight**.
- C: free space is a pre-check for backups.

## Non-goals in architecture

- Cloud accounts
- Kernel driver in the core product
- ISO customization
- Remote execution against other PCs in v1
