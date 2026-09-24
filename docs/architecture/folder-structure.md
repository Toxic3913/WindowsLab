# Source tree

Date: 2026-09-24  
Status: **1.0 product tree** (no empty phase stubs in `src/` / `tests/`).

Per-module requirements: [modules.md](modules.md). Data root: [data-root.md](data-root.md). Lab VM: [../testing/vm-lab.md](../testing/vm-lab.md).

```
D:\WindowsLab\                              # git repo / source / docs / tests
  CONTRIBUTING.md
  SECURITY.md
  README.md
  LICENSE
  WindowsLab.sln
  global.json                               # pin .NET 10 SDK
  Directory.Build.props                     # net10.0-windows, nullable, warnings
  .editorconfig
  .gitignore
  .gitattributes
  .github/
    workflows/ci.yml                        # test + App + Setup (path-filtered)
    workflows/release.yml                   # tag v* → portable zip (+ Setup)
    ISSUE_TEMPLATE/                         # bug + feature
    PULL_REQUEST_TEMPLATE.md
    dependabot.yml
  docs/
    QUICKSTART.md
    SECURITY-MODEL.md
    # … living design
  schemas/                                  # JSON Schema for catalog + audit
  catalog/
    tweaks/
    checklists/
    presets/                                # packs + stacks (empresa, pruebas, …)
    applications/
    checks/
    profiles/
  config/
    defaults.json
  fixtures/
    host-pchugo.json
    vm-lab-25h2.json
  eng/                                      # publish / Inno / scripts
  assets/                                   # icon only (no installers)
  src/
    WindowsLab.Core/
    WindowsLab.Cli/                         # windowslab-cli.exe
    WindowsLab.Audit/
    WindowsLab.Tweaks/
    WindowsLab.Recommendations/
    WindowsLab.Applications/
    WindowsLab.Backup/
    WindowsLab.Worker/
    WindowsLab.App/                         # WPF shell
    WindowsLab.Setup/                       # setup bootstrapper
  tests/
    WindowsLab.Core.Tests/                  # includes Audit/Tweaks coverage
    WindowsLab.Backup.Tests/
    WindowsLab.Cli.Tests/

%ProgramData%\WindowsLab\                   # runtime on C: (NOT the git repo)
  backups\
  logs\
  reports\
  tmp\
```

**Do not** put backup blobs, installers, or SQLite logs inside `D:\WindowsLab`. VM disks stay under `D:\VM\`. Publish output goes to `artifacts/` (gitignored).

Planned modules (no folders until started): Benchmarks (Phase 6), Plugins.Abstractions (Phase 10) — see [modules.md](modules.md).
