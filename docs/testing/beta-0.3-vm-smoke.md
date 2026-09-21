# Beta 0.3 VM smoke checklist

Target: `WindowsLab-Test-25H2` clone of `snap-clean-25h2` only. Never PC-HUGO.

| Step | Command / action | Expect |
| --- | --- | --- |
| 1 | Deploy App + CLI + Worker + catalog | Files present |
| 2 | `windowslab-cli audit --os` | Exit 0, build ≥ 22000 |
| 3 | `tweak apply explorer.show-file-extensions --lab-apply` | HKCU write + LocalAppData backup |
| 4 | Enable System Protection on C: | Ready for RP |
| 5 | `tweak apply developer.long-paths --apply --yes --i-am-on-lab-vm` | UAC → Worker → ProgramData backup |
| 6 | `backup list` | Shows backupId |
| 7 | `tweak rollback --backup-id … --i-am-on-lab-vm` | Value restored |
| 8 | GUI: Ajustes → Permitir apply de sistema → Apply pack Gaming | Confirm + UAC as needed |
| 9 | Revert VM snapshot | Clean state |

Refuse on host without `AllowSystemApply` / `--i-am-on-lab-vm` / name `WindowsLab-Test*`.
