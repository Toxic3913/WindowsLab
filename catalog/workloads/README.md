# catalog/workloads/

Curated **external workloads** (D028): detect + runtime stop for third-party launchers/overlays.

- Allowlisted process and service names only.
- Never DiagTrack / SysMain / WSearch / Defender family.
- UI: Más → Cargas externas. CLI: `windowslab-cli workload list|stop <id> --yes`.
- Not a generic process killer; Performance page remains sample-only (D022).
