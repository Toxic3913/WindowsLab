# Updates & releases (operator checklist)

WindowsLab is **public**. In-app update uses GitHub Releases **without a token**.

## What users do

1. Install once from [Releases](https://github.com/Toxic3913/WindowsLab/releases) → `WindowsLab-Setup.exe`.
2. In the app: **Más → Buscar e instalar actualización**.
3. Confirm → download Setup → UAC → app closes → files replaced → app reopens.

No GitHub token is required while the repository stays public. The UI no longer offers saving a PAT; for a private repo again, set `WINDOWSLAB_GITHUB_TOKEN` / `GH_TOKEN` or `operator.json` (Contents:read) outside the app.

## What you do to publish a new version

1. Bump version in `Directory.Build.props` (+ EULA / CLI help / tests if needed).
2. Commit on `main` (default branch).
3. Tag and push:

```powershell
git tag -a v1.2.4 -m "WindowsLab 1.2.4"
git push origin v1.2.4
```

4. Wait for Actions workflow **release** (`v*` tags):
   - runs tests
   - runs `eng/publish.ps1`
   - uploads `WindowsLab-portable-win-x64.zip` and `WindowsLab-Setup.exe`
5. Confirm the new tag is **Latest** on the Releases page (not draft, not pre-release).
6. In a PC with the previous version: **Buscar e instalar actualización** → should offer the new tag.

## Required GitHub settings (already applied when possible)

| Setting | Value |
| --- | --- |
| Visibility | **Public** |
| Default branch | **main** |
| Homepage | Releases URL |
| Workflow `release.yml` | on tag `v*` |
| Old Beta `v0.3.0` | Pre-release / archived |

Optional but recommended: Settings → Code security → enable **Secret scanning** and **Dependabot alerts**.

## Asset names (must match the updater)

The app looks for a release asset whose name contains `Setup` and ends with `.exe` (e.g. `WindowsLab-Setup.exe`). Do not rename that file in the release.

The portable payload must include **`WindowsLab-Uninstall.exe`**. Setup registers it in **Aplicaciones instaladas** (`HKLM\...\Uninstall\WindowsLab`).

## Local smoke test (optional)

```powershell
# Anonymous API must return the latest tag
Invoke-RestMethod https://api.github.com/repos/Toxic3913/WindowsLab/releases/latest |
  Select-Object tag_name, @{n='assets';e={$_.assets.name}}
```
