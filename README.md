# WindowsLab

[![CI](https://github.com/Toxic3913/WindowsLab/actions/workflows/ci.yml/badge.svg)](https://github.com/Toxic3913/WindowsLab/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/Toxic3913/WindowsLab?display_name=tag)](https://github.com/Toxic3913/WindowsLab/releases)

**WindowsLab 1.0** — configure and care for Windows 11 workstations with audit, curated apply, backup/rollback, and a live performance view.

Inspired by popular Windows utilities (e.g. [winutil](https://github.com/ChrisTitusTech/winutil)) for **UX density**, but **not a clone**: typed C# engine, evidence grades, no `irm | iex`, no default `InvokeScript` catalog.

## Quick start

- Users: [docs/QUICKSTART.md](docs/QUICKSTART.md) · [Releases](https://github.com/Toxic3913/WindowsLab/releases)
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
| Inicio | Status + montajes (Empresa / Lab / Gaming / …) + pendientes |
| Ajustes | Montaje marca checks → revisar → aplicar una vez (backup) |
| Apps | winget curado con búsqueda y confirmación |
| Rendimiento | CPU / RAM / disco / procesos en vivo |
| Más | Respaldos, apply de sistema, herramientas, sistema/seguridad |

System-wide (HKLM) changes require elevation and **Allow system apply** (or a lab VM). Defender realtime opt-out needs an extra confirmation (D021).

## Docs

- [Quick start](docs/QUICKSTART.md)
- [Roadmap](docs/ROADMAP.md)
- [Architecture decisions](docs/architecture/decision-log.md)
- [Security model](docs/SECURITY-MODEL.md) · [SECURITY.md](SECURITY.md)
- [Contributing](CONTRIBUTING.md)
- [EULA (ES)](docs/legal/EULA-es.txt) · [EULA (EN)](docs/legal/EULA-en.txt)
- [Agents](AGENTS.md)
