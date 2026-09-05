# Official documentation index

Date: 2026-09-05  
Priority: Microsoft Learn, Windows Hardware, PowerShell, Windows App SDK.

This is not an exhaustive dump of the web. It is the **primary-doc map** Phase 1+ implementers should open before writing a tweak.

## Platform and UI

| Topic | URL | Use |
| --- | --- | --- |
| Windows App SDK / WinUI | https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-requirements | GUI choice |
| WinUI 3 | https://learn.microsoft.com/en-us/windows/apps/winui/winui3/ | Dashboard |
| Unpackaged WinAppSDK | https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps | Distro |
| MSIX packaging | https://learn.microsoft.com/en-us/windows/msix/ | Later channel |
| UAC / elevation | https://learn.microsoft.com/en-us/windows/win32/secauthz/user-account-control | Process model |

## Inventory / WMI / CIM

| Topic | URL | Use |
| --- | --- | --- |
| Win32_DeviceGuard | https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity | VBS/HVCI detect (**OFFICIAL** enum tables) |
| Get-CimInstance | https://learn.microsoft.com/en-us/powershell/module/cimcmdlets/get-ciminstance | Audit probes |
| Get-PhysicalDisk | https://learn.microsoft.com/en-us/powershell/module/storage/get-physicaldisk | Storage |
| Get-Tpm | https://learn.microsoft.com/en-us/powershell/module/trustedplatformmodule/get-tpm | TPM (admin) |
| Confirm-SecureBootUEFI | https://learn.microsoft.com/en-us/powershell/module/secureboot/confirm-securebootuefi | Secure Boot (admin) |
| Get-AppxPackage | https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage | AppX |
| Get-ScheduledTask / Export-ScheduledTask | https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/ | Tasks backup |
| Get-MpComputerStatus | https://learn.microsoft.com/en-us/powershell/module/defender/get-mpcomputerstatus | Defender |
| Get-NetFirewallProfile | https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallprofile | Firewall |
| Get-MMAgent | https://learn.microsoft.com/en-us/powershell/module/mmagent/get-mmagent | Prefetch/page combining (admin) |
| Enable/Disable-MMAgent | https://learn.microsoft.com/en-us/powershell/module/mmagent/ | Memory manager — **do not toggle without profile + measure** |

## Security

| Topic | URL | Grade |
| --- | --- | --- |
| Memory integrity / HVCI | https://learn.microsoft.com/en-us/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity | OFFICIAL |
| VBS for drivers | https://learn.microsoft.com/en-us/windows-hardware/drivers/bringup/device-guard-and-credential-guard | OFFICIAL |
| BitLocker | https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/ | OFFICIAL |
| Windows Defender / Microsoft Defender | https://learn.microsoft.com/en-us/microsoft-365/security/defender-endpoint/ | OFFICIAL |
| Credential Guard | https://learn.microsoft.com/en-us/windows/security/identity-protection/credential-guard/ | OFFICIAL |

**Policy:** recommending HVCI **off** for FPS is never default. Community 5–15% CPU-bound claims (e.g. SageTweaks 2026) are **COMMUNITY**, not Microsoft.

## Backup / restore

| Topic | URL | Notes |
| --- | --- | --- |
| Checkpoint-Computer | https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/checkpoint-computer | **One restore point per 24h** limitation is OFFICIAL |
| System Restore | https://learn.microsoft.com/en-us/windows/win32/sr/system-restore-portal | VSS-based, not a full image |
| wbadmin | https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/wbadmin | Image backup |
| reg export | https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/reg-export | Hive/key export |
| bcdedit | https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/bcdedit | Firmware/boot — CRITICAL risk |
| powercfg | https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options | Export plans |
| netsh advfirewall export | Windows Commands | Firewall policy file |
| secedit | https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/secedit | Local security policy |

## Performance / ETW

| Topic | URL | Use |
| --- | --- | --- |
| Windows Performance Recorder | https://learn.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-recorder | Boot, DPC/ISR, CPU |
| Performance counters | https://learn.microsoft.com/en-us/windows/win32/perfctrs/performance-counters-portal | Live metrics |
| ETW | https://learn.microsoft.com/en-us/windows/win32/etw/event-tracing-portal | Engine traces |
| Memory.working set vs standby | Windows Internals + RAMMap docs | Do not treat standby as "wasted RAM" |

## Graphics / gaming (primary vs community)

| Topic | URL / note | Grade |
| --- | --- | --- |
| WDDM hardware scheduling feature id | https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dukmdt/ne-d3dukmdt-dxgk_feature_id (`DXGK_FEATURE_HWSCH`) | OFFICIAL that the feature exists |
| HAGS toggle in Settings | documented in support/Q&A more than a single clean Learn article | Detect via Settings/DXGI; registry `HwSchMode` = **COMMUNITY** until a kernel/Learn page is cited |
| Game Mode | Settings > Gaming; no high-quality public kernel spec found in this pass | Detect HKCU GameBar; apply = **COMMUNITY/NEEDS_RESEARCH** for "FPS" claims |
| DirectStorage | https://learn.microsoft.com/en-us/gaming/gdk/docs/features/system/directstorage/overviews/directstorage-overview | OFFICIAL; mostly app-side |
| Variable refresh / MPO | GPU vendor + WDDM multiplane overlay docs | Partial; **NEEDS_RESEARCH** per GPU |

## Packaging and servicing

| Topic | URL |
| --- | --- |
| Windows Update | https://learn.microsoft.com/en-us/windows/deployment/update/ |
| winget | https://learn.microsoft.com/en-us/windows/package-manager/winget/ |
| Optional features | https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/enable-or-disable-windows-features-using-dism |

## PowerShell testing

| Topic | URL |
| --- | --- |
| Pester | https://pester.dev/docs/quick-start |
| Windows Sandbox | https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-overview |
| Hyper-V checkpoints | https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/ |

## Intentionally weak sources (do not treat as OFFICIAL)

- Microsoft Q&A independent advisors telling users to disable SysMain/WSearch
- YouTube "ultimate 25H2 gaming" lists
- WinUtil `OriginalValue` fields
- SageTweaks / Reddit FPS percentages

Those may be filed as COMMUNITY hypotheses with a required benchmark.
