# Architecture decision log

Canonical file. Also linked from `docs/DECISION-LOG.md`.

Do not change an architectural decision without a new dated entry.

---

## D001 — Product is not a WinUtil clone

- **Date:** 2026-09-05
- **Context:** Operator asked to study winutil and surpass it without cloning.
- **Options:** Fork winutil; wrap winutil; independent engine.
- **Chosen:** Independent engine; conceptual reuse only.
- **Reason:** Safety, evidence, CLI, plugins, and measurement cannot be bolted onto WPF checkbox IDs.
- **Trade-offs:** Slower to first GUI; no instant app-install tab.

## D002 — C# / .NET 10 engine + WinUI 3 + CLI

- **Date:** 2026-09-05
- **Context:** Need GUI+CLI, UAC, maintainability. Host has .NET 10 Desktop, no Rust.
- **Options:** PS+WPF; WPF; WinUI3; Avalonia; MAUI; Tauri; Rust.
- **Chosen:** WinUI 3 unpackaged + Core classlib + console CLI. WPF is fallback **shell only**.
- **Reason:** See TECHNOLOGY-DECISION.md. Microsoft direction, types, PowerToys precedent.
- **Trade-offs:** WinUI unpackaged packaging friction vs WPF's boring reliability.

## D003 — Unelevated UI + elevated worker

- **Date:** 2026-09-05
- **Context:** This host's audit already works partially without admin; always-admin is a WinUtil smell.
- **Chosen:** Broker process.
- **Reason:** Least privilege; better UX for Audit tab.
- **Trade-offs:** IPC complexity.

## D004 — No generic InvokeScript in default catalog

- **Date:** 2026-09-05
- **Context:** winutil tweaks.json contains unbounded PowerShell.
- **Chosen:** Declarative ops + compiled handlers.
- **Reason:** Testability and review.
- **Trade-offs:** Some tweaks harder to express.

## D005 — Rollback from backup snapshot, not catalog OriginalValue

- **Date:** 2026-09-05
- **Chosen:** Backup record is source of truth.
- **Reason:** OriginalValue in winutil is often blank or wrong.
- **Trade-offs:** Must succeed at backup or refuse apply.

## D006 — Windows 11 primary; Windows 10 detect-only

- **Date:** 2026-09-05
- **Chosen:** minBuild 22000 for mutating tweaks unless opted in.
- **Reason:** Operator is on 25H2; Win10 would double test matrix.
- **Trade-offs:** Fewer users.

## D007 — Unpackaged distribution in v1

- **Date:** 2026-09-05
- **Chosen:** Unpackaged signed exe; MSIX later.
- **Reason:** HKLM/services vs MSIX container constraints.
- **Trade-offs:** Harder Store distribution.

## D008 — No ISO customization / TPM bypass

- **Date:** 2026-09-05
- **Chosen:** Out of scope forever unless a new decision reopens.
- **Reason:** WinUtil Creator is high-risk and not a lab audit tool.
- **Trade-offs:** No "reinstall optimized Windows" feature.

## D009 — CLI is a Phase 1 client, Phase 11 is polish

- **Date:** 2026-09-05
- **Context:** Operator listed CLI as Phase 11.
- **Chosen:** Stub CLI in Phase 1; complete in 11.
- **Reason:** Engine needs a driver before GUI.
- **Trade-offs:** Two phases named CLI.

## D010 — Test on VMware VM, not host

- **Date:** 2026-09-05
- **Chosen:** `D:\VM\Windows 11 x64` + 25H2 ISO for clean clones.
- **Reason:** Host is irreplaceable daily driver.
- **Trade-offs:** GPU/PresentMon on VM is not comparable to RTX 3060 host; gaming benches stay host-opt-in later.

## D011 — GUI specialist dissent: keep WinUI 3, gate Phase 5

