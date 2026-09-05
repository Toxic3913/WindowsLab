# BACKUP AND RECOVERY

Date: 2026-09-05

## Goal

No MEDIUM+ tweak runs without a **named backup** that the engine can restore, or a hard fail in pre-check.

## Pipeline

```
PRE-CHECK → BACKUP → CHANGE → VERIFY → (optional) ROLLBACK
```

### PRE-CHECK

- Disk free on C: (this host is low — **warn below 20 GB**, **block** restore-point creation below a configurable floor). Prefer WindowsLab artifacts on **D:** (D014). Optionally recommend VSS shadow storage `/For=C: /On=D:` so OS restore points do not fill C:. Never apply `vssadmin` without approval.
- Pending reboot: prefer Microsoft DSC resource `Microsoft.Windows/RebootPending` semantics if we take a dependency; otherwise implement only from documented CBS/WU keys. Still **verify on 25H2** (NEEDS_RESEARCH for the exact key set we ship).
- `reagentc /info` for HIGH+ (WinRE present)
- BitLocker: confirm recovery material exists **off-box**; never copy recovery keys into the backup folder
- VSS writer health if creating a restore point
- Elevation available
- Conflicting tweaks
- Snapshot of `detect()`
- System Restore service available if we plan a restore point

### BACKUP (layered)

A Backup is a directory `%ProgramData%\WindowsLab\backups\<backupId>\` plus a `manifest.json` ([architecture/data-root.md](architecture/data-root.md)). The git repo on D: is not a backup store.

| Layer | Mechanism | Official? | Limits |
| --- | --- | --- | --- |
| Restore point | `Checkpoint-Computer` / SR API | OFFICIAL | **Max one per 24 hours** via that cmdlet; not a full image; needs System Protection on |
| Registry keys | Per-value previous type/data **and** whether the value existed | OFFICIAL APIs | `reg export` is insufficient: import **merges** and does not delete later values; **ACLs omitted** |
| Services | `sc qc`, `qfailure`, `qtriggerinfo`, `sdshow` + runtime | OFFICIAL `sc` | `Get-Service` is not a backup |
| Scheduled tasks | `Export-ScheduledTask` XML (the baseline script already does this globally) | OFFICIAL | Some tasks refuse export |
| Firewall | `netsh advfirewall export` | OFFICIAL | Whole store, not per-rule delta |
| Power plan | `powercfg -export` | OFFICIAL | GUID based |
| Network | `Get-NetIPConfiguration` + `netsh dump` snapshot | STRONG | Restore is hard; treat as **document** unless we have exact reverse ops |
| BCD | `bcdedit /export` | OFFICIAL | CRITICAL; rare |
| GPO / local policy | `secedit /export` / LGPO.exe if present | OFFICIAL / tool | WORKGROUP has local policy only |
| Drivers | `pnputil /export-driver` optional | OFFICIAL | Large; not default |
| Logical snapshot | JSON of all `detect()` outputs in the job | ours | Fast, always |

**System Restore is necessary but not sufficient.** It is coarse, time-limited, and can miss some HKCU / service states. Always take **logical + registry export** even when a restore point succeeds.

If restore point creation fails (24h limit, System Protection off), MEDIUM tweaks may continue with logical backup; HIGH/CRITICAL **stop** unless `--no-restore-point` is explicitly set for HIGH only. CRITICAL never skips.

### CHANGE

Worker applies ops. Partial failure triggers **automatic rollback** of ops already done in reverse order, then a verify.

### VERIFY

Re-run `detect()` and compare to `desired`. Mismatch = failed job.

### ROLLBACK

1. Prefer reversing ops using **manifest previous values**
2. Else per-value restore (create/delete as recorded). Whole `.reg` import is disaster-only
3. Else restore task XML
4. Else restore point (user-initiated, disruptive)
5. If BCD involved: restore BCD export only with CRITICAL UI

## Mapping to user-requested backups

| Request | WindowsLab approach |
| --- | --- |
| Restore point | Layer 1 |
| Logical snapshots | Always |
| Registry | Per-value previous state (not whole-key merge) |
| Services | JSON + sc config restore |
| Tasks | XML |
| Firewall | netsh export |
| GPO | secedit/LGPO if available |
| BCD | export on CRITICAL boot jobs only |
| Network | snapshot + documented reverse; **no blind `int ip reset`** |
| Power plans | powercfg export/import |
| Drivers | optional, slow |

## Recovery module (beyond tweaks)

- Surface `rstrui.exe` (System Restore UI)
- List WindowsLab backups
- `windowslab backup restore <id>`
- Do **not** wrap `bootrec` / `bcdboot` in v1 without a dedicated CRITICAL recovery wizard and VM tests

## wbadmin / system image

Microsoft documents `wbadmin` but lists **System Image Backup as deprecated** (not actively developed). WindowsLab may invoke it as an **optional** operator-chosen backend after warning. It is **not** the sole CRITICAL recovery guarantee (D012).

## This host

System Protection state: **UNKNOWN** (not queried unelevated). Phase 1 audit probe must include it. C: free space may block restore points — pre-check is mandatory.
