# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.0.x | Yes |

WindowsLab targets **Windows 11** (`CurrentBuild >= 22000`).

## Reporting a vulnerability

1. Prefer a **private** GitHub Security Advisory on [Toxic3913/WindowsLab](https://github.com/Toxic3913/WindowsLab) when available.
2. Otherwise open a normal issue titled `[Security]` **without** exploit steps if disclosure must stay limited, and ask maintainers to convert it to a private channel.

Do not file public issues that include working exploit PoCs against WindowsLab elevation or backup stores.

## Product security posture

See [docs/SECURITY-MODEL.md](docs/SECURITY-MODEL.md). Short version:

- No `irm | iex` delivery.
- Unelevated UI + elevated Worker; system apply is opt-in (D020).
- No generic `InvokeScript` in the default catalog.
- Defender realtime opt-out is curated HIGH only (D021), never recommended by default.
