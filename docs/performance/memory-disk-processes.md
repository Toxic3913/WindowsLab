# Processes, RAM, and disk (design notes)

Date: 2026-09-05

## Processes

Collect: CPU, working set, private bytes, handles, threads, IO, GPU engine (if counters), start time, service association (`Win32_Service.ProcessId`).

Startup impact ≠ current CPU. Use Startup Impact from Diagnostics-Performance when possible (**admin/ETW**).

## RAM

Show separately:

| Bucket | Meaning | "Optimize"? |
| --- | --- | --- |
| Working set | actively used | no empty |
| Committed | virtual commit charge | watch vs commit limit |
| Standby | cached, available | **not wasted** |
| Modified | dirty cache | |
| Compressed | memory manager | MMAgent (admin) |
| Paged / nonpaged pool | kernel | leak hunting, not tweaking |

Emptying standby to "free RAM" is a **MYTH** for performance. Local `GC.Collect` is unrelated to OS standby.

16 GB on this host: committed memory pressure is the real risk during Docker+VM+game. Recommendation: **hardware upgrade** or fewer simultaneous hypervisors — not SysMain off.

## Disk

Metrics: throughput, IOPS, latency, queue depth via PhysicalDisk counters. TRIM/SMART/temp/firmware: admin/vendor.

This host: two SN750s, C: ~67 GB free — **capacity**, not fragmentation, is the Windows 11 issue. Cleanup must use documented `%TEMP%` / Delivery Optimization APIs, not Prefetch deletion.

Prefetch on SSD: deleting it is a common script (local toolkit). Grade: **UNVERIFIED** as a win; can slow launches. Do not default.

SysMain on NVMe: default **leave running**. Offer EXPERIMENTAL disable only after disk.random shows pathological use by SysMain (ETW).

Windows Search: Developer profile keeps it. Cursor/VS Code users search files. HDD + 100% disk COMMUNITY exception only.
