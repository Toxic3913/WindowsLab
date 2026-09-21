namespace WindowsLab.Core;

/// <summary>Phase 3 contract — implemented by WindowsLab.Backup.</summary>
public interface IBackupStore
{
    string RootPath { get; }

    BackupManifest Create(BackupCreateRequest request);

    BackupManifest? GetManifest(string backupId);

    IReadOnlyList<BackupSummary> List();

    BackupRestoreResult Restore(string backupId, IBackupRestorer restorer);
}

public sealed record BackupCreateRequest(
    string Reason,
    IReadOnlyList<string> TweakIds,
    RiskLevel HighestRisk,
    IReadOnlyList<BackupRegistryEntry> Registry,
    IReadOnlyList<BackupServiceEntry> Services,
    IReadOnlyList<BackupTaskEntry> Tasks,
    IReadOnlyList<BackupPowerEntry> Power,
    IReadOnlyList<BackupLogicalEntry> Logical,
    BackupRestorePointInfo? RestorePoint);

public sealed record BackupManifest(
    string BackupId,
    DateTimeOffset CreatedUtc,
    string Reason,
    IReadOnlyList<string> TweakIds,
    RiskLevel HighestRisk,
    IReadOnlyList<BackupRegistryEntry> Registry,
    IReadOnlyList<BackupServiceEntry> Services,
    IReadOnlyList<BackupTaskEntry> Tasks,
    IReadOnlyList<BackupPowerEntry> Power,
    IReadOnlyList<BackupLogicalEntry> Logical,
    BackupRestorePointInfo? RestorePoint);

public sealed record BackupSummary(
    string BackupId,
    DateTimeOffset CreatedUtc,
    string Reason,
    IReadOnlyList<string> TweakIds,
    RiskLevel HighestRisk);

public sealed record BackupRegistryEntry(
    string TweakId,
    string Hive,
    string Path,
    string Name,
    string ValueKind,
    string? PreviousValue,
    bool ValueExisted);

public sealed record BackupServiceEntry(
    string TweakId,
    string ServiceName,
    int? PreviousStartType,
    string? QueryConfig,
    string? XmlPath);

public sealed record BackupTaskEntry(
    string TweakId,
    string TaskPath,
    string? XmlPath,
    bool? PreviouslyEnabled);

public sealed record BackupPowerEntry(
    string TweakId,
    string? PreviousSchemeGuid,
    string? ExportPath);

public sealed record BackupLogicalEntry(
    string TweakId,
    string? ActualDisplay,
    string? DesiredDisplay,
    bool MatchesDesired);

public sealed record BackupRestorePointInfo(
    bool Attempted,
    bool Succeeded,
    int? SequenceNumber,
    string? Message);

public sealed record BackupRestoreResult(bool Succeeded, string Message, int RestoredCount);

/// <summary>Callback used by the store to apply inverse ops (registry/service/…). Implemented by Tweaks.</summary>
public interface IBackupRestorer
{
    void RestoreRegistry(BackupRegistryEntry entry);

    void RestoreService(BackupServiceEntry entry);

    void RestoreTask(BackupTaskEntry entry);

    void RestorePower(BackupPowerEntry entry);
}
