using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.Worker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!WorkerClient.IsElevated())
        {
            Console.Error.WriteLine("WindowsLab.Worker must run elevated.");
            return ExitCodes.PolicyBlocked;
        }

        ConfigChannels.EnsureRuntimeFolders();
        Console.WriteLine("WindowsLab.Worker listening on \\\\.\\pipe\\" + WorkerProtocol.PipeName);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await using var server = CreatePipe();
                await server.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);
                var reqBytes = await WorkerClient.ReadFrameAsync(server, cts.Token).ConfigureAwait(false);
                var request = JsonSerializer.Deserialize<WorkerRequest>(reqBytes, WorkerProtocol.JsonOptions)
                              ?? new WorkerRequest("Health", null);
                var response = await HandleAsync(request).ConfigureAwait(false);
                var respBytes = JsonSerializer.SerializeToUtf8Bytes(response, WorkerProtocol.JsonOptions);
                await WorkerClient.WriteFrameAsync(server, respBytes, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Worker loop: " + ex.Message);
            }
        }

        return ExitCodes.Ok;
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        // Only the elevating user (and admins) may talk to the Worker — not BuiltinUsers (LPE).
        var self = WindowsIdentity.GetCurrent().User
                   ?? new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.AddAccessRule(new PipeAccessRule(
            self,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            WorkerProtocol.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private static Task<WorkerResponse> HandleAsync(WorkerRequest request)
    {
        try
        {
            return request.Method switch
            {
                "Health" => Task.FromResult(Ok(new { elevated = true, version = WorkerProtocol.ProtocolVersion })),
                "ApplyJob" => Task.FromResult(HandleApply(request)),
                "RollbackJob" => Task.FromResult(HandleRollback(request)),
                _ => Task.FromResult(new WorkerResponse(false, "Unknown method: " + request.Method))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WorkerResponse(false, ex.Message));
        }
    }

    private static bool IsTrustedCatalogDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var allowed = new List<string>();
        var found = CatalogLocator.FindTweaksDirectory();
        if (found is not null)
        {
            allowed.Add(Path.GetFullPath(found).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        var baseDir = AppContext.BaseDirectory;
        allowed.Add(Path.GetFullPath(Path.Combine(baseDir, "catalog", "tweaks"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        allowed.Add(Path.GetFullPath(Path.Combine(ConfigChannels.MachineRoot, "catalog", "tweaks"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return allowed.Any(a => string.Equals(full, a, StringComparison.OrdinalIgnoreCase));
    }

    private static WorkerResponse HandleApply(WorkerRequest request)
    {
        if (request.Params is null)
        {
            return new WorkerResponse(false, "Missing params");
        }

        var p = request.Params.Value.Deserialize<WorkerApplyParams>(WorkerProtocol.JsonOptions);
        if (p is null || p.TweakIds.Count == 0)
        {
            return new WorkerResponse(false, "Invalid ApplyJob params");
        }

        if (!IsTrustedCatalogDirectory(p.CatalogDirectory))
        {
            return new WorkerResponse(false, "CatalogDirectory is not an allowed WindowsLab catalog path.");
        }

        var catalog = TweakCatalogLoader.LoadDirectory(p.CatalogDirectory);
        var tweaks = p.TweakIds
            .Select(id => catalog.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(t => t is not null)
            .Cast<TweakDefinition>()
            .ToArray();

        if (tweaks.Length == 0)
        {
            return new WorkerResponse(false, "No matching tweaks in catalog");
        }

        var reader = new LiveRegistryReader();
        var writer = new LiveRegistryWriter(allowMachineHive: true);
        var store = new FileBackupStore();

        var highest = tweaks.Max(t => t.Risk);
        var rp = RestorePointService.TryCreate("WindowsLab Worker " + string.Join(",", tweaks.Select(t => t.Id).Take(3)));
        if (!rp.Succeeded && RestorePointService.RequiresRestorePointSuccess(highest) && !p.DryRun)
        {
            return new WorkerResponse(false, "Restore point required but failed: " + rp.Message);
        }

        var createReq = ManifestBackupBuilder.Build(tweaks, reader, "worker-apply", rp, store.RootPath);
        BackupManifest? manifest = null;
        if (!p.DryRun)
        {
            manifest = store.Create(createReq);
        }

        var outcomes = new List<WorkerTweakOutcome>();
        var applied = new List<TweakDefinition>();
        foreach (var tweak in tweaks)
        {
            var result = TweakApplicator.ApplyElevated(tweak, reader, writer, p.DryRun);
            outcomes.Add(new WorkerTweakOutcome(tweak.Id, result.Outcome.ToString(), result.Message));
            if (result.Succeeded)
            {
                applied.Add(tweak);
            }
            else if (result.Outcome != ApplyOutcome.AlreadyMatched && manifest is not null)
            {
                store.Restore(manifest.BackupId, new BackupRestorer(writer));
                break;
            }
        }

        var dto = new WorkerApplyResultDto(manifest?.BackupId ?? "(dry-run)", outcomes);
        return Ok(dto);
    }

    private static WorkerResponse HandleRollback(WorkerRequest request)
    {
        if (request.Params is null)
        {
            return new WorkerResponse(false, "Missing params");
        }

        var p = request.Params.Value.Deserialize<WorkerRollbackParams>(WorkerProtocol.JsonOptions);
        if (p is null || string.IsNullOrWhiteSpace(p.BackupId))
        {
            return new WorkerResponse(false, "Invalid RollbackJob params");
        }

        var store = new FileBackupStore();
        var writer = new LiveRegistryWriter(allowMachineHive: true);
        var result = store.Restore(p.BackupId, new BackupRestorer(writer));
        return result.Succeeded ? Ok(result) : new WorkerResponse(false, result.Message);
    }

    private static WorkerResponse Ok(object result)
    {
        var el = JsonSerializer.SerializeToElement(result, WorkerProtocol.JsonOptions);
        return new WorkerResponse(true, null, el);
    }
}
