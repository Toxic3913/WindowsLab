# Compatibility matrix

See also TESTING-STRATEGY.md.

| SKU | Audit | Tweaks | Notes |
| --- | --- | --- | --- |
| Windows 11 Pro 25H2 26200 | primary | primary | PC-HUGO |
| Windows 11 Home | yes | subset (no some policies/Hyper-V) | per-tweak editions |
| Windows 11 Enterprise / Education | yes | yes | extra GPO |
| Windows 11 IoT | no | no | |
| Windows 10 22H2 | detect-only | opt-in tweaks only | not worth full support |
| Future 26H2 | refuse unknown | catalog maxBuild | |

Every tweak carries `minBuild` / `editions`. Engine never assumes 24H2 registry still exists on 25H2 (WinUtil already documents a broken Start Menu tweak).
