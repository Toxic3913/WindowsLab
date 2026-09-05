# TECHNOLOGY DECISION

Date: 2026-09-05  
Status: **CHOSEN** for Phase 1 unless a later decision-log entry supersedes it.

## Recommendation (one stack)

**WindowsLab Engine + CLI + GUI in C# / .NET 10**, with:

| Piece | Choice |
| --- | --- |
| Shared engine | `WindowsLab.Core` class library, `net10.0-windows` |
| CLI | `windowslab` console (`System.CommandLine`) |
| GUI | **WinUI 3** (Windows App SDK), unpackaged first |
| Privileged worker | Separate `WindowsLab.Worker` process, started via `runas` / COM elevation / scheduled task |
| Tweak catalog | JSON + JSON Schema (YAML allowed as authoring sugar) |
| Audit log | SQLite (SQLitePCLRaw) |
| Plugins | Out-of-process, signed, capability manifest |
| Tests | xUnit + FluentAssertions for C#; Pester only for optional PS adapters |
| Install | Signed zip / winget later; **not** `irm \| iex` |

PowerShell 7 remains a **probe/adapter language**, not the application architecture.

## Comparison

| Option | Windows integration | System access | RAM | Maintain | Security | Distro | UAC | Future | Verdict |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **WinUI 3 + WASDK + C#** | Native 11, Fluent | Excellent via C#/PInvoke/Cim | Moderate | High if we stay unpackaged | Good (strong types, no iex) | MSIX or unpackaged | Broker process | Microsoft's desktop direction | **CHOSEN** |
| WPF .NET 10 | Excellent, older look | Excellent | Slightly lower | Very high maturity | Good | Trivial exe | Same broker | Maintenance mode visually | Runner-up |
| PowerShell + WPF (WinUtil) | Good | Good but stringly typed | OK | Weak at this scale | Weak (`InvokeScript`, iex) | Viral | Always-admin | Dead end for plugins | Reject as core |
| Avalonia | Cross-platform unused | Extra interop | OK | Extra toolkit | OK | Easy | Same | We do not need Linux GUI | Reject |
| MAUI | Compromised desktop | Weaker Win32 | Higher | Split focus | OK | Store-oriented | Awkward | Not an admin tool stack | Reject |
| React + WebView2 | Skin deep | Bridge required | **High** | Two languages | Larger XSS/IPC surface | Easy pretty UI | Awkward | Tempting, wrong | Reject |
| Tauri / Rust GUI | OK | Need lots of unsafe/FFI | Low | Two ecosystems | Good if careful | Good | Harder WinRT | Rust not on this host today | Reject for v1 |
| Pure Rust | Max control | High effort | Low | Hiring/agent cost | High | Good | Hard | No rustc on host | Defer |

## Why not WPF as primary?

WPF would ship faster for a classic admin UI. We still lose Fluent, Mica, and the hiring/docs gravity of WinUI. This host already has **.NET 10 Desktop** and **Windows App SDK-capable SDKs**. PowerToys proves WinUI 3 can ship system utilities.

**Fallback:** if WinUI unpackaged elevation or WebView-free packaging blocks Phase 5, switch GUI shell to WPF **without rewriting the engine**. That is why the engine is not in code-behind.

**Specialist dissent (2026-09-05):** an independent GUI review argued WPF should be primary because Windows 11 **Administrator Protection** uses a hidden, profile-separated admin identity (always-elevated GUI assumptions break), WinUI unpackaged + elevation combinations are **UNKNOWN until prototyped**, and WPF on .NET 10 LTS (supported through November 2028) is the lower operational risk. See [D011](architecture/decision-log.md). **Do not target .NET 8** (end of support November 2026).

**PowerShell hosting:** never in the elevated worker. Optional `WindowsLab.PowerShellHost.exe` unelevated, `InitialSessionState.CreateDefault2()`, no arbitrary plugin scripts.

## Why not PowerShell core?

1. Tweaks as `InvokeScript` strings cannot be unit-tested or sandboxed well.
2. JSON catalogs in WinUtil still execute unbounded script.
3. Type system, plugins, SQLite, ETW, DXGI, Named Pipes are first-class in C#.
4. Agents implement C# more safely than 2,000-line `.ps1` files.
5. PowerShell 7.6.5 is available — we will **host** it for CIM/one-liners when cheaper than PInvoke.

## Process model (UAC)

```
[WindowsLab.App WinUI]  unelevated
        | named pipe / JSON-RPC
[WindowsLab.Worker]     elevated, short-lived per job
        |
   CIM / registry / sc / schtasks / bcdedit / powercfg
```

Read-only audit runs unelevated and records `denied` probes. Apply/backup of HKLM/services requires the worker. This matches how professional tools behave and matches this host (audit already worked partially without admin).

## Distribution

Phase 1–4: `dotnet publish` unpackaged, Authenticode when a cert exists (**UNKNOWN** if the operator has a code-signing cert — NEEDS_RESEARCH).

Phase 13: winget + optional MSIX. Sandbox of MSIX vs HKLM tweaks is a **known tension**; many OS tweakers remain unpackaged **for this reason**. Decision: **unpackaged + elevation worker** until proven otherwise.

## Host fit

- SDKs: .NET 8 and 10 already installed
- No Rust toolchain
- Cursor + VS Code present
- WinUI 3 unpackaged is viable on Windows 11 25H2

## Cost of reversing this decision

If we later choose WPF, only `WindowsLab.App` changes. If we later choose PowerShell-as-core, we throw away the engine — **do not**.
