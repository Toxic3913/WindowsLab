# TWEAK ENGINE

Date: 2026-09-05

## Problem with the WinUtil model

A tweak is not a checkbox. It is a **state transition** on a versioned OS, with dependencies, evidence, and a measured outcome.

WinUtil stores `Value` + `OriginalValue`. That is a patch, not a resource.

## Design: desired-state resource

Each catalog item is a **Tweak** with:

| Field | Required | Purpose |
| --- | --- | --- |
| `id` | yes | Stable dotted id: `privacy.telemetry.allow` — **never** `WPFTweaks…` |
| `name`, `description` | yes | User-facing |
| `category` | yes | privacy, gaming, power, … |
| `evidence` | yes | OFFICIAL / STRONG / COMMUNITY / EXPERIMENTAL |
| `evidenceRefs[]` | yes if OFFICIAL/STRONG | URLs |
| `risk` | yes | See risk model |
| `impact` | yes | What the user should notice |
| `sideEffects[]` | yes | Honest list, can be empty |
| `affectsSecurity` | yes | bool |
| `affectsUpdates` | yes | bool |
| `affectsCompatibility` | yes | bool |
| `requiresReboot` | yes | bool or `maybe` |
| `supported` | yes | builds, editions, SKUs |
| `requires[]` | no | other tweak ids, features, hardware facts |
| `conflicts[]` | no | mutually exclusive ids |
| `profiles[]` | no | gaming, developer, … |
| `detect` | yes | how to read actual state |
| `desired` | yes | target state for apply |
| `apply` | yes | typed operations, not a script blob |
| `rollback` | yes | must reconstruct from **backup record**, not from catalog OriginalValue |
| `verify` | yes | post-condition |
| `benchmark` | no | protocol id if we claim performance |
| `handler` | no | C# handler name for non-declarative ops |

The user's example JSON is a **good starting point**. It is **not** final. Additions that we require: `evidence`, `affects*`, `conflicts`, `handler`, and **no** `OriginalValue` in the catalog.

## Allowed apply operations (declarative)

```json
{
  "ops": [
    { "type": "registry.set", "hive": "HKLM", "path": "...", "name": "...", "valueKind": "DWord", "value": 0 },
    { "type": "service.setStart", "name": "lfsvc", "start": "Demand" },
    { "type": "scheduledTask.set", "path": "\\", "name": "...", "enabled": false },
    { "type": "powercfg.setActive", "guid": "..." },
    { "type": "firewall.profile", "profile": "Private", "enabled": true },
    { "type": "feature.set", "name": "...", "state": "Enabled" }
  ]
}
```

Anything else is a **named C# handler** with its own tests. AppX removal is a handler with `reversibility: partial`.

**Forbidden in default catalog:** `Invoke-Expression`, unbounded script, `icacls /deny` on Store databases, ISO/registry-offline edits.

## Detect / actual / desired

```
detect() -> TweakState { Actual, Sources[], Confidence }
plan(desired, actual) -> Op[]
precheck(op[]) -> PrecheckResult
backup(op[]) -> BackupId
apply(op[]) -> ApplyResult
verify() -> VerifyResult
```

If `actual == desired`, apply is a no-op (success, `changed: false`).

## Risk model

Severity alone is not enough. Use a **severity** plus **axes**:

### Severity (what the user sees)

| Level | Meaning | Default UI gate |
| --- | --- | --- |
| SAFE | No persistence / read-only | none |
| LOW | User-level, easy undo | confirm |
| MEDIUM | HKLM or service, full backup | confirm + backup |
| HIGH | Security, networking, boot-adjacent | typed confirm + backup + restore point |
| CRITICAL | BCD, HVCI, BitLocker, Defender real-time, boot | dual confirm, no profiles auto-include |
| EXPERIMENTAL | Evidence < STRONG | same as HIGH + benchmark required to claim gain |

### Orthogonal axes (always displayed)

- `reversibility`: full | partial | none
- `securityImpact`: none | reduces | increases
- `updateImpact`: none | may_defer | may_break_wu
- `compatImpact`: none | apps | games | virt | drivers
- `reboot`: none | required | unknown

Disabling Defender real-time is **CRITICAL** + `securityImpact: reduces` + never in Balanced/Developer/Gaming defaults.

## Recommendation pipeline

```
AUDIT → DETECT → ANALYZE → RECOMMEND → USER APPROVAL → BACKUP → APPLY → VERIFY → BENCHMARK?
```

Recommendations are **ranked suggestions**, each pointing at tweak ids. There is no `optimize --all` without `--profile` **and** `--i-understand`. CLI `windowslab optimize` is an alias for `recommend --apply-approved` only after an explicit approval file.

## Profiles (host-aware)

Detected facts on PC-HUGO would include: desktop, 16 GB RAM, NVMe, NVIDIA+Intel, VBS on, HypervisorPresent, WSL installed, VMware present, Docker installed, Steam present, developer tools present, custom High Performance plan, C: low disk.

| Profile | Intent | Example allows | Example forbids by default |
| --- | --- | --- | --- |
| Balanced | Daily | privacy ads/telemetry *policies that are OFFICIAL* | HVCI off, SysMain off |
| Gaming | Frame time / latency | Game Mode detect, HAGS detect, overlay inventory | Disable hypervisor blindly |
| Developer | toolchain | long paths, Dev Drive *if documented*, keeping Search | Disable WSearch if VS/Cursor indexing needed |
| Workstation | stability | Defender on, restore points | experimental timer tweaks |
| Virtualization | Hyper-V/VMware/WSL | keep virtualization services | "disable all Xbox" if it breaks Game Bar needed by some titles — **NEEDS_RESEARCH** |
| Laptop | power | do not force Máximo rendimiento | desktop-only power plans |
| Privacy | reduce telemetry | documented policies | breaking WU/Defender |
| Server-like | headless | RDP optional | Game DVR |
| Maximum Performance | honest name | power plan + measure | myths |
| Custom | user | whatever they pin | still cannot skip CRITICAL gates |

## Hibernation example (how we would grade WinUtil's tweak)

WinUtil disables hibernation for all machines with text "really meant for laptops". On this desktop, `powercfg /a` still lists hibernate as available and hiberfil is absent. Applying `powercfg /hibernate off` may be **LOW** and **profile-dependent**. Claiming it "should never be used" is **COMMUNITY**, not OFFICIAL. Fast startup is related — **NEEDS_RESEARCH** before bundling.

## Versioning

Catalog schema `tweakSchemaVersion: 1`. Engine refuses unknown `type` ops. Tweaks without `verify` cannot be HIGH or above.
