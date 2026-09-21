using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsLab.Core;

namespace WindowsLab.Backup;

public sealed class FileBackupStore : IBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public FileBackupStore(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(ConfigChannels.MachineRoot, "backups");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public BackupManifest Create(BackupCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                 + "-" + Guid.NewGuid().ToString("N")[..8];
        var dir = Path.Combine(RootPath, id);
        Directory.CreateDirectory(dir);

        var manifest = new BackupManifest(
            id,
            DateTimeOffset.UtcNow,
            request.Reason,
            request.TweakIds.ToArray(),
            request.HighestRisk,
            request.Registry.ToArray(),
            request.Services.ToArray(),
            request.Tasks.ToArray(),
            request.Power.ToArray(),
            request.Logical.ToArray(),
            request.RestorePoint);

        var path = Path.Combine(dir, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        return manifest;
    }

    public BackupManifest? GetManifest(string backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);
        if (backupId.Contains("..", StringComparison.Ordinal) ||
            backupId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new UnauthorizedAccessException("Invalid backupId.");
        }

        var path = Path.GetFullPath(Path.Combine(RootPath, backupId, "manifest.json"));
        var rootFull = Path.GetFullPath(RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("backupId escapes backup root.");
        }

        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path), JsonOptions);
    }

    public IReadOnlyList<BackupSummary> List()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        var list = new List<BackupSummary>();
        foreach (var dir in Directory.EnumerateDirectories(RootPath))
        {
            var manifest = GetManifest(Path.GetFileName(dir));
            if (manifest is null)
            {
                continue;
            }

            list.Add(new BackupSummary(
                manifest.BackupId,
                manifest.CreatedUtc,
                manifest.Reason,
                manifest.TweakIds,
                manifest.HighestRisk));
        }

        return list.OrderByDescending(b => b.CreatedUtc).ToArray();
    }

    public BackupRestoreResult Restore(string backupId, IBackupRestorer restorer)
    {
        ArgumentNullException.ThrowIfNull(restorer);
        var manifest = GetManifest(backupId);
        if (manifest is null)
        {
            return new BackupRestoreResult(false, $"Backup not found: {backupId}", 0);
        }

        var count = 0;
        try
        {
            foreach (var entry in manifest.Registry.Reverse())
            {
                restorer.RestoreRegistry(entry);
                count++;
            }

            foreach (var entry in manifest.Services.Reverse())
            {
                restorer.RestoreService(entry);
                count++;
            }

            foreach (var entry in manifest.Tasks.Reverse())
            {
                restorer.RestoreTask(entry);
                count++;
            }

            foreach (var entry in manifest.Power.Reverse())
            {
                restorer.RestorePower(entry);
                count++;
            }

            return new BackupRestoreResult(true, $"Restored {count} layer(s) from {backupId}.", count);
        }
        catch (Exception ex)
        {
            return new BackupRestoreResult(false, ex.Message, count);
        }
    }
}
