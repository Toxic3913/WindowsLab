# WindowsLab

Professional Windows 11 lab: audit, diagnose, optimize, configure, maintain, and recover — with evidence, backup, verification, and rollback.

**Status:** Phase 1 foundation. Engine is C# / .NET 10. Mutating tests go on a Windows 11 Pro VM, not this host.

Start here:

1. [docs/PROJECT-VISION.md](docs/PROJECT-VISION.md) — what we are building
2. [docs/architecture/modules.md](docs/architecture/modules.md) — requirements per folder
3. [docs/architecture/folder-structure.md](docs/architecture/folder-structure.md) — tree
4. [docs/architecture/data-root.md](docs/architecture/data-root.md) — repo on D:, program on C:
5. [docs/testing/vm-lab.md](docs/testing/vm-lab.md) — lab VM spec
6. [docs/ROADMAP.md](docs/ROADMAP.md) — phases

```text
dotnet test WindowsLab.sln
dotnet run --project src/WindowsLab.Cli -- audit --os
```


Canonical indexes:

- [docs/research/research-index.md](docs/research/research-index.md)
- [docs/architecture/decision-log.md](docs/architecture/decision-log.md)
