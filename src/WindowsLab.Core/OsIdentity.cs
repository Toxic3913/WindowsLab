namespace WindowsLab.Core;

public sealed record OsIdentityFacts(
    int Major,
    int Minor,
    int Build,
    int Ubr,
    string DisplayVersion,
    string EditionId,
    string ProductName,
    string? CompositionEditionId);

public sealed record OsIdentity(
    int Major,
    int Minor,
    int Build,
    int Ubr,
    string DisplayVersion,
    string EditionId,
    string ProductName,
    string? CompositionEditionId,
    bool IsWindows11,
    string FamilyLabel,
    string? ProductNameWarning);
