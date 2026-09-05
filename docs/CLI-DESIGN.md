# CLI DESIGN

Date: 2026-09-05

Inspired by `gh`, `kubectl`, `winget` — verb + noun, stable JSON, nonzero exit on failure.

## Global flags

```
windowslab [command]
  --output json|table|markdown|sarif|yaml   # --format alias
  --profile <name>
  --yes                            skip LOW confirms only
  --confirm                        skip MEDIUM+ confirms; also requires WINDOWSLAB_CONFIRM=1 in scripts
  --dry-run
  --disable-interactivity
  --verbose
  --log-file <path>                # NDJSON
  --config <path>
```

`--output json` **must still exit non-zero** on failure (unlike some `gh --json` cases).

Exit codes: `0` ok, `1` generic, `2` cancelled, `3` partial, `4` elevation required, `5` preflight/incompatible, `6` plugin, `7` backup failed, `10` tweak not found, `11` already applied, `12` rollback unavailable, `13` policy blocked, `20+` audit `--fail-on` severity. Document in `windowslab help exit-codes`.

JSON envelope includes `schemaVersion`, `command`, `exitCode`, `timestamp`, `host.build/edition`, `data`.

Audit/report may emit **SARIF 2.1.0** (`--output sarif`) for CI gates. Non-security findings go in `properties.category`.

## Commands

### audit

```
windowslab audit
windowslab audit --category hardware,security
windowslab doctor              # alias: failed probes + obvious misconfig
windowslab report              # last audit as markdown
windowslab report --out report.md
```

### backup

```
windowslab backup create --reason "pre-gaming-profile"
windowslab backup list
windowslab backup show <id>
windowslab backup restore <id>
```

### tweak

```
windowslab tweak list [--category privacy] [--profile gaming]
windowslab tweak show privacy.telemetry.allow
windowslab tweak detect <id>
windowslab tweak apply <id>
windowslab tweak apply <id> --dry-run
windowslab tweak rollback <id> --backup <id>
```

No `tweak apply --all`.

### optimize

```
windowslab optimize --profile gaming --recommend-only
windowslab optimize --profile gaming --from-approval .\approval.json
```

`windowslab optimize` without flags prints help and does nothing. This is intentional.

### benchmark

```
windowslab benchmark list
windowslab benchmark run disk.random --drive D:
windowslab benchmark compare <beforeId> <afterId>
```

### plugins / developer

```
windowslab plugin list|install|remove|verify
windowslab dev detect
```

## Machine output

JSON schema versioned. For CI, `--format json` on audit should be stable. SARIF later for security checks (**Phase 8**).

## Elevation

If a command needs admin, CLI re-executes via worker or `Start-Process -Verb RunAs` and streams results. Unelevated `audit` never prompts.

## Agent-friendly

Commands used by Cursor agents in later phases should prefer JSON. Document this in AGENTS.md after Phase 1.
