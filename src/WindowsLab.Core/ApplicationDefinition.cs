namespace WindowsLab.Core;

public sealed record AppAxes(
    double Privacy,
    double Telemetry,
    double Security,
    double Performance,
    double Ecosystem);

public enum AppInstallScope
{
    User,
    Machine,
    Any
}

public sealed record ApplicationDefinition(
    string Id,
    string Title,
    string Description,
    string WingetId,
    string Category,
    EvidenceGrade Evidence,
    IReadOnlyList<string> EvidenceRefs,
    RiskLevel Risk,
    AppAxes Axes,
    IReadOnlyList<string> Profiles,
    AppInstallScope ScopePreference,
    IReadOnlyList<string> DetectNames);

public sealed record InstalledAppFact(
    string DisplayName,
    string? Publisher,
    string Source);

public enum UiThemePreference
{
    Dark,
    Light,
    System
}