- **Date:** 2026-09-05
- **Context:** Independent GUI review recommended **WPF .NET 10 LTS** as the primary shell (Administrator Protection, unpackaged EXE predictability, WinUI/App SDK elevation combinations **UNKNOWN** until prototyped). Phase 0 originally chose WinUI 3 (D002).
- **Options:** Flip D002 to WPF now; keep WinUI 3; prototype both.
- **Chosen:** **Keep D002 (WinUI 3 unpackaged)** as the documented default. **Do not start Phase 5 GUI until** a short prototype proves: unpackaged launch, named-pipe elevation worker, and Administrator Protection identity. If that prototype fails, **D002 falls back to WPF without rewriting Core**.
- **Reason:** Engine/CLI do not depend on the shell. Changing D002 now would be premature; ignoring the WPF case would be dishonest. .NET 10 LTS (support through Nov 2028) remains either way. .NET 8 ends Nov 2026 — do not target .NET 8.
- **Trade-offs:** Phase 5 may slip if WinUI unpackaged + elevation is painful. Visual Fluent match vs operational boredom.

## D012 — Layered backup is exact inverse first; System Image is not the CRITICAL guarantee

- **Date:** 2026-09-05
- **Context:** Security/backup review: `reg export` does not delete keys created after export and omits ACLs; `Get-Service` is not full SCM; System Image Backup via `wbadmin` is **deprecated** as a product (Microsoft still documents the command).
- **Chosen:** Rollback prefers **recorded previous values + inverse ops**. `reg import` of a whole key is disaster-only. CRITICAL still requires WinRE readiness + BitLocker recovery material **outside** the transaction bundle. `wbadmin` is an optional legacy backend, not the sole CRITICAL path.
- **Reason:** Official mechanism gaps. Never store BitLocker recovery keys in the WindowsLab backup store (D014).
- **Trade-offs:** More code than “export everything”.

## D013 — Plugin trust tiers

- **Date:** 2026-09-05
- **Chosen:** Third-party plugins **out-of-process** (Job Object, capability broker). First-party may use in-process `AssemblyLoadContext` only after Authenticode + thumbprint pin. WASM optional later for report transforms only. **MEF is not a security boundary.**
- **Reason:** Microsoft documents ALC as non-security. GPU/Hyper-V plugins need Win32.
- **Trade-offs:** IPC cost.

## D014 — Data root and VSS storage on D:

