using System.Windows;
using System.Windows.Controls;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private List<ToolHubRow> _toolHubRows = [];

    private void EnsureToolHubLoaded()
    {
        if (_toolHubRows.Count > 0)
        {
            return;
        }

        var entries = ToolHubService.LoadEntries();
        var rows = new List<ToolHubRow>(entries.Count);
        foreach (var entry in entries)
        {
            var row = new ToolHubRow(entry);
            row.PrimaryRequested += OnToolHubPrimary;
            row.DocsRequested += OnToolHubDocs;
            rows.Add(row);
        }

        _toolHubRows = rows;
    }

    private void RefreshToolHub()
    {
        if (ToolHubGroups is null)
        {
            return;
        }

        EnsureToolHubLoaded();
        var apps = _session?.Applications ?? Array.Empty<ApplicationDefinition>();
        var facts = InstalledAppDetector.Scan();
        foreach (var row in _toolHubRows)
        {
            row.RefreshLocalizedChrome();
            ApplyToolHubRowState(row, apps, facts);
        }

        ToolHubGroups.ItemsSource = BuildToolHubGroups(_toolHubRows);
    }

    private static ToolHubGroupRow[] BuildToolHubGroups(IReadOnlyList<ToolHubRow> rows) =>
        rows
            .GroupBy(r => r.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ToolHubGroupRow(g.Key, GroupTitle(g.Key), g.ToArray()))
            .ToArray();

    private static string GroupTitle(string key) => key.ToLowerInvariant() switch
    {
        "desktop" => Loc.T("tools.hub.group.desktop"),
        "performance" => Loc.T("tools.hub.group.performance"),
        "network" => Loc.T("tools.hub.group.network"),
        "accessibility" => Loc.T("tools.hub.group.accessibility"),
        _ => key
    };

    private static void ApplyToolHubRowState(
        ToolHubRow row,
        IReadOnlyList<ApplicationDefinition> apps,
        IReadOnlyList<InstalledAppFact> facts)
    {
        var entry = row.Entry;
        var present = ToolHubService.IsPresent(entry, apps, facts);
        row.IsPresent = present;

        switch (entry.Kind)
        {
            case ToolHubKind.Winget:
                row.StatusText = Loc.T(present
                    ? "tools.hub.status.installed"
                    : "tools.hub.status.missing");
                row.ActionText = Loc.T(present ? "tools.hub.action.open" : "tools.hub.action.install");
                break;
            case ToolHubKind.Steam:
                row.StatusText = Loc.T(present
                    ? "tools.hub.status.installed"
                    : "tools.hub.status.steam");
                row.ActionText = Loc.T(present ? "tools.hub.action.open" : "tools.hub.action.steam");
                break;
            case ToolHubKind.WindowsSettings:
                row.StatusText = Loc.T("tools.hub.status.settings");
                row.ActionText = Loc.T("tools.hub.action.settings");
                break;
            case ToolHubKind.ExternalUrl:
                row.StatusText = Loc.T(present
                    ? "tools.hub.status.installed"
                    : "tools.hub.status.external");
                row.ActionText = Loc.T(present
                    ? "tools.hub.action.open"
                    : "tools.hub.action.website");
                break;
            case ToolHubKind.DocsOnly:
                row.StatusText = Loc.T(present
                    ? "tools.hub.status.detected"
                    : "tools.hub.status.docs");
                row.ActionText = Loc.T(present ? "tools.hub.action.openPanel" : "tools.hub.action.docs");
                break;
            default:
                row.StatusText = "";
                row.ActionText = Loc.T("tools.hub.action.docs");
                break;
        }

        row.ActionEnabled = true;
    }

    private void OnToolHubDocs(ToolHubRow row)
    {
        if (!ToolHubService.TryOpenUrl(row.Entry.Url))
        {
            SetQuickStatus(Loc.T("tools.hub.openFail"));
        }
        else
        {
            SetQuickStatus(Loc.T("tools.hub.docs") + ": " + row.Title);
        }
    }

    private void OnToolHubPrimary(ToolHubRow row)
    {
        var entry = row.Entry;
        switch (entry.Kind)
        {
            case ToolHubKind.Winget:
                if (row.IsPresent)
                {
                    if (!ToolHubService.TryLaunch(entry))
                    {
                        SetQuickStatus(Loc.T("tools.hub.openFail"));
                        MessageBox.Show(Loc.T("tools.hub.openFail"), entry.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        SetQuickStatus(Loc.T("tools.hub.action.open") + ": " + entry.Title);
                    }

                    return;
                }

                InstallToolHubWinget(row);
                return;

            case ToolHubKind.Steam:
                if (row.IsPresent && ToolHubService.TryLaunch(entry))
                {
                    SetQuickStatus(Loc.T("tools.hub.action.open") + ": " + entry.Title);
                    return;
                }

                if (!ToolHubService.TryOpenSteamApp(entry.SteamAppId) && !ToolHubService.TryOpenUrl(entry.Url))
                {
                    SetQuickStatus(Loc.T("tools.hub.openFail"));
                }
                else
                {
                    SetQuickStatus(Loc.T("tools.hub.action.steam") + ": " + entry.Title);
                }

                return;

            case ToolHubKind.WindowsSettings:
                if (!ToolHubService.TryOpenSettings(entry.SettingsUri))
                {
                    SetQuickStatus(Loc.T("tools.hub.openFail"));
                }
                else
                {
                    SetQuickStatus(Loc.T("tools.hub.action.settings") + ": " + entry.Title);
                }

                return;

            case ToolHubKind.ExternalUrl:
                if (row.IsPresent && ToolHubService.TryLaunch(entry))
                {
                    SetQuickStatus(Loc.T("tools.hub.action.open") + ": " + entry.Title);
                    return;
                }

                if (!ToolHubService.TryOpenUrl(entry.Url))
                {
                    SetQuickStatus(Loc.T("tools.hub.openFail"));
                }
                else
                {
                    SetQuickStatus(Loc.T("tools.hub.action.website") + ": " + entry.Title);
                }

                return;

            case ToolHubKind.DocsOnly:
                if (row.IsPresent && ToolHubService.TryLaunch(entry))
                {
                    SetQuickStatus(Loc.T("tools.hub.action.openPanel") + ": " + entry.Title);
                    return;
                }

                if (!ToolHubService.TryOpenUrl(entry.Url))
                {
                    SetQuickStatus(Loc.T("tools.hub.openFail"));
                }
                else
                {
                    SetQuickStatus(Loc.T("tools.hub.action.docs") + ": " + entry.Title);
                }

                return;

            default:
                OnToolHubDocs(row);
                return;
        }
    }

    private void InstallToolHubWinget(ToolHubRow row)
    {
        var apps = _session?.Applications ?? Array.Empty<ApplicationDefinition>();
        var app = ToolHubService.ResolveCatalogApp(row.Entry, apps);
        if (app is null)
        {
            SetQuickStatus(Loc.T("tools.hub.missingCatalog"));
            MessageBox.Show(Loc.T("tools.hub.missingCatalog"), row.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (app.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
        {
            SetQuickStatus(Loc.IsEnglish
                ? "Policy: UNKNOWN/EXPERIMENTAL apps cannot be installed."
                : "Política: apps UNKNOWN/EXPERIMENTAL no se instalan.");
            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("tools.hub.confirmInstall")
                .Replace("{0}", app.Title, StringComparison.Ordinal)
                .Replace("{1}", app.WingetId, StringComparison.Ordinal)
                .Replace("{2}", app.Evidence.ToString(), StringComparison.Ordinal)
                .Replace("{3}", app.Risk.ToString(), StringComparison.Ordinal)
            + "\n\n" + app.Description
            + (string.IsNullOrWhiteSpace(row.RiskNote) ? "" : "\n\n" + row.RiskNote),
            row.Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            SetQuickStatus(Loc.T("tools.hub.cancelled"));
            return;
        }

        SetQuickStatus(Loc.T("tools.hub.working").Replace("{0}", app.Title, StringComparison.Ordinal));
        row.ActionEnabled = false;
        Task.Run(() => WingetClient.Install(app, allowElevate: true)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                row.ActionEnabled = true;
                if (t.IsCanceled)
                {
                    SetQuickStatus(Loc.T("tools.hub.cancelled"));
                    return;
                }

                if (t.IsFaulted)
                {
                    var err = Truncate(t.Exception?.GetBaseException().Message ?? "error", 400);
                    SetQuickStatus(err);
                    MessageBox.Show(err, row.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = t.Result;
                SetQuickStatus($"{result.Outcome}: {Truncate(result.Message, 280)}");
                MessageBox.Show(
                    result.Message + (result.LogPath is null ? "" : "\n\nLog: " + result.LogPath),
                    row.Title + " — " + result.Outcome,
                    MessageBoxButton.OK,
                    result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
                if (result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled)
                {
                    var profileItem = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced";
                    Reload(ParseProfile(profileItem));
                }

                RefreshToolHub();
            });
        }, TaskScheduler.Default);
    }
}
