# AGENTS.md

WindowsLab is in **Beta 0 (read-only)**. Source lives in `D:\WindowsLab`. The installed program and runtime data live on **C:** (`%ProgramData%\WindowsLab`, `%LocalAppData%\WindowsLab`).

Before coding:

1. Read `docs/ROADMAP.md`, `docs/architecture/decision-log.md`, `docs/architecture/modules.md`.
2. Do not import ChrisTitusTech/winutil JSON as tweaks.
3. Do not add `InvokeScript` to the default catalog.
4. Do not call `Win32_Product`.
5. Do not trust `ProductName` / `WindowsProductName` for OS family. Use `CurrentBuild >= 22000`.
6. Do not apply tweaks on the host. Beta 0 is detect/simulate only. Mutating tests: VM `WindowsLab-Test-25H2`.
7. Grade evidence; if UNKNOWN or EXPERIMENTAL, do not recommend.
8. Do not hardcode this PC’s CPU/GPU/RAM. Facts + `requires[]` only.
9. Do not run `vssadmin` / System Restore changes on the host unless the operator asked.
10. Prefer cheap models for boilerplate, medium for Windows internals, independent review for tweak handlers.

Beta 0 scope: Core + Audit + Tweaks.detect + Recommendations + Cli + WPF App + installer. **No** Worker apply, no backup writes, no WinUI yet (D017).

CLI: prefer `--output json`. `windowslab tweak apply` and `windowslab preset apply` must exit 13. `windowslab checklist` and `windowslab preset list|show|simulate` are read-only.
