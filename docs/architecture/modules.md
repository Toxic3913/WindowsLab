# Module requirements

Date: 2026-09-05

This is the contract for every project folder. **Only Core + Cli + their tests compile in Phase 1.** Other `src/*` folders exist so later work has a named home; they are not in the `.sln` until their phase.

Hardware-agnostic rules: [../requirements/hardware-agnostic.md](../requirements/hardware-agnostic.md).  
Pipeline: AUDIT → DETECT → ANALYZE → RECOMMEND → APPROVAL → BACKUP → APPLY → VERIFY → BENCHMARK.

## Shared rules (every module)

| Must | Must not |
| --- | --- |
| Probe/tweak results use `Ok\|Denied\|Unsupported\|Error\|Timeout` | Invent zeros or treat Access Denied as “off” |
| OS family from **build ≥ 22000**, never `ProductName` | Call `Win32_Product` |
| Tweaks: evidence grade; UNKNOWN → no apply | Generic `InvokeScript` in default catalog |
| Mutating tests on `WindowsLab-Test-25H2` | Tweaks on PC-HUGO |
| Facts drive recommendations | Hardcode 12600K / RTX 3060 / 16 GB / “always D:” |
| Spanish-capable messages OK; logs English-stable IDs | `irm \| iex`, ISO/TPM bypass, disable Defender by default |

---

## Phase 1 (in solution)

### `WindowsLab.Core`

| | |
| --- | --- |
| Purpose | Shared models, OS identity, data-root policy, interfaces for later engines |
| Depends on | none (no WinUI, no SQLite required yet) |
| TFM | `net10.0-windows` |
| Must | `OsIdentityMapper`: CurrentBuild ≥ 22000 ⇒ Windows 11 even if ProductName says “Windows 10”. `DataRootResolver`: `%ProgramData%\WindowsLab` on C:. Interfaces: `IAuditRunner`, `IBackupStore`, `ITweakCatalog`, `IAuditLog` as **empty contracts**. |
| Must not | WMI live calls in Core (inject later). Hardcoded host CPU/GPU. |
| Tests | `WindowsLab.Core.Tests` — mapper + data root with fakes |
| Acceptance | Tests prove the ProductName quirk; runtime data is ProgramData on C:, never the git repo |

### `WindowsLab.Cli`

| | |
| --- | --- |
| Purpose | `windowslab-cli.exe` — first engine client (never named `windowslab.exe`; collides with GUI on Windows) |
| Depends on | Core |
| Must | `--help` / `-h`. `audit --os` prints build, DisplayVersion, edition, `isWindows11` from Core. Exit `0` on success. JSON later (`--output json` stub may print “not implemented”). |
| Must not | Elevation prompt for `audit --os`. Apply tweaks. |
| Tests | `WindowsLab.Cli.Tests` — help text contains `audit`; OS mapper still unit-tested in Core |
| Acceptance | On PC-HUGO: build 26200, 25H2, Professional, isWindows11=true |

### `WindowsLab.Core.Tests` / `WindowsLab.Cli.Tests`

xUnit. No `[Live]` mutating tests. Live OS read for `audit --os` is allowed (read-only).

---

## Later phases (folders exist, not in `.sln`)

### `WindowsLab.Audit` — Phase 2

Read-only probe runner. Coverage = `ola.txt` + host-audit gaps (DXGI VRAM, topology, denied labels). Dual-path USER then admin. See [../AUDIT-ENGINE.md](../AUDIT-ENGINE.md).

### `WindowsLab.Backup` — Phase 3 / Beta 0.3 (D020)

Named backups under **data root** `backups\<id>\`. Exact inverse. Restore-point via Worker. Never silent `vssadmin`.

### `WindowsLab.Tweaks` — Phase 4 / Beta 0.3

Catalog load + lab HKCU apply + system apply eligibility. Rollback from captured backup (D005).

### `WindowsLab.Worker` — Phase 3–4 / Beta 0.3 (D020)

Elevated process only. Named-pipe JSON. No UI.

### `WindowsLab.Recommendations` — Phase 4–6

Profile + inventory facts → ranked tweak list. Beta 0.2 also ranks curated apps (multi-axis). No apply.

### `WindowsLab.Applications` — Beta 0.2 (D019)

Curated winget catalog loader, installed-app detect (Uninstall registry), install with explicit approval. Logs under ProgramData reports.

### `WindowsLab.Benchmarks` — Phase 6

DiskSpd / CPU protocols. Never invent FPS. GPU benches are host-opt-in later (VM has no RTX).

### `WindowsLab.App` — Phase 5

WinUI 3 unpackaged. Gated on elevation prototype (D011). Shell routes may stub.

### `WindowsLab.Plugins.Abstractions` — Phase 10

First-party ALC; third-party out-of-process fail-closed (D013).

### `catalog/`

JSON only. Validated against `schemas/`. Empty objects until Phase 2/4.

### `schemas/`

Source of truth for audit snapshot, tweak definition, backup manifest.

### `fixtures/`

Canned inventories. `host-pchugo.json` is an example profile, not production defaults.

### `eng/`

CI helpers. No nested Hyper-V on `windows-latest`.
