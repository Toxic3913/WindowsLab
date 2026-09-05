# WindowsLab.Backup

Phase **3** — not in the solution yet.

Named backups under `%ProgramData%\WindowsLab\backups\<id>\` (C:, with the installed program). Exact inverse rollback. Restore points may **recommend** VSS `/For=C: /On=D:`; never apply `vssadmin` silently.
