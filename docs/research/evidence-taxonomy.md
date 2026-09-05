# Evidence taxonomy

Date: 2026-09-05

Every claim in this repository that affects Windows internals, tweaks, performance, security, or gaming **must** carry one of these grades.

| Grade | Meaning | Allowed in product |
| --- | --- | --- |
| **OFFICIAL** | Microsoft Learn, Windows Hardware Dev Center, Windows Internals (Russinovich et al.), vendor SDK docs, or an API that we executed on this host | Default catalog, detect/apply/rollback |
| **STRONG EVIDENCE** | Multiple independent primary sources, or measured on this host with a documented method | Default catalog if reversible |
| **COMMUNITY CONSENSUS** | Repeated in reputable admin communities, no primary doc | Optional, never auto-apply, labeled |
| **EXPERIMENTAL** | Plausible, hardware-specific, or version-specific; needs a benchmark | Opt-in only, HIGH/EXPERIMENTAL risk |
| **UNVERIFIED** | Viral tweak, Reddit, YouTube, copied registry with no doc | Not in default catalog |
| **MYTH** | Contradicted by official docs or by measurement | Explicitly blocked or documented as myth |
| **UNKNOWN** | We looked and could not confirm | Do not implement apply; may detect as NEEDS_RESEARCH |
| **NEEDS_RESEARCH** | Worth answering later; recorded in the research index | Blocked from apply until researched |

## Rules

1. Do not invent registry values, service names, or BCD options.
2. A WinUtil / Sophia / debloat JSON entry is **not** evidence. It is a hypothesis to grade.
3. Microsoft Q&A independent-advisor answers are **COMMUNITY** unless they cite a primary document.
4. `irm \| iex` distribution, unsigned scripts, and static `OriginalValue` undo are **not** the WindowsLab safety model.
5. If a measurement cannot be reproduced, the UI must not say "this improved FPS/performance".
