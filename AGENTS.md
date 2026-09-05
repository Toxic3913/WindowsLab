# AGENTS.md

WindowsLab is in **Phase 1 (foundation)**. Source lives in `D:\WindowsLab`. The installed program and runtime data live on **C:** (`%ProgramData%\WindowsLab`, `%LocalAppData%\WindowsLab`) — [docs/architecture/data-root.md](docs/architecture/data-root.md).

Before coding:

1. Read `docs/ROADMAP.md`, `docs/architecture/decision-log.md`, `docs/architecture/modules.md`, `docs/architecture/folder-structure.md`.
2. Do not import ChrisTitusTech/winutil JSON as tweaks.
3. Do not add `InvokeScript` to the default catalog.
4. Do not call `Win32_Product`.
5. Do not trust `ProductName` / `WindowsProductName` for OS family. Use `CurrentBuild >= 22000`.
6. Do not test mutating tweaks on the host. Use VM `WindowsLab-Test-25H2` ([docs/testing/vm-lab.md](docs/testing/vm-lab.md)). Existing `D:\VM\Windows 11 x64` is dirty until proven otherwise.
7. Grade evidence; if UNKNOWN, do not apply.
8. Do not hardcode this PC’s CPU/GPU/RAM. Facts + `requires[]` only.
9. Do not run `vssadmin` / System Restore changes on the host unless the operator asked.
10. Prefer cheap models for boilerplate, medium for Windows internals, independent review for tweak handlers.

Phase 1 scope: Core + Cli + tests. Do **not** start WinUI, Worker, or tweak apply until the matching phase.

CLI for agents: `windowslab audit --os` is read-only. Prefer JSON when `--output json` exists.
