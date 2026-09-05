# Source vs install vs runtime

Date: 2026-09-06  
Decisions: [D014](decision-log.md) (superseded in part), [D016](decision-log.md)

The operator asked to keep **work** off C: (~67 GB free). That means the **git repo**, not the installed app.

| What | Where | Why |
| --- | --- | --- |
| Source, docs, tests, catalog | **`D:\WindowsLab`** | Large, grows, not needed to boot Windows |
| Installed program (unpackaged EXE later) | **C:** — `%LocalAppData%\Programs\WindowsLab` or `C:\Program Files\WindowsLab` | Normal Windows app |
| Runtime artifacts (backups, logs, reports) | **C:** — `%ProgramData%\WindowsLab` | Follows the program |
| Settings | **C:** — `%LocalAppData%\WindowsLab` | Per-user, small |
| Lab VM disks | **`D:\VM\...`** | Already on D: |

Do **not** mix git (`D:\WindowsLab`) with `%ProgramData%\WindowsLab`.

## Runtime layout (C:)

```
%ProgramData%\WindowsLab\
  backups\<backupId>\manifest.json
  logs\windowslab.db
  reports\
  tmp\
```

## VSS (restore points) — optional, not the repo

C: is still tight. Enabling System Protection **on D:** only snapshots D:; it does **not** protect Windows.

To store **C:** restore-point data on D: (admin, explicit):

```text
vssadmin list shadowstorage
vssadmin add shadowstorage /For=C: /On=D: /MaxSize=20GB
```

WindowsLab may **recommend** this later. It does not apply `vssadmin` unless the operator asks.

## Pre-check (Phase 3)

- Warn if C: free < 20 GB (restore points and ProgramData backups)
- Block restore-point creation if there is no room
