# AGENTS.md

WindowsLab is **1.0.0** (audit + configure + apply with backup + live Performance). Source lives in `D:\WindowsLab`. Runtime data: `%ProgramData%\WindowsLab`, `%LocalAppData%\WindowsLab`.

Before coding:

1. Read `docs/ROADMAP.md`, `docs/architecture/decision-log.md`, `docs/architecture/modules.md`.
2. Do not import ChrisTitusTech/winutil JSON as tweaks or applications.
3. Do not add `InvokeScript` to the default catalog.
4. Do not call `Win32_Product`.
5. Do not trust `ProductName` / `WindowsProductName` for OS family. Use `CurrentBuild >= 22000`.
6. Prefer mutating tests on VM `WindowsLab-Test-25H2`. **System apply** (HKLM / Worker) is gated: `AllowSystemApply` in settings, machine name containing `WindowsLab-Test`, or CLI `--i-am-on-lab-vm` (D020). Lab apply HKCU remains available unelevated.
7. **Protected services (hard):** never mutate DiagTrack / SysMain / WSearch (or kill their processes). **Defender:** no process kill of `MsMpEng.exe`; optional curated HIGH tweak `security.defender-realtime-off` only (D021) — registry policy, never recommended, double confirmation + system-apply gate. Generic `sc`/service ops on WinDefend/Sense/Wd* stay blocked.
8. Grade evidence; if UNKNOWN or EXPERIMENTAL, do not recommend or apply/install. Tweaks with `affectsSecurity: true` must not appear in default recommendations.
9. Do not hardcode this PC’s CPU/GPU/RAM. Facts + `requires[]` only.
10. Do not run `vssadmin` silently on the host. Restore points may be attempted by the elevated Worker on the lab VM (D020).
11. Prefer cheap models for boilerplate, medium for Windows internals, independent review for tweak handlers.

1.0 scope: Core + Audit + Tweaks + Backup + Worker + Recommendations + Applications + stacks (empresa/pruebas) + dual theme + Cli + WPF App (5 destinations: Inicio/Ajustes/Apps/Rendimiento/Más, D024) + Performance live dashboard (D022/D023). Lab `--lab-apply` = HKCU. `--apply` / GUI Apply = system pipeline when allowed. No WinUI yet.

CLI: prefer `--output json`. Executable is `windowslab-cli.exe` (never `windowslab.exe`). `live [--output json]` metrics. `tweak apply` without `--lab-apply` or `--apply` → exit 13. `preset apply` requires `--yes`. `app install` requires `--yes`. `backup list`, `tweak rollback --backup-id` available.
