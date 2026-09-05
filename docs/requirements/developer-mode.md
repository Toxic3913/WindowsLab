# Developer mode (detection + recommendations)

Date: 2026-09-05

## Detect (this host already has most)

| Tool | Host |
| --- | --- |
| Git | yes 2.53 |
| GitHub CLI | yes (unauthenticated) |
| Node / npm / pnpm | yes |
| Python / uv | yes (also a 3.15 alpha via `py`) |
| Docker / compose | CLI yes, engine **down** |
| WSL | Ubuntu + docker-desktop stopped |
| Kubernetes | kubectl client only |
| PowerShell 7 | yes |
| VS Code / Cursor | yes |
| VMware | yes |
| Hyper-V | present, needs admin to list VMs |
| .NET 8/10 | yes |
| Rust | no |

## Recommendations (examples, all need evidence before apply)

| Idea | Grade now | Action |
| --- | --- | --- |
| Enable Developer Mode (sideload) | OFFICIAL Settings | detect + optional |
| Long paths | OFFICIAL policy | optional |
| Keep Windows Search | STRONG for this persona | do not disable |
| Do not disable Hyper-V to "gain FPS" while using WSL/Docker | STRONG | conflict with virt profile |
| Docker Desktop running vs stopped | observation | dashboard status, not a tweak |
| Warn Python 3.15.0a7 via `py` launcher | STRONG | might surprise agents |
| Dev Drive | OFFICIAL on supported SKUs | NEEDS_RESEARCH for this Pro 25H2 |

No auto-install of Chocolatey. winget is present.
