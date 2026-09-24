## Type of change

- [ ] Bug fix
- [ ] Feature / UI
- [ ] Catalog (tweak or app)
- [ ] Docs / packaging / CI
- [ ] Refactor

## Summary

<!-- What and why (1–3 sentences). -->

## Test plan

- [ ] `dotnet test WindowsLab.sln -c Release`
- [ ] App build (if UI touched)
- [ ] Lab VM `WindowsLab-Test-*` for mutating apply (if apply/Worker touched)

## Safety checklist

- [ ] No winutil / third-party `InvokeScript` catalog import
- [ ] No DiagTrack / SysMain / WSearch mutation; no `MsMpEng` kill
- [ ] UNKNOWN/EXPERIMENTAL not recommended or auto-applied
- [ ] System apply still D020-gated; Defender opt-out still D021

## Related issues

- Resolves #
