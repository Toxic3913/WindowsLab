# WinUtil analysis (ChrisTitusTech/winutil)

Date: 2026-09-05  
Sources: GitHub API, README, official architecture page, `config/tweaks.json` (structure only — not copied into WindowsLab).

| Field | Observed |
| --- | --- |
| URL | https://github.com/ChrisTitusTech/winutil |
| Docs | https://winutil.christitus.com/ |
| Architecture | https://winutil.christitus.com/code-reference/architecture/ |
| Stars | 61,948 |
| Forks | 3,625 |
| License | MIT |
| Language | PowerShell |
| Last push | 2026-09-04 |
| Open issues | 21 |
| Latest release | 26.08.19 (2026-08-19) |

`/dev/architecture/` currently 404; use `/code-reference/architecture/`.

Approximate catalog mix on main (specialist count, 2026-09-05): ~50 registry-bearing tweaks, ~2 service-bearing, ~26 `InvokeScript`, ~20 `UndoScript`. First-class `scheduledtask` action **not** found in tweaks.json (tasks handled in update workflows/scripts). AppX executor exists; lowercase `appx` actions may be sparse in current JSON.

Undo: `"OriginalValue": " "` means **delete on undo**. AppX removal is not reversed by the tweak executor. A restore-point tweak can run **before** other selected tweaks in a run — defense in depth, **not** a transaction.

Pester covers configs, routing, runspaces, logging, AppX, ISO; **does not** prove mutations on every Windows 11 build. Analyzer step appears advisory. SHA-256 on GitHub releases; Authenticode still described as future.

## Architecture (STRONG EVIDENCE — project docs Jan 2026)

WinUtil is **PowerShell 5.1+ + WPF XAML**, compiled by `Compile.ps1` into a single `winutil.ps1`. Distribution is `irm https://christitus.com/win | iex` as Administrator.

Layers:

1. WPF GUI (`xaml/inputXML.xaml`)
2. Public/private functions under `functions/`
3. JSON catalogs: `applications.json`, `tweaks.json`, `feature.json`, `preset.json`, `dns.json`
4. Shared `$sync` hashtable across runspaces
5. STA UI runspace + worker runspace pool
6. External package managers: WinGet, Chocolatey

Threading is one of the strongest parts: UI never does long work; `Start-WinUtilJob` owns busy state, progress, logging.

IDs are **GUI-bound**: `WPFTweaksTelemetry`, `WPFInstallGoogleChrome`. Checkboxes map to JSON keys by name.

## Tweak model

From `tweaks.json` and architecture docs, a tweak can contain:

- `Content`, `Description`, `category`, `panel`, `link`
- `registry[]` with `Path`, `Name`, `Type`, `Value`, `OriginalValue`
- `service[]` with `StartupType` / `OriginalType`
- `InvokeScript` / `UndoScript` (arbitrary PowerShell)
- scheduled tasks and AppX in some entries

**Undo is mostly static `OriginalValue`**, not a captured pre-change snapshot. Empty `OriginalValue: " "` appears in real entries (e.g. Activity History). Hibernation tweak both writes registry and runs `powercfg /hibernate off`. Widget "tweak" is an AppX removal script with **no UndoScript** in the snippet we saw. Store-search tweak uses `icacls /deny` on `store.db` — clever but brittle and not a documented Windows setting.

Presets: Standard / Minimal / Advanced via `-Preset`. Export/import of selected keys exists; unknown keys can reject an import.

## What is well designed (keep conceptually)

1. **Declarative catalogs** separate from engine code.
2. **Compile-to-one-file** for easy first-run (we will not copy `irm | iex`).
3. **Worker vs UI threads.**
4. **Pester tests on JSON config** (`pester/configs.Tests.ps1`).
5. **GitHub Actions** producing the compiled artifact.
6. **Headless `-Config` / `-Preset`** for automation.
7. **Docs generated from JSON** (single source of truth for descriptions).
8. **Logging** to `%LocalAppData%\winutil\logs\`.

## What is limited

| Limitation | Why it matters |
| --- | --- |
| No detect/desired/actual state machine | Applying twice or on a modified PC is undefined |
| Static OriginalValue | Undo can write the wrong "factory" value |
| InvokeScript is unbounded | Hard to review, test, sandbox |
| IDs coupled to WPF names | Cannot share a clean CLI/API |
| No backup/restore point pipeline | Undo ≠ backup |
| No verification step | Success = script did not throw |
| No benchmark | "Essential Tweaks" is marketing, not measurement |
| Always-admin | Audit-only work still requires elevation |
| `irm \| iex` | Supply-chain and execution-policy issues |
| Win11 Creator ISO path | High blast radius (TPM bypass, BitLocker disable in offline image) — **out of scope for WindowsLab** |
| Commercial .NET "Windows Toolbox" upsell | Split brain: OSS PS vs paid rewrite |

## Security notes

- Open source, MIT, auditable — good.
- Remote script execution as the documented happy path — bad for a professional tool.
- InvokeScript can stop processes and remove AppX for all users.
- Digital signing called "future" in their own architecture doc (as of Jan 2026).
- ISO hardware-bypass tweaks are explicitly documented. WindowsLab must **not** clone this.

## Testing

Pester 5.8 exists for configs/functions. No evidence of VM snapshot CI on GitHub-hosted runners applying tweaks. **UNKNOWN** whether they test on 25H2 specifically; they do document 25H2 Start Menu tweak as version-fragile.

## What WindowsLab should do differently

1. Engine in C# with a versioned tweak schema; PowerShell only as an adapter.
2. Live **detect** before apply; store **observed previous state** in the backup record.
3. Risk, evidence grade, reboot, security impact, update impact as first-class fields.
4. PRE-CHECK → BACKUP → CHANGE → VERIFY → ROLLBACK.
5. Unelevated GUI; elevate per operation.
6. Signed releases (MSIX or Authenticode), never `iex` of a URL as the primary install.
7. Recommendations from hardware + profile, not a global "Essential" list.
8. No ISO rewriter in v1.

## Verdict

WinUtil is an excellent **distribution and UX** lesson and a weak **safety and evidence** lesson. Reuse the catalog idea, job/UI split, and JSON-driven docs. Do not reuse the tweak execution model, ID scheme, or delivery mechanism.
