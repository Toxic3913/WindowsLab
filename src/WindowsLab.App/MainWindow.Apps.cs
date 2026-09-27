using System.Windows;
using System.Windows.Controls;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private void RenderChannels()
    {
        var channels = ConfigChannels.Describe(CatalogLocator.FindTweaksDirectory());
        ChannelList.ItemsSource = channels.Select(c =>
            $"{c.Title}\n{c.Path}\n{c.Role}").ToArray();
        SettingsCatalogPath.Text = "Catálogo: " + (CatalogLocator.FindTweaksDirectory() ?? "(no encontrado)")
            + "  ·  Checklist: " + (CatalogLocator.FindChecklistsDirectory() ?? "(no encontrado)")
            + "  ·  Packs: " + (CatalogLocator.FindPresetsDirectory() ?? "(no encontrado)")
            + "  ·  Apps: " + (CatalogLocator.FindApplicationsDirectory() ?? "(no encontrado)");
    }

    private void RenderApps()
    {
        if (_session is null)
        {
            return;
        }

        var category = (AppsCategoryBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                       ?? (AppsCategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filter = string.Equals(category, "all", StringComparison.OrdinalIgnoreCase) ? null : category;
        var search = AppsSearchBox?.Text?.Trim() ?? "";

        var rows = _session.AppRecommendations
            .Where(r => filter is null || string.Equals(r.Category, filter, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(search)
                        || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || r.WingetId.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || r.AppId.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(r => new AppRow
            {
                Id = r.AppId,
                Title = r.Title,
                Category = r.Category,
                Score = r.Score,
                Evidence = r.Evidence.ToString(),
                InstalledLabel = r.Installed ? (Loc.IsEnglish ? "installed" : "instalado") : (Loc.IsEnglish ? "missing" : "falta"),
                Why = r.Why,
                WingetId = r.WingetId,
                Privacy = Math.Round(r.Axes.Privacy, 2),
                Telemetry = Math.Round(r.Axes.Telemetry, 2),
                Security = Math.Round(r.Axes.Security, 2),
                Performance = Math.Round(r.Axes.Performance, 2),
                Ecosystem = Math.Round(r.Axes.Ecosystem, 2)
            })
            .ToList();
        AppsGrid.ItemsSource = rows;
        AppsStatusText.Text = $"{rows.Count} apps · {_session.Profile}";
    }

    private void AppsCategoryBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _session is null)
        {
            return;
        }

        RenderApps();
    }

    private void AppsSearch_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _session is null)
        {
            return;
        }

        RenderApps();
    }

    private void InstallApp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || AppsGrid.SelectedItem is not AppRow row)
        {
            AppsStatusText.Text = Loc.IsEnglish ? "Select an app row first." : "Selecciona una fila primero.";
            return;
        }

        var app = _session.Applications.FirstOrDefault(a => string.Equals(a.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            AppsStatusText.Text = "App not found in catalog.";
            return;
        }

        if (app.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
        {
            AppsStatusText.Text = Loc.IsEnglish
                ? "Policy: UNKNOWN/EXPERIMENTAL apps cannot be installed."
                : "Política: apps UNKNOWN/EXPERIMENTAL no se instalan.";
            return;
        }

        var confirm = MessageBox.Show(
            Loc.IsEnglish
                ? $"Install {app.Title} via winget ({app.WingetId})?\nMay prompt UAC for machine-wide packages."
                : $"¿Instalar {app.Title} con winget ({app.WingetId})?\nPuede pedir UAC si el paquete es de máquina.",
            "WindowsLab",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            AppsStatusText.Text = Loc.IsEnglish ? "Cancelled." : "Cancelado.";
            return;
        }

        AppsStatusText.Text = Loc.IsEnglish ? "Installing…" : "Instalando…";
        BtnInstallApp.IsEnabled = false;
        Task.Run(() => WingetClient.Install(app, allowElevate: true)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnInstallApp.IsEnabled = true;
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.GetBaseException();
                    AppsStatusText.Text = Truncate(ex?.Message ?? "error", 300);
                    return;
                }

                var result = t.Result;
                AppsStatusText.Text = $"{result.Outcome}: {result.Message}"
                    + (result.LogPath is null ? "" : " · " + result.LogPath);
                if (result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled)
                {
                    var profileItem = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced";
                    Reload(ParseProfile(profileItem));
                }
            });
        }, TaskScheduler.Default);
    }

    private void OpenDefaultApps_OnClick(object sender, RoutedEventArgs e) =>
        Launch("ms-settings:defaultapps", Loc.IsEnglish
            ? "Could not open Default apps settings."
            : "No se pudo abrir Apps predeterminadas.");

    private void Persist()
    {
        var profile = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                      ?? _session?.Profile.ToString().ToLowerInvariant()
                      ?? "balanced";
        var preset = _lastMontageId ?? OperatorSettingsStore.Load().LastPresetId;
        var lang = Loc.Language;
        var theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "dark";
        var allowSystem = ChkAllowSystemApply.IsChecked == true;
        var existingToken = OperatorSettingsStore.Load().GitHubToken;
        OperatorSettingsStore.Save(new OperatorSettings(profile, preset, DateTimeOffset.UtcNow, lang, theme, allowSystem, existingToken));
    }

    private void SaveGithubToken_OnClick(object sender, RoutedEventArgs e)
    {
        var existing = OperatorSettingsStore.Load();
        var token = string.IsNullOrWhiteSpace(GithubTokenBox.Password) ? null : GithubTokenBox.Password.Trim();
        OperatorSettingsStore.Save(existing with
        {
            GitHubToken = token,
            SavedUtc = DateTimeOffset.UtcNow
        });
        GithubTokenBox.Password = "";
        UpdateStatusText.Text = token is null
            ? (Loc.IsEnglish ? "GitHub token cleared." : "Token de GitHub borrado.")
            : (Loc.IsEnglish ? "GitHub token saved (local only)." : "Token de GitHub guardado (solo local).");
    }

    private void AllowSystemApply_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Persist();
        if (_session is not null)
        {
            RenderHome();
        }
    }
}
