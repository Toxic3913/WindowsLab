# Dashboard architecture

Date: 2026-09-06  
GUI technology: **WPF .NET 10** for Beta 0 (D017). WinUI 3 remains the later shell (D002). Not a WinUtil clone.


## Shell

NavigationView (left) with the operator-requested destinations. Each destination is a module view bound to Core services. Unimplemented modules show inventory facts if the audit already collected them, else a placeholder — **never fabricated gauges**.

## Pages

| Route | Primary data | Notes |
| --- | --- | --- |
| Dashboard | last audit summary, profile, backup health, C: free space | Low disk warning on this host |
| Audit | probe tree, export | Unelevated first |
| Performance | counters + myths panel | Standby RAM explainer |
| Optimization | recommendations, not a mega-checkbox | Approval cart |
| Gaming | Game Mode/HAGS/overlays detect | PresentMon opt-in |
| Security | Defender, VBS, firewall | No "disable all" |
| Privacy | OFFICIAL policies | |
| Hardware | CPU/GPU/RAM/board | BIOS age warning |
| Storage | volumes, SMART if admin | |
| Network | adapters, DNS, WinHTTP, VPN present | Fortinet visible |
| Applications | Curated winget catalog + multi-axis recommend | Install with confirmation (D019); not WinUtil-scale |
| Services | filter + diffs vs baseline | |
| Scheduled Tasks | list + export | |
| Drivers | signed list | |
| Windows Update | COM settings | no pause-as-tweak default |
| Backups | restore | |
| Recovery | rstrui + our backups | |
| Benchmarks | protocols | inconclusive OK |
| Developer | toolchain | |
| Plugins | sideload | |
| Logs | SQLite viewer | |
| Settings | theme (dark/light/system), language, elevation helper | |

## UX rules

1. Apply is a cart with diffs.
2. CRITICAL items red, require typing the tweak id.
3. Every row shows evidence grade.
4. Dark theme default; light + system selectable (Beta 0.2).
5. No "boost" wording.
