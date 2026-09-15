# AGENTS.md

WindowsLab is in **Beta 0.2 (lab apply HKCU + apps winget)**. Source lives in `D:\WindowsLab`. The installed program and runtime data live on **C:** (`%ProgramData%\WindowsLab`, `%LocalAppData%\WindowsLab`).

Before coding:

1. Read `docs/ROADMAP.md`, `docs/architecture/decision-log.md`, `docs/architecture/modules.md`.
2. Do not import ChrisTitusTech/winutil JSON as tweaks or applications.
3. Do not add `InvokeScript` to the default catalog.
4. Do not call `Win32_Product`.
5. Do not trust `ProductName` / `WindowsProductName` for OS family. Use `CurrentBuild >= 22000`.
6. Prefer mutating tests on VM `WindowsLab-Test-25H2`. Lab apply is **HKCU only** (LOW + OFFICIAL/STRONG); never silent HKLM. Do not kill Defender/DiagTrack/SysMain/Search.
7. Grade evidence; if UNKNOWN or EXPERIMENTAL, do not recommend or apply/install.
8. Do not hardcode this PC’s CPU/GPU/RAM. Facts + `requires[]` only.
9. Do not run `vssadmin` / System Restore changes on the host unless the operator asked.
10. Prefer cheap models for boilerplate, medium for Windows internals, independent review for tweak handlers.

Beta 0.2 scope: Core + Audit + Tweaks.detect + **Tweaks.lab-apply (HKCU)** + Recommendations (tweaks + apps) + **Applications (winget, curated)** + dual theme (dark/light/system) + Cli + WPF App + installer. **No** elevated Worker process, no HKLM tweak apply, no WinUI yet (D017/D018/D019). App install may prompt UAC via `runas` when winget requires it.

CLI: prefer `--output json`. Executable is `windowslab-cli.exe` (never `windowslab.exe` — that name collides with GUI `WindowsLab.exe` on Windows). `windowslab-cli tweak apply` without `--lab-apply` and `windowslab-cli preset apply` must exit 13. `windowslab-cli app install` requires `--yes`. `windowslab-cli checklist` and `windowslab-cli preset list|show|simulate` are read-only.
