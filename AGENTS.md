# AGENTS.md

WindowsLab is in **Beta 0.3 (system apply + lab HKCU + apps winget)**. Source lives in `D:\WindowsLab`. The installed program and runtime data live on **C:** (`%ProgramData%\WindowsLab`, `%LocalAppData%\WindowsLab`).

Before coding:

1. Read `docs/ROADMAP.md`, `docs/architecture/decision-log.md`, `docs/architecture/modules.md`.
2. Do not import ChrisTitusTech/winutil JSON as tweaks or applications.
3. Do not add `InvokeScript` to the default catalog.
4. Do not call `Win32_Product`.
5. Do not trust `ProductName` / `WindowsProductName` for OS family. Use `CurrentBuild >= 22000`.
6. Prefer mutating tests on VM `WindowsLab-Test-25H2`. **System apply** (HKLM / Worker) is gated: `AllowSystemApply` in settings, machine name containing `WindowsLab-Test`, or CLI `--i-am-on-lab-vm`. Lab apply HKCU remains available unelevated. Do not kill Defender/DiagTrack/SysMain/Search.
7. Grade evidence; if UNKNOWN or EXPERIMENTAL, do not recommend or apply/install.
8. Do not hardcode this PC’s CPU/GPU/RAM. Facts + `requires[]` only.
9. Do not run `vssadmin` silently on the host. Restore points may be attempted by the elevated Worker on the lab VM (D020).
10. Prefer cheap models for boilerplate, medium for Windows internals, independent review for tweak handlers.

Beta 0.3 scope: Core + Audit + Tweaks detect/apply + **Backup** (`%ProgramData%\WindowsLab\backups`) + **Worker** (named pipe, UAC) + Recommendations + Applications + dual theme + Cli + WPF App. Lab `--lab-apply` = HKCU only. `--apply` / GUI Apply = system pipeline. No WinUI yet.

CLI: prefer `--output json`. Executable is `windowslab-cli.exe` (never `windowslab.exe`). `tweak apply` without `--lab-apply` or `--apply` → exit 13. `preset apply` requires `--yes`. `app install` requires `--yes`. `backup list`, `tweak rollback --backup-id` available.
