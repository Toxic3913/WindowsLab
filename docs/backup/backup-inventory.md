# Backup inventory (mechanisms)

See [../BACKUP-RECOVERY.md](../BACKUP-RECOVERY.md).

This file tracks **which OS mechanisms we will wrap**, not implementation.

| Mechanism | Status |
| --- | --- |
| System Restore / Checkpoint-Computer | planned, 24h limit |
| Logical detect snapshot | planned always |
| reg export | planned |
| Service JSON | planned |
| schtasks XML | planned (ola.txt already proves export works mostly) |
| netsh advfirewall export | planned |
| powercfg -export | planned |
| bcdedit /export | CRITICAL jobs only |
| secedit | if present |
| pnputil export-driver | optional |
| wbadmin full image | not v1 (heavy) |
| VMware snapshot | operator process, not API v1 |
