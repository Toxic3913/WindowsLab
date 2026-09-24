# Contributing to WindowsLab

## Build and test

```text
dotnet test WindowsLab.sln -c Release
dotnet build src/WindowsLab.App/WindowsLab.App.csproj -c Release
dotnet build src/WindowsLab.Setup/WindowsLab.Setup.csproj -c Release
pwsh -File eng/publish.ps1
```

CLI binary name is `windowslab-cli.exe` (never `windowslab.exe`). Prefer `--output json` for automation.

## Product rules (non-negotiable)

See [AGENTS.md](AGENTS.md) and [docs/architecture/decision-log.md](docs/architecture/decision-log.md).

- Do not import ChrisTitusTech/winutil JSON as tweaks or apps.
- Do not add `InvokeScript` to the default catalog.
- Do not call `Win32_Product`. OS family = `CurrentBuild >= 22000`.
- Never mutate DiagTrack / SysMain / WSearch. Defender: curated HIGH tweak only (D021), no process kill of `MsMpEng.exe`.
- UNKNOWN / EXPERIMENTAL evidence: do not recommend or apply. `affectsSecurity: true` stays out of default recommendations.
- System apply (HKLM / Worker) is gated (D020): settings flag, lab VM name, or `--i-am-on-lab-vm`.

## Git hygiene

- Do not commit `artifacts/`, `bin/`, `obj/`, installers, zips, PDBs, or local logs.
- Keep catalogs under `catalog/` declarative and reviewed.

## Pull requests

- Prefer small PRs with `dotnet test` green.
- Mutating system tests: VM `WindowsLab-Test-25H2` when possible.
