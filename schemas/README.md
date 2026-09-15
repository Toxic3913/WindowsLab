# schemas/

JSON Schema (draft 2020-12) for catalog and reports.

| File | Used by |
| --- | --- |
| `os-identity.schema.json` | Phase 1 CLI / Core |
| `tweak.schema.json` | Phase 4 catalog |
| `application.schema.json` | Beta 0.2 Applications (D019) |
| `backup-manifest.schema.json` | Phase 3 backup store |

Validate catalog JSON against these files before merge. There is **no** `InvokeScript` op kind.
