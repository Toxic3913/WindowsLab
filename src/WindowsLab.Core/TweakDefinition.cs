namespace WindowsLab.Core;

public sealed record RegistryDetect(
    string Hive,
    string Path,
    string Name,
    string ValueKind,
    /// <summary>
    /// When the registry value is absent, treat detection as this value (winutil-style DefaultState).
    /// Null = absent never matches (legacy). Display still marks that the key was absent.
    /// </summary>
    string? DefaultIfAbsent = null);

public sealed record TweakOp(
    string Kind,
    string? Hive,
    string? Path,
    string? Name,
    string? ValueKind,
    object? Value);

public sealed record TweakDefinition(
    string Id,
    string Title,
    string Description,
    string Category,
    RiskLevel Risk,
    EvidenceGrade Evidence,
    IReadOnlyList<string> EvidenceRefs,
    int MinBuild,
    IReadOnlyList<string> Editions,
    IReadOnlyList<string> Profiles,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Conflicts,
    bool RequiresReboot,
    bool AffectsSecurity,
    bool AffectsUpdates,
    bool AffectsCompatibility,
    RegistryDetect Detect,
    string DesiredEquals,
    IReadOnlyList<TweakOp> Ops);

public sealed record TweakDetection(
    string TweakId,
    ProbeStatus Status,
    string? ActualDisplay,
    string? DesiredDisplay,
    bool MatchesDesired,
    string? Message,
    bool ValueAbsent = false,
    bool InterpretedFromDefault = false);
