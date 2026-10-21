namespace WindowsLab.Core;

public enum ToolHubKind
{
    Winget,
    ExternalUrl,
    Steam,
    WindowsSettings,
    DocsOnly
}

/// <summary>
/// Curated Herramientas hub card (D019 winget installs + non-winget links/settings).
/// </summary>
public sealed record ToolHubEntry(
    string Id,
    string Title,
    string Group,
    string Summary,
    string SummaryEn,
    string Improves,
    string ImprovesEn,
    ToolHubKind Kind,
    string? CatalogAppId,
    string? Url,
    string? SteamAppId,
    string? SettingsUri,
    EvidenceGrade Evidence,
    string RiskNote,
    string RiskNoteEn,
    IReadOnlyList<string> LaunchExeHints);
