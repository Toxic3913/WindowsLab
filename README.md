# WindowsLab

**WindowsLab 1.0** — configure and care for Windows 11 workstations.

Audit hardware and security posture, apply curated tweaks with backup and rollback, install recommended apps via winget, mount machine stacks (Empresa / Pruebas / Gaming / Dev), and watch live CPU / RAM / disk / processes.

## Install

- Setup: `artifacts/installer/WindowsLab-Setup.exe` (or Inno `WindowsLab-Setup-Inno.exe`)
- Portable: `artifacts/zip/WindowsLab-portable-win-x64.zip`
- From source:

```text
dotnet test WindowsLab.sln
dotnet run --project src/WindowsLab.App
dotnet run --project src/WindowsLab.Cli -- --help
pwsh -File eng/publish.ps1
```

## What you get

| Area | Capability |
| --- | --- |
| Home | Quick modes + **Empresa** / **Pruebas** stacks |
| Packs / Stacks | Curated tweaks; apply HKCU or system (opt-in) |
| Apps | winget catalog with confirmation |
| Performance | Live CPU, RAM, disks, processes (Windows / Microsoft / External) |
| Backups | ProgramData / LocalAppData manifests + rollback |
| Menu | File / View / Tools / Help |

System-wide (HKLM) changes require elevation and **Allow system apply** (or a lab VM). Defender realtime opt-out needs an extra confirmation (D021).

## Docs

- [Roadmap](docs/ROADMAP.md)
- [Architecture decisions](docs/architecture/decision-log.md)
- [Contributing](CONTRIBUTING.md)
- [EULA (ES)](docs/legal/EULA-es.txt) · [EULA (EN)](docs/legal/EULA-en.txt)
- [Agents](AGENTS.md)
