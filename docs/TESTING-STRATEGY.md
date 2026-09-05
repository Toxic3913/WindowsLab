# TESTING STRATEGY

Date: 2026-09-05

## Principle

**Never validate tweaks on PC-HUGO first.** The host is the operator's daily driver (Steam, Docker, VPN, Cursor). Default fabric: dedicated VM **WindowsLab-Test-25H2** ([vm-lab.md](testing/vm-lab.md)). Existing `D:\VM\Windows 11 x64` is possibly dirty. Hyper-V / Windows Sandbox are optional later.

## Layers

| Layer | Tool | What |
| --- | --- | --- |
| Unit | xUnit | schema, detect parsers, recommendation ranking, ProductName quirk |
| Contract | json-schema | catalog files |
| Integration (mocked CIM) | xUnit + fakes | executor pipeline |
| Integration (VM) | manual + scripts | real apply/rollback |
| Pester | optional | any PS adapters |
| UI | WinAppDriver / Appium later | smoke |
| CI | GitHub Actions `windows-latest` | unit + schema **only** (no live tweaks) |

## Why CI cannot apply tweaks

GitHub-hosted Windows runners are shared images. Mutating services/WU/Defender is hostile and non-reproducible. CI = compile + unit + catalog lint.

## VM protocol (acceptance for each HIGH tweak)

1. Snapshot `snap-clean-25h2` (see [vm-lab.md](testing/vm-lab.md))
2. Copy unpackaged build
3. `windowslab audit --format json`
4. `windowslab tweak apply <id>`
5. Reboot if required
6. `detect` + `verify`
7. `rollback`
8. Diff audit
9. Revert snapshot

Operator already has a 25H2 Spanish ISO in `D:\VM\COMPARTIDO`. Prefer a **clean test VM** over the possibly dirty current VM.

## Windows Sandbox

Good for: unelevated audit, CLI smoke, `sandbox-safe` tweaks.  
Bad for: persistence, Hyper-V nested, GPU gaming, VMware.

Win11 24H2+: `wsb.exe` CLI exists. As of 24H2 docs, **`wsb exec` has no stdout capture** — tests must write NDJSON to a shared folder. Sandbox is **Pro+**, not Home.

Hyper-V on this host: cmdlets exist, permission denied unelevated. Do not fight VMware; both can coexist.

## CI limits (2026-09)

Standard GitHub `windows-latest` **does not nest Hyper-V** ([runner-images #9285](https://github.com/actions/runner-images/discussions/9285)). PR CI = unit + schema. Snapshot E2E stays on this lab (VMware) or a self-hosted runner.

Windows 10 Home/Pro EOS **2025-10-14** — mutating support is not worth the matrix (D006).

## Compatibility matrix

| Edition | v1 support |
| --- | --- |
| Windows 11 Pro 25H2 (26200) | **primary** (this host + ISO) |
| Windows 11 Home | audit yes; some policies absent — per-tweak `editions` |
| Windows 11 Enterprise / Education | should work; CompositionEditionID already Enterprise-like on this Pro |
| Windows 11 IoT | **out of scope** |
| Windows 10 22H2 | detect-only unless a tweak opts in |
| Insider / 26H2 | catalog `maxBuild` unknown; refuse rather than guess |

Each tweak: `editions[]`, `minBuild`, optional `maxBuild`.

## Safety tests that must exist before any apply code merges

- Executor refuses unknown op types
- Executor refuses CRITICAL without backup
- Rollback uses backup previous state, not catalog defaults
- `Win32_Product` never called
- ProductName not used for OS detection
- Dry-run writes no HKLM

## Flaky hardware tests

SMART, NVAPI, PresentMon: tagged `[Hardware]` and skipped in CI.
