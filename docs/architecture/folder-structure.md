# Source tree

Date: 2026-09-06  
Status: **created in Phase 1**. Placeholder modules have a README and no production code until their phase.

Per-module requirements: [modules.md](modules.md). Data root: [data-root.md](data-root.md). Lab VM: [../testing/vm-lab.md](../testing/vm-lab.md).

```
D:\WindowsLab\                              # git repo / source / docs / tests
  AGENTS.md
  README.md
  WindowsLab.sln
  global.json                               # pin .NET 10 SDK
  Directory.Build.props                     # net10.0-windows, nullable, warnings
  .editorconfig
  .gitignore
  .github/workflows/ci.yml                  # unit tests only (no nested Hyper-V)

  docs/                                     # Phase 0 + living design
  schemas/                                  # JSON Schema for catalog + audit
  catalog/
    tweaks/                                 # Beta 0 catalog (detect-only)
    checklists/                             # baseline checklist (detect-only; no apply)
    presets/                                # named packs: perf.max, privacy, desktop, custom

    checks/                                 # audit check defs (empty until Phase 2)
    profiles/                               # gaming / developer / balanced
  config/
    defaults.json                           # dataRoot policy (not secrets)
  fixtures/                                 # canned inventories for tests
    host-pchugo.json
    vm-lab-25h2.json
  eng/                                      # scripts, not the product
  src/
    WindowsLab.Core/                        # models, OS identity, data root
    WindowsLab.Cli/                         # windowslab-cli.exe
    WindowsLab.Audit/                       # Beta 0 probes
    WindowsLab.Tweaks/                      # catalog + detect
    WindowsLab.Recommendations/             # ranking
    WindowsLab.App/                         # WPF Beta 0
    WindowsLab.Setup/                       # setup.exe bootstrapper
    WindowsLab.Backup/                      # Phase 3 — placeholder
    WindowsLab.Benchmarks/                  # Phase 6 — placeholder
    WindowsLab.Worker/                      # Phase 3–4 — placeholder
    WindowsLab.Plugins.Abstractions/        # Phase 10 — placeholder
  tests/
    WindowsLab.Core.Tests/                  # Phase 1
    WindowsLab.Audit.Tests/                 # Phase 2 — placeholder
    WindowsLab.Backup.Tests/                # Phase 3 — placeholder
    WindowsLab.Tweaks.Tests/                # Phase 4 — placeholder
    WindowsLab.Cli.Tests/                   # Phase 1 help/OS smoke

%ProgramData%\WindowsLab\                   # runtime on C: (NOT the git repo)
  backups\
  logs\
  reports\
  tmp\
```

**Do not** put backup blobs or SQLite logs inside `D:\WindowsLab`. VM disks stay under `D:\VM\`.

**Do not** add WinUI / Worker / tweak apply code until the matching phase. Empty folders + README are the contract.
