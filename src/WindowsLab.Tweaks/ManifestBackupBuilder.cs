using System.Globalization;
using WindowsLab.Backup;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public static class ManifestBackupBuilder
{
    public static BackupCreateRequest Build(
        IReadOnlyList<TweakDefinition> tweaks,
        IRegistryReader reader,
        string reason,
        BackupRestorePointInfo? restorePoint,
        string? blobDirectory = null)
    {
        var registry = new List<BackupRegistryEntry>();
        var services = new List<BackupServiceEntry>();
        var tasks = new List<BackupTaskEntry>();
        var power = new List<BackupPowerEntry>();
        var logical = new List<BackupLogicalEntry>();
        var highest = RiskLevel.Low;
        var blobs = blobDirectory ?? Path.Combine(ConfigChannels.MachineRoot, "tmp", "backup-blobs");
        Directory.CreateDirectory(blobs);

        foreach (var tweak in tweaks)
        {
            if (tweak.Risk > highest)
            {
                highest = tweak.Risk;
            }

            var det = TweakDetector.Detect(tweak, reader);
            logical.Add(new BackupLogicalEntry(
                tweak.Id, det.ActualDisplay, det.DesiredDisplay, det.MatchesDesired));

            foreach (var op in tweak.Ops)
            {
                var kind = op.Kind?.ToLowerInvariant() ?? "";
                switch (kind)
                {
                    case "registry":
                    {
                        object? previous = null;
                        var existed = false;
                        try
                        {
                            previous = reader.GetValue(op.Hive!, op.Path!, op.Name!);
                            existed = previous is not null;
                        }
                        catch
                        {
                            // record as missing
                        }

                        registry.Add(new BackupRegistryEntry(
                            tweak.Id,
                            op.Hive!,
                            op.Path!,
                            op.Name!,
                            op.ValueKind ?? "DWord",
                            FormatStored(previous),
                            existed));
                        break;
                    }
                    case "service":
                    {
                        var name = op.Name!;
                        var start = ServiceOpExecutor.ReadStartType(name);
                        services.Add(new BackupServiceEntry(
                            tweak.Id,
                            name,
                            start,
                            ServiceOpExecutor.CaptureConfig(name),
                            null));
                        break;
                    }
                    case "task":
                    {
                        var path = op.Path ?? op.Name!;
                        var xml = TaskOpExecutor.ExportXml(path);
                        string? saved = null;
                        if (xml is not null && File.Exists(xml))
                        {
                            saved = Path.Combine(blobs, Guid.NewGuid().ToString("N") + ".xml");
                            File.Copy(xml, saved, overwrite: true);
                        }

                        tasks.Add(new BackupTaskEntry(
                            tweak.Id, path, saved, TaskOpExecutor.IsEnabled(path)));
                        break;
                    }
                    case "power":
                    {
                        var guid = PowerOpExecutor.GetActiveSchemeGuid();
                        var export = guid is null ? null : PowerOpExecutor.ExportScheme(guid, blobs);
                        power.Add(new BackupPowerEntry(tweak.Id, guid, export));
                        break;
                    }
                }
            }
        }

        return new BackupCreateRequest(
            reason,
            tweaks.Select(t => t.Id).ToArray(),
            highest,
            registry,
            services,
            tasks,
            power,
            logical,
            restorePoint);
    }

    private static string? FormatStored(object? value) => value switch
    {
        null => null,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        byte[] b => Convert.ToHexString(b),
        string[] arr => string.Join('\0', arr),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}

public sealed class BackupRestorer : IBackupRestorer
{
    private readonly IRegistryWriter _writer;

    public BackupRestorer(IRegistryWriter writer)
    {
        _writer = writer;
    }

    public void RestoreRegistry(BackupRegistryEntry entry)
    {
        var kind = TweakApplicator.ParseKind(entry.ValueKind);
        if (!entry.ValueExisted || entry.PreviousValue is null)
        {
            _writer.DeleteValue(entry.Hive, entry.Path, entry.Name);
        }
        else
        {
            _writer.SetValue(entry.Hive, entry.Path, entry.Name,
                TweakApplicator.CoerceValue(entry.PreviousValue, kind), kind);
        }
    }

    public void RestoreService(BackupServiceEntry entry)
    {
        if (entry.PreviousStartType is int start)
        {
            ServiceOpExecutor.RestoreStartType(entry.ServiceName, start);
        }
    }

    public void RestoreTask(BackupTaskEntry entry)
    {
        if (entry.PreviouslyEnabled is bool enabled)
        {
            TaskOpExecutor.SetEnabled(entry.TaskPath, enabled);
        }
    }

    public void RestorePower(BackupPowerEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.PreviousSchemeGuid))
        {
            PowerOpExecutor.SetActiveScheme(entry.PreviousSchemeGuid!);
        }
    }
}
