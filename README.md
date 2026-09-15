# WindowsLab

Professional Windows 11 lab: audit, diagnose, optimize, configure, maintain, and recover — with evidence, backup, verification, and rollback.

**Status:** Beta 0 (read-only). WPF dashboard + CLI. Install: self-contained zip or `setup.exe`. Tweaks are **detected**, not applied.

```text
dotnet test WindowsLab.sln
dotnet run --project src/WindowsLab.App
dotnet run --project src/WindowsLab.Cli -- audit --output json
pwsh -File eng/publish.ps1
```



Canonical indexes:

- [docs/research/research-index.md](docs/research/research-index.md)
- [docs/architecture/decision-log.md](docs/architecture/decision-log.md)
