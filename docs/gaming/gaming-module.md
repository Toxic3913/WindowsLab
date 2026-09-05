# Gaming module (design)

Date: 2026-09-05

## Goal

Inventory gaming-related OS/GPU settings and measure **frame time**, not market a 200-tweak pack.

## Metrics (keep separate)

| Metric | How | Claim allowed |
| --- | --- | --- |
| Average FPS | PresentMon | weakest |
| Frame time median / p95 | PresentMon | yes |
| 1% / 0.1% lows | PresentMon | yes, with run count |
| Input latency | UNKNOWN inbox method | no claim in v1 |
| Network latency | ping/TCP | not FPS |
| CPU / GPU util | counters / GPU plugin | indirect |

## Settings to DETECT (v1)

| Setting | Detect | Apply in v1? | Evidence |
| --- | --- | --- | --- |
| Game Mode | Settings toggle; HKCU is **not** proof of per-process engagement | optional Settings/HKCU LOW | OFFICIAL control; EXPERIMENTAL FPS |
| Game DVR / captures | GameConfigStore (this host GameDVR_Enabled=1) | optional | COMMUNITY idle vs active capture |
| VRR | Windows Advanced Display + vendor CP + OSD | detect | OFFICIAL vendor docs |
| HAGS | Settings + dxdiag WDDM 2.7+; `HwSchMode` **not** a management contract; `D3DKMT_WDDM_2_7_CAPS` is **reserved** | detect only in v1; Settings+reboot if we ever apply | [DirectX blog](https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/) OFFICIAL; registry UNVERIFIED |
| MPO | `IDXGIOutput2::SupportsOverlays` = capability, not active use | **no** `OverlayTestMode` | OFFICIAL DDI; registry COMMUNITY |
| Windowed-game optimizations | Graphics Settings (flip model for DX10/11 windowed) | optional documented UI | [Microsoft Support](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11) OFFICIAL |
| Shader cache | Storage “DirectX Shader Cache”; NVIDIA Shader Cache Size | clear only as cold-cache test | OFFICIAL; clearing **worsens** first-run stutter |
| DirectStorage | `fsutil bypassIo state`; game must opt in | never “enable DS” OS tweak | OFFICIAL |
| Timer resolution | ETW of `timeBeginPeriod` requesters | **never** force 0.5 ms globally | OFFICIAL per-process since 2004; MYTH as FPS pack |
| Fullscreen optimizations | per-app Graphics / compatibility | detect; disable only for proven regression | OFFICIAL UI; global `GameDVR_FSEBehavior` UNVERIFIED |
| Xbox services | service list | do not mass-disable | may break Game Bar |
| GPU scheduling | see HAGS | | |
| Power plan | powercfg | Gaming profile may **recommend keeping** current Máximo rendimiento; do not assume SCHEME_MIN is better than a custom GUID (this host uses custom) | |
| Overlays | process list: Discord, NVIDIA App, Xbox Game Bar | recommend measure with/without | STRONG as interference hypothesis |
| DPC/ISR | WPR protocol | no timer tweaks without it | OFFICIAL tool |

## Hardware-specific (PC-HUGO illustration)

- RTX 3060 + 144 Hz-class display (143 Hz observed): VRR **may** matter; unmeasured.
- i5-12600K: CPU-bound esports titles might show HVCI cost **if HVCI were on**; it is **not running** here. Do not disable VBS for gaming by default (VBS is running; nested virt in use).
- 16 GB RAM: overlay + Chrome + Discord + game will commit hard; more RAM is a hardware recommendation, not a tweak.
- Dual NVMe: DirectStorage-capable storage; still game-dependent.

## Anti-goals

- "Ultimate 25H2 gaming registry pack"
- Disable HVCI/VBS as a recommended gaming tweak
- Fake FPS numbers
