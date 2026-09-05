# BENCHMARK ENGINE

Date: 2026-09-05

## Rule

If we cannot measure it, we cannot claim it.

## Metric classes

| Class | Meaning | Example | Allowed claim |
| --- | --- | --- | --- |
| **Reproducible benchmark** | Same protocol, same machine, before/after, variance reported | DiskSpd 64K random QD32 30s × 3 runs | "median IOPS changed by X% ± Y" |
| **Indirect metric** | Related but not the user goal | CPU % during idle | "idle CPU dropped"; not "game FPS improved" |
| **Estimate** | Counter with known error | WMI GPU memory | Show as estimate |
| **Subjective** | User felt snappier | UI smoothness | Store as opinion, never in reports as proof |

## Protocols (v1)

### disk.sequential / disk.random

Tool: DiskSpd (Microsoft, vendor the binary with license file). Run on D: (this host has space) never on a nearly full C: without warning.

### cpu.idle_and_load

ETW + perf counters. Do not use WinSAT as proof.

### memory.pressure

Working set, committed, standby, compressed (MMAgent/memory counters). Teach the UI: **standby is cache**, not waste. Emptying standby can **hurt**.

### boot.time

WPR boot trace or `Diagnostics-Performance` logon events. Slow; opt-in.

### dpc.isr

WPR CPU profile. Needed before blaming "timer resolution" or network adapters. **No apply of HPET/timer tweaks without this protocol.**

### gpu.frame_times (Gaming module)

PresentMon capture of a user-selected process. Report:

- average FPS (least important)
- median frame time
- 1% / 0.1% lows
- time-in-ms percentiles

Define 1% low as **p99 frame time** (and optionally `1000/p99_ms` as equivalent FPS). 0.1% = p99.9; statistically unstable — needs longer captures.

PresentMon: report displayed FPS vs application FPS separately; do not mix generated frames silently. Render latency ≠ input latency. Click-to-photon needs LDAT-class hardware — **no claim in v1**.

ETW: reject traces with `EventsLost`. `% Processor Utility` is **not** `% Processor Time` and can exceed 100% under boost.

DiskSpd: Microsoft recommends **≥ 60 s** for serious runs; `-o` is queue depth; `-L` latency. Do not apply HDD “queue > 2 is bad” to NVMe.

Protocol: capture immutable system manifest first; **ABBA/BAAB** run order; ≥5 paired runs before claiming more than exploratory; paired 95% CI; “no detectable change” when CI includes zero.

### input.latency / network.latency

Input latency: **NEEDS_RESEARCH** for a first-party Windows API that is trustworthy for games. Do not use webcam-vs-LED methods in-app.

Network: ICMP/TCP to a user-chosen host is an indirect metric. VPN (Fortinet present) will dominate. Label as such.

### power / thermals

RAPL / NVML / LibreHardwareMonitor: plugin, not core. If missing, metric = unavailable.

## Before/after job

```
capture system manifest (build, drivers, power, VBS, HAGS, display)
randomize ABBA (do not run all BEFORE then all AFTER)
run protocol N times (N≥5 for claims; fewer = exploratory)
apply tweak (caller)
reboot if required; cooldown
run protocol N times
statistics: median, IQR, paired CI
result: improved | regressed | inconclusive
```

**Inconclusive is a first-class result.** With 16 GB RAM and dual NVMe, many privacy tweaks will be inconclusive on IOPS/FPS. That is success for honesty.

## Myths to encode as tests (not tweaks)

| Claim | Grade | Engine behavior |
| --- | --- | --- |
| Free RAM = faster | MYTH | UI copy: Windows uses RAM for cache |
| Disable SysMain always faster | UNVERIFIED / often MYTH on NVMe | Do not default; if user insists, require disk.random protocol |
| Disable Windows Search always faster | UNVERIFIED | Developer profile recommends **keeping** WSearch; HDD-only COMMUNITY exception |
| Disable HPET always better | MYTH/UNVERIFIED | Blocked without dpc.isr + docs |
| GC.Collect RAM boost | MYTH | Blocked (seen in local BAT) |

SysMain: Microsoft still ships it as Superfetch/SysMain for prefetch and related memory behavior. `Get-MMAgent` is the OFFICIAL configuration surface. Disabling the service to "fix 100% disk" is a **COMMUNITY** HDD-era workaround; this host is NVMe.

## What we will not bundle

3DMark, Cinebench, commercial suites. Optional: detect if installed and offer to **launch** them, recording that the result is external.
