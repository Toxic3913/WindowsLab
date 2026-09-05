# Recommendation engine

Date: 2026-09-05

Pipeline: **AUDIT → DETECT → ANALYZE → RECOMMEND → APPROVAL → BACKUP → APPLY → VERIFY → BENCHMARK**

## Inputs

- Inventory facts (CPU class, RAM, GPU, laptop/desktop, virt, disk media, locale)
- Installed roles (Steam, VS Code, Cursor, Docker, WSL, VMware)
- Security posture (VBS, Defender, firewall)
- Profile selected by user (never inferred silently for apply; inference only for **default profile suggestion**)

On PC-HUGO a suggested default is **Custom mix: Developer + Gaming + Virtualization**, not Maximum Performance.

## Scoring (initial, must be documented in UI)

```
score = evidenceWeight * reversibilityWeight * profileMatch - riskPenalty - conflictPenalty
```

OFFICIAL + SAFE + profile match → top. EXPERIMENTAL never auto-selected.

## Output

Ordered list with: why, risk, side effects, reboot, security/update/compat flags, how to undo.

## Forbidden product copy

"Apply all essential tweaks."
