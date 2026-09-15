# SECURITY MODEL

Date: 2026-09-05

## Threat model (local tool)

| Threat | Mitigation |
| --- | --- |
| Accidental self-DoS (boot, network, Defender) | risk gates, backup, verify, no CRITICAL in profiles |
| Malicious plugin | signatures, capabilities, out-of-process |
| Supply chain (`irm \| iex`) | we do not use it; pin hashes on release |
| Running always-admin | unelevated UI |
| Logging secrets | redaction; no product-key module |
| Tampering with backup store | ACL on `%ProgramData%\WindowsLab`, optional signature of backup manifest |

We are **not** an EDR. We consume Defender/Firewall/VBS state; we do not replace them.

## Trust boundaries

1. Catalog files (treat as untrusted until schema-validated and, for third-party, signed)
2. Plugins
3. Worker (admin)
4. SQLite log (integrity, not secrecy)

## Default security posture

- Microsoft Defender real-time: **do not disable**
- Tamper Protection: recommend **on** if we can detect it (this host: currently False — recommendation, not auto-apply)
- VBS: this host has VBS **running**. Do not recommend off. Gaming profile may **explain** possible CPU-bound cost with COMMUNITY evidence and require EXPERIMENTAL + benchmark
- HVCI / memory integrity: currently **not running**. Enabling is a **security increase** with driver-compat risk (OFFICIAL warning on Microsoft Learn). Recommend only in Security profile after driver inventory
- Credential Guard: not running. Enterprise-oriented; not default on WORKGROUP gaming/dev PC
- RDP: disabled — keep unless Server-like profile
- Firewall: remain enabled
- Secure Boot / TPM: detect-only until admin; never "bypass" (WinUtil ISO path is rejected)

## Elevation policy

| Action | Elevation |
| --- | --- |
| Audit (most probes) | no |
| HKCU Game Bar | no |
| HKLM, services, tasks, firewall, features | worker |
| BCD, BitLocker, HVCI registry | worker + CRITICAL UI |
| Plugin kernel helpers | explicit, never silent |

## Code execution policy

Default catalog cannot contain script. Handlers are compiled. PowerShell adapters, if any, run with `-NoProfile -NonInteractive` and a allow-list of cmdlets.

## Logging

Every mutation writes: timestamp (UTC), Windows identity, tweak id, catalog version, previous state hash, new state hash, backup id, result, errors, rollback token.

## Secrets

Out of scope: product keys, stored VPN passwords, browser cookies. Do not implement `Buscador-Clave-Producto`.

## Hardening the app itself

- Authenticode on `windowslab-cli.exe` / worker (cert: **NEEDS_RESEARCH**)
- ASLR/CFI defaults from .NET
- Named pipe ACL: only same user + admins
- Disable plugin load from writable-by-everyone paths