- **Date:** 2026-09-05
- **Context:** Host C: has ~67 GB free. Restore points and WindowsLab artifacts must not fill the boot volume.
- **Options:** Keep `%ProgramData%` on C:; move only logical backups; associate VSS shadow storage for C: onto D:.
- **Chosen:** Default **WindowsLab data root = `D:\WindowsLab`**. Logical backups, logs, reports live there. Settings remain in `%LocalAppData%\WindowsLab`. Optional (admin, documented, never silent): `vssadmin add shadowstorage /For=C: /On=D:` so OS restore points for C: consume D: space. **Do not confuse “System Protection on D:” with protecting C:** — protecting D: only snapshots D:.
- **Reason:** Operator request + measured C: pressure. Official VSS can store another volume’s shadows on D:.
- **Trade-offs:** If D: is missing (other PCs), fall back to `%ProgramData%\WindowsLab` after a warning.
- **Superseded in part by:** [D016](#d016--source-on-d-installed-program-on-c) (2026-09-06). Operator meant the **repo** on D:, not runtime data.

## D015 — Test fabric is a dedicated Win11 Pro VM; product stays hardware-agnostic

- **Date:** 2026-09-05
- **Context:** Optimizations must not be developed against PC-HUGO. The product must still run on unknown hardware.
- **Chosen:** All mutating tests on VM **WindowsLab-Test-25H2** (spec in `docs/testing/vm-lab.md`). Host and any other SKU: audit/degrade. Catalog + probes use **facts** (RAM GB, GPU vendor, virt present), never hardcoded 12600K/3060/16 GB.
- **Reason:** Operator request. Existing `D:\VM\Windows 11 x64` may be dirty; ISO `Win11_25H2_Spanish_x64_v2.iso` is the clean source.
- **Trade-offs:** GPU/PresentMon on VM ≠ RTX 3060 host; gaming benches remain host-opt-in later.

## D016 — Source on D:; installed program on C:

- **Date:** 2026-09-06
- **Context:** Operator clarified D014. “Things on D:” meant the **project directory** (code, docs, tests), not the running app. The program should live on C:.
- **Chosen:** Git repo = **`D:\WindowsLab`**. Install + `%ProgramData%\WindowsLab` + `%LocalAppData%\WindowsLab` stay on **C:**. VSS `/For=C: /On=D:` remains an optional admin recommendation (C: is still tight); it is not the app data root.
- **Reason:** C: is for Windows and installed software. D: already holds DEV-IA, NEXUS, VMs — source belongs there.
- **Trade-offs:** Other machines without D: clone the repo wherever they want; runtime path does not depend on D:.

## D017 — Beta 0 is read-only; WPF shell; dual installer

- **Date:** 2026-09-06
- **Context:** Operator wants a usable beta (dashboard, advice, catalog, easy install) before the lab VM. Apply on the host remains forbidden (D010). WinUI 3 + WASDK self-contained is a common “won't start on a clean PC” failure. D011 gated WinUI on an elevation prototype; Beta 0 has **no** elevated worker.
- **Chosen:** Beta 0 = audit + detect + recommend + **simulate**. Apply is policy-blocked (exit 13). GUI is **WPF .NET 10 unpackaged** (the D002 fallback). Distribution is **self-contained win-x64**: Inno Setup `setup.exe` to `C:\Program Files\WindowsLab` **and** a portable zip. WinUI 3 remains the later shell target; Core is unchanged.
- **Reason:** Ship on any Windows 11 x64 without the SDK. Do not mutate the daily driver.
- **Trade-offs:** Fluent/Mica wait until a later GUI swap. Catalog is curated (~30–40), not WinUtil-scale.

## D018 — Beta 0.1 lab apply (HKCU only)

- **Date:** 2026-09-14
- **Context:** Operator on lab VM wants Apply from the WPF UI; full Phase 3–4 backup/worker not ready. System ComboBox/DataGrid chrome made the dark UI unreadable; product lacked an icon.
- **Chosen:** Enable **lab apply** for tweaks that are `registry` + **HKCU** + **LOW** + **OFFICIAL|STRONG**, no reboot/security/compat flags. Backup previous value (or missing) under `%LocalAppData%\WindowsLab\backups\` before write (D005). CLI requires `--lab-apply` (without it still exit 13). GUI prompts Yes/No then applies. HKLM / services / elevation / Worker remain blocked. Dark WPF chrome + `assets/WindowsLab.ico`.
- **Reason:** Unblock VM lab workflows without mutating HKLM or the daily-driver host policy surface.
- **Trade-offs:** No System Restore point yet; rollback is per-value JSON. Preset CLI `apply` still exit 13 (use GUI pack apply).

## D019 — Applications module (winget-only, curated)

- **Date:** 2026-09-15
- **Context:** Operator wants prep-of-PCs UX including selecting default installers (browsers/tools) with multi-axis recommendations (privacy, telemetry, security, performance, ecosystem) — without becoming a WinUtil checkbox wall.
- **Options:** Defer forever (D001 trade-off); checklist-only howTo; curated winget module; full choco+winget+InvokeScript.
- **Chosen:** Curated `catalog/applications/*.json` + `WindowsLab.Applications` winget client. Install requires explicit confirmation (`--yes` / GUI Yes). No Chocolatey auto-install, no `irm|iex`, no WinUtil JSON import. UNKNOWN/EXPERIMENTAL never recommended or installed by default. No Worker yet: spawn `winget`; if elevation required, one `runas` UAC prompt. Browser ranking is profile-weighted multi-axis (no fixed “best browser”). Install ≠ set default browser (open `ms-settings:defaultapps`).
- **Reason:** Fill the prep gap vs WinUtil while keeping evidence grades and least-privilege install path.
- **Trade-offs:** Smaller catalog than WinUtil; machine-wide packages may UAC; uninstall/rollback via `winget uninstall` is a follow-up.

