# catalog/applications/

Curated winget packages (D019). Validated against `../../schemas/application.schema.json`.

- Do **not** import WinUtil `applications.json`.
- `UNKNOWN` / `EXPERIMENTAL` evidence → never recommend or install by default.
- Axes are relative scores for ranking, not absolute claims.
- Prefer free / open-source tools suitable for IT work, study, gaming, and telework.

## Categories

| File | Category tag | Focus |
| --- | --- | --- |
| `browsers.json` | `browser` | Chromium/Firefox/Edge |
| `tools.json` | `tool` | Git, 7-Zip, Everything, PowerToys, Rufus, … |
| `system.json` | `system` | HWiNFO, CrystalDiskInfo, CPU-Z, Sysinternals |
| `security.json` | `security` | KeePassXC, Bitwarden, WireGuard, Proton VPN |
| `backup.json` | `backup` | Duplicati, FreeFileSync, Kopia |
| `developer.json` | `developer` | VS Code, gh, Node, Python, .NET SDK |
| `virt.json` | `virt` | Docker Desktop, VirtualBox |
| `remote.json` | `remote` | RustDesk, WinSCP, PuTTY, Wireshark |
| `office.json` | `office` | LibreOffice, Thunderbird, Obsidian, PDFsam |
| `media.json` | `media` | VLC, GIMP, Audacity, OBS |
| `gaming-apps.json` | `gaming` | Steam, Discord, Epic |

Profiles (`balanced` / `developer` / `gaming` / `virtualization`) filter recommendations in the Apps page.
