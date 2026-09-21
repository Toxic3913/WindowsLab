# WindowsLab.Backup

Phase 3 — in solution (Beta 0.3 / D020).

Named backups under `%ProgramData%\WindowsLab\backups\<id>\manifest.json`. Exact inverse rollback. Restore points via `RestorePointService` (never silent `vssadmin`).
