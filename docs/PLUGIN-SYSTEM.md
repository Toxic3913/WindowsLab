# PLUGIN SYSTEM

Date: 2026-09-05

## Why plugins

Vendor surfaces (NVIDIA NVAPI, AMD ADL, Intel IGC, VMware VIX, Docker engine, WSL) change faster than WindowsLab Core and may need extra rights or DLLs we do not want in the default trust set.

## Model

**Out-of-process plugin host** with a capability manifest. Not in-process MEF in the UI.

```
WindowsLab.Worker or App
    -> spawn WindowsLab.PluginHost.exe
        -> load plugin assembly from %ProgramData%\WindowsLab\plugins\<id>\
```

IPC: same JSON-RPC schema as the worker, extra methods under `plugin.<id>.*`.

## Manifest (conceptual)

```json
{
  "id": "nvidia.gpu",
  "version": "1.0.0",
  "capabilities": ["gpu.read", "gpu.clocks.read"],
  "minEngine": "1.0.0",
  "os": { "minBuild": 22000 },
  "elevation": "optional",
  "signature": "Authenticode"
}
```

Capabilities examples: `gpu.read`, `gpu.write`, `virt.hyperv.read`, `docker.read`, `wsl.read`, `security.read`, `storage.smart`.

**Write capabilities require HIGH UI** even inside a plugin.

## Install / update / remove

```
windowslab plugin install .\nvidia.gpu.nupkg
windowslab plugin verify nvidia.gpu
windowslab plugin update nvidia.gpu
windowslab plugin remove nvidia.gpu
```

Install steps: hash, Authenticode, schema, capability review prompt, copy under ProgramData, register.

No marketplace in v1. Sideload only.

## Trust tiers (merged specialist review)

| Tier | Load | Who |
| --- | --- | --- |
| 0 BuiltIn | In-process ALC, thumbprint-pinned | First-party only |
| 1 Trusted | Out-of-process worker | Signed partner plugins |
| 2 Community | Out-of-process + strict Job Object, **fail-closed** | Sideload |
| 3 Compute | WASM later, no Win32 | Report/SARIF transforms |

`AssemblyLoadContext` is **not** a security boundary ([Microsoft plugin guidance](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)). MEF is composition only. PowerShell modules may run **inside** a capability-scoped worker, never as the trust boundary.

**Fail-closed:** if Job Object attach or signature verify fails, the plugin does not load. No in-process fallback for Tier 2+.

Host never passes raw tokens. Access is broker RPC with allowlisted capabilities. Plugins must not ship a second copy of `WindowsLab.Plugin.Abstractions`.

AppContainer vs restricted token for workers: **UNKNOWN — prototype in Phase 10** (D013).

NVIDIA/AMD SDK redistribution for a marketplace: **UNKNOWN**, legal review before bundling.

## Alternatives considered

| Approach | Why not as the only model |
| --- | --- |
| In-process MEF / AssemblyLoadContext | No security boundary; OK only for Tier 0 |
| PowerShell modules as plugins | Same InvokeScript problem |
| WASM | Strong sandbox, cannot call NVAPI/Hyper-V |
| COM out-of-proc | Viable; JSON-RPC is enough for v1 |

## First-party plugin candidates (later phases)

NVIDIA, AMD, Intel, VMware, Hyper-V, Docker, WSL, Kubernetes, Storage/SMART, Security extras, Gaming overlays inventory.

Core still detects **presence** of Docker/WSL without a plugin. Plugins add depth (docker info, nvidia-smi).
