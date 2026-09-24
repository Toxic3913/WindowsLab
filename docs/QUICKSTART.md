# Quick start

## From source (developers)

```text
git clone https://github.com/Toxic3913/WindowsLab.git
cd WindowsLab
dotnet test WindowsLab.sln -c Release
dotnet run --project src/WindowsLab.App
```

CLI:

```text
dotnet run --project src/WindowsLab.Cli -- --help
dotnet run --project src/WindowsLab.Cli -- live --output json
```

Publish portable + Setup bootstrapper:

```text
pwsh -File eng/publish.ps1
```

Outputs under `artifacts/` (gitignored): portable zip, `WindowsLab-Setup.exe`.

## From a release (users)

1. Open [Releases](https://github.com/Toxic3913/WindowsLab/releases).
2. Download **Setup** (`WindowsLab-Setup.exe`) or the **portable zip**.
3. Run the GUI unelevated. HKCU apply works without admin; HKLM needs **Allow system apply** (or a lab VM) and UAC (D020).

WindowsLab does **not** use `irm | iex`. Prefer GitHub Releases with checksums when published.

## First-run map (UI)

| Nav | Job |
| --- | --- |
| Inicio | Status + montajes + pendientes |
| Ajustes | Check tweaks → Aplicar once |
| Apps | winget install with confirmation |
| Rendimiento | Live CPU / RAM / processes |
| Más | Backups, system-apply gate, tools |

## Lab VM

Mutating system tests: machine name containing `WindowsLab-Test` (see [docs/testing/vm-lab.md](testing/vm-lab.md) if present, else AGENTS.md).
