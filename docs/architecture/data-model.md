# Data model

Date: 2026-09-05

## Entities

```
System 1──* HardwareComponent
System 1──* AuditCheck (result)
System 1──* Service
System 1──* ScheduledTask
System 1──* Driver
System 1──* Process (snapshot)
System 1──* Event (Windows Event Log summary, optional)

Profile *──* Tweak (recommended)
Tweak 1──* AuditCheck (detect)
Tweak *──* Tweak (requires/conflicts)
Tweak 1──0..1 Benchmark (protocol)

Recommendation -> (System facts, Profile, Tweak)

Backup 1──* TweakJob
Snapshot = Backup.logicalDetect
TweakJob -> Backup, Tweak, Log Event

Plugin 1──* Capability
Plugin may contribute Tweaks or Probes

Log Event: timestamp, user, tweak, before, after, backupId, result, errors, rollbackAvailable
```

## Tweak

Identity + evidence + risk axes + detect/desired/apply/verify + compatibility. See TWEAK-ENGINE.md.

## AuditCheck

A probe result: `id`, `status`, `observed`, `expected?`, `severity`, `evidence`.

## Recommendation

`tweakId`, `score`, `reasons[]`, `profile`, `blockedBy[]`.

## Backup

`id`, `createdUtc`, `jobId`, `layers[]`, `sizeBytes`, `restoreVerified?`.

## Benchmark

`protocolId`, `beforeRunId`, `afterRunId`, `verdict`.

## Profile

Enum + detection rules (desktop/laptop, ramGB, gpuVendor, virtPresent, devToolsPresent, steamPresent).

## Plugin

Manifest + path + signature status.

## Relationships that matter

- A Recommendation must not apply a Tweak whose `requires` are unmet.
- A TweakJob without Backup is allowed only if `risk <= LOW` and `reversibility == full` and HKCU-only.
- Log Event is append-only; never update in place.
