# WindowsLab.Cli

Production CLI host: `windowslab-cli.exe` (distinct from GUI `WindowsLab.exe` on case-insensitive Windows).

```text
windowslab-cli --help
windowslab-cli audit --os
windowslab-cli live --output json
windowslab-cli workload list
windowslab-cli app list
```

Prefer `--output json` for scripting. Mutating commands require explicit flags (`--lab-apply` / `--apply` / `--yes`).
