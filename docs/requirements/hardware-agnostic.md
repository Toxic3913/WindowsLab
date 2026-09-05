# Hardware-agnostic product rules

Date: 2026-09-05  
Decision: D015

PC-HUGO (i5-12600K, RTX 3060, 16 GB, two NVMe, VMware+Hyper-V+WSL) is **one** inventory profile used in docs as an example. It is not the product’s assumed machine.

## Must

1. Every probe records `Status` (`Ok|Denied|Unsupported|Error|Timeout`) and never invents zeros.
2. Tweaks declare compatibility: `minBuild`, `editions[]`, `requires[]`, `conflicts[]`.
3. Recommendations are computed from **facts** (RAM GB, disk media, GPU vendors[], hypervisorPresent, laptop/desktop, toolchain).
4. Data root: `%ProgramData%\WindowsLab` on the system drive. Source checkout on this lab is `D:\WindowsLab`; that is not the install path.
5. Tests: mutating → VM spec; unit tests use **fixtures**, not live WMI from the developer PC as the only path.

## Must not

1. Hardcode CPU names, NVIDIA device IDs, Corsair part numbers, or “16 GB”.
2. Skip a probe because “our lab always has admin / always has D:”.
3. Claim FPS/IOPS on hardware we did not measure.

## Fixture sets (tests)

| Fixture | Intent |
| --- | --- |
| `host-pchugo.json` | Regression vs the Phase 0 audit |
| `vm-lab-25h2.json` | 4 vCPU, 8 GB, virtual GPU, Pro |
| `laptop-amd-home.json` | Home, battery, iGPU only |
| `no-d-volume.json` | Single disk (no D: for source/VMs) |

Add fixtures when we meet those machines. Until then, keep the JSON schema ready.
