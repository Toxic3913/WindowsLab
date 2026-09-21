using System.Text.Json;
using WindowsLab.Backup;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public sealed record SystemApplyJobResult(
    bool Succeeded,
    string Message,
    string? BackupId,
    IReadOnlyList<ApplyResult> Results);

/// <summary>
/// PRE-CHECK → BACKUP → CHANGE → VERIFY (D020). Lab HKCU stays in-process; elevated ops via Worker.
/// </summary>
public static class SystemApplyEngine
{
    public static async Task<SystemApplyJobResult> ApplyAsync(
        IReadOnlyList<TweakDefinition> tweaks,
        IBackupStore backupStore,
        IRegistryReader reader,
        OperatorSettings settings,
        bool iAmOnLabVm,
        bool dryRun,
        string catalogDirectory,
        bool attemptRestorePoint = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tweaks);
        ArgumentNullException.ThrowIfNull(backupStore);
        ArgumentNullException.ThrowIfNull(settings);

        if (tweaks.Count == 0)
        {
            return new SystemApplyJobResult(false, "No tweaks.", null, []);
        }

        var labOnly = tweaks.All(t => TweakApplicator.IsLabEligible(t) && !TweakApplicator.NeedsElevation(t));
        if (!labOnly && !SystemApplyPolicy.IsAllowed(settings, iAmOnLabVm))
        {
            return new SystemApplyJobResult(false, SystemApplyPolicy.RefuseMessage, null, []);
        }

        foreach (var t in tweaks)
        {
            if (!TweakApplicator.IsApplyEligible(t) && !TweakApplicator.IsLabEligible(t))
            {
                return new SystemApplyJobResult(false, $"Not eligible: {t.Id}", null, []);
            }
        }

        var needsElevation = tweaks.Any(TweakApplicator.NeedsElevation);
        BackupRestorePointInfo? rp = null;
        if (attemptRestorePoint && needsElevation && !dryRun)
        {
            var highest = tweaks.Max(t => t.Risk);
            rp = RestorePointService.TryCreate("WindowsLab " + string.Join(",", tweaks.Select(t => t.Id).Take(3)));
            if (!rp.Succeeded && RestorePointService.RequiresRestorePointSuccess(highest))
            {
                return new SystemApplyJobResult(false,
                    "HIGH risk requires a restore point; creation failed: " + rp.Message, null, []);
            }
        }

        if (needsElevation && !dryRun)
        {
            await WorkerClient.EnsureRunningAsync().ConfigureAwait(false);
            var req = new WorkerRequest(
                "ApplyJob",
                JsonSerializerElement(new WorkerApplyParams(
                    tweaks.Select(t => t.Id).ToArray(),
                    catalogDirectory,
                    dryRun)));
            var resp = await WorkerClient.SendAsync(req).ConfigureAwait(false);
            if (!resp.Ok)
            {
                return new SystemApplyJobResult(false, resp.Error ?? "Worker apply failed", null, []);
            }

            var dto = resp.Result is { } el
                ? JsonSerializer.Deserialize<WorkerApplyResultDto>(el.GetRawText(), WorkerProtocol.JsonOptions)
                : null;
            if (dto is null)
            {
                return new SystemApplyJobResult(false, "Worker returned no result", null, []);
            }

            var mapped = dto.Outcomes.Select(o => new ApplyResult(
                Enum.TryParse<ApplyOutcome>(o.Outcome, true, out var oc) ? oc : ApplyOutcome.Error,
                o.Message)).ToArray();
            var ok = mapped.All(r => r.Succeeded);
            return new SystemApplyJobResult(ok,
                ok ? $"Applied via Worker. backup={dto.BackupId}" : "Some tweaks failed (Worker).",
                dto.BackupId,
                mapped);
        }

        // Unelevated / lab path
        var request = ManifestBackupBuilder.Build(tweaks, reader, "system-apply", rp);
        BackupManifest? manifest = null;
        if (!dryRun)
        {
            manifest = backupStore.Create(request);
            foreach (var entry in request.Registry)
            {
                TweakApplicator.PersistBackup(new TweakBackupEntry(
                    entry.TweakId, entry.Hive, entry.Path, entry.Name, entry.ValueKind,
                    entry.PreviousValue, entry.ValueExisted, DateTimeOffset.UtcNow));
            }
        }

        var writer = new LiveRegistryWriter(allowMachineHive: false);
        var results = new List<ApplyResult>();
        foreach (var tweak in tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = TweakApplicator.Apply(tweak, reader, writer, dryRun);
            results.Add(result);
            if (!result.Succeeded && result.Outcome != ApplyOutcome.AlreadyMatched && manifest is not null)
            {
                var restorer = new BackupRestorer(writer);
                backupStore.Restore(manifest.BackupId, restorer);
                return new SystemApplyJobResult(false,
                    $"Failed on {tweak.Id}: {result.Message}. Auto-rollback attempted.",
                    manifest.BackupId,
                    results);
            }
        }

        return new SystemApplyJobResult(
            results.All(r => r.Succeeded),
            manifest is null ? "Dry-run complete." : $"Applied. backup={manifest.BackupId}",
            manifest?.BackupId,
            results);
    }

    public static async Task<BackupRestoreResult> RollbackAsync(
        string backupId,
        IBackupStore store,
        OperatorSettings settings,
        bool iAmOnLabVm)
    {
        if (!SystemApplyPolicy.IsAllowed(settings, iAmOnLabVm))
        {
            // Allow rollback of HKCU-only manifests without system gate if all registry are HKCU
            var m = store.GetManifest(backupId);
            if (m is null)
            {
                return new BackupRestoreResult(false, "Backup not found.", 0);
            }

            var needsElev = m.Registry.Any(r =>
                                  r.Hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)
                                  || r.Hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                            || m.Services.Count > 0 || m.Tasks.Count > 0 || m.Power.Count > 0;
            if (needsElev)
            {
                return new BackupRestoreResult(false, SystemApplyPolicy.RefuseMessage, 0);
            }

            return store.Restore(backupId, new BackupRestorer(new LiveRegistryWriter()));
        }

        var manifest = store.GetManifest(backupId);
        if (manifest is null)
        {
            return new BackupRestoreResult(false, "Backup not found.", 0);
        }

        var elev = manifest.Registry.Any(r =>
                       r.Hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)
                       || r.Hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                   || manifest.Services.Count > 0 || manifest.Tasks.Count > 0 || manifest.Power.Count > 0;

        if (!elev)
        {
            return store.Restore(backupId, new BackupRestorer(new LiveRegistryWriter()));
        }

        await WorkerClient.EnsureRunningAsync().ConfigureAwait(false);
        var resp = await WorkerClient.SendAsync(new WorkerRequest(
            "RollbackJob",
            JsonSerializerElement(new WorkerRollbackParams(backupId)))).ConfigureAwait(false);
        if (!resp.Ok)
        {
            return new BackupRestoreResult(false, resp.Error ?? "Worker rollback failed", 0);
        }

        var result = resp.Result is { } el
            ? JsonSerializer.Deserialize<BackupRestoreResult>(el.GetRawText(), WorkerProtocol.JsonOptions)
            : null;
        return result ?? new BackupRestoreResult(false, "Empty rollback result", 0);
    }

    private static System.Text.Json.JsonElement JsonSerializerElement<T>(T value)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, WorkerProtocol.JsonOptions);
        using var doc = System.Text.Json.JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }
}
