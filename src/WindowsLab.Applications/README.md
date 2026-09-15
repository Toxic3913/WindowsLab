# WindowsLab.Applications

Winget-only curated application installs (D019).

- Load `catalog/applications/*.json`
- Detect installed apps via Uninstall registry (not `Win32_Product`)
- Install with explicit approval; UNKNOWN/EXPERIMENTAL blocked
- Logs under `%ProgramData%\WindowsLab\reports\app-installs\`
