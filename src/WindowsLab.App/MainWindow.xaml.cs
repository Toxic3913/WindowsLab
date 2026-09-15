using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WindowsLab.Applications;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public sealed class PresetPick
{
    public required PresetEvaluation Eval { get; init; }

    public string Label => Eval.Total == 0
        ? $"{Eval.Preset.Title}  (elige ítems)"
        : $"{Eval.Preset.Title}  ({Eval.ReadyCount}/{Eval.Total} listos)";
}

public sealed class PresetItemRow
{
    public bool Include { get; set; }
    public string Id { get; init; } = "";
    public string Estado { get; init; } = "";
    public string Title { get; init; } = "";
    public string Actual { get; init; } = "";
    public string Desired { get; init; } = "";
    public string HowTo { get; init; } = "";
    public string? SettingsUri { get; init; }
}

public sealed class TweakRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Risk { get; init; }
    public required string Evidence { get; init; }
    public string? Actual { get; init; }
    public string? Desired { get; init; }
    public bool? Match { get; init; }
    public string? Status { get; init; }
}

public sealed class AppRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required double Score { get; init; }
    public required string Evidence { get; init; }
    public required string InstalledLabel { get; init; }
    public required string Why { get; init; }
    public required string WingetId { get; init; }
    public double Privacy { get; init; }
    public double Telemetry { get; init; }
    public double Security { get; init; }
    public double Performance { get; init; }
    public double Ecosystem { get; init; }
}

public sealed class ChecklistRow
{
    public required string Id { get; init; }
    public required string Estado { get; init; }
    public required string Section { get; init; }
    public required string Title { get; init; }
    public required string HowTo { get; init; }
    public required string? SettingsUri { get; init; }
    public required ChecklistVerdict Verdict { get; init; }
    public required ChecklistPolicy Policy { get; init; }
    public bool CanActivate => Policy != ChecklistPolicy.NeverDisable;
}

public sealed class AdviceTip
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string Uri { get; init; }
}

public partial class MainWindow : Window
{
    private LabSession? _session;
    private bool _suppressProfile;
    private bool _loading;
    private bool _suppressTheme;
    private int _sessionGeneration;

    public MainWindow()
    {
        InitializeComponent();
        ConfigChannels.EnsureRuntimeFolders();
        StartupLog.Write("MainWindow.ctor.begin");
        var saved = OperatorSettingsStore.Load();
        Loc.Language = saved.Language;
        _suppressProfile = true;
        _suppressTheme = true;
        SelectLangBox(saved.Language);
        SelectProfileBox(saved.Profile);
        SelectThemeBox(saved.Theme);
        ThemeService.Apply(OperatorSettingsStore.ParseTheme(saved.Theme));
        _suppressProfile = false;
        _suppressTheme = false;
        ApplyUiLanguage();
        HomeSummary.Text = Loc.T("home.loading");
        HomeWarn.Text = Loc.T("home.loadingHint");
        Loaded += (_, _) =>
        {
            StartupLog.Write("MainWindow.Loaded");
            BeginLoad();
        };
        StartupLog.Write("MainWindow.ctor.end");
    }

    private void BeginLoad()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        var saved = OperatorSettingsStore.Load();
        Loc.Language = saved.Language;
        ApplyUiLanguage();
        var profile = ParseProfile(saved.Profile);
        var presetId = saved.LastPresetId;
        var gen = Interlocked.Increment(ref _sessionGeneration);

        // Show the window first; heavy audit/detect must not block Show().
        Task.Run(() =>
        {
            try
            {
                return new LabSession(profile);
            }
            catch (Exception ex)
            {
                App.LogCrash("LabSession", ex);
                throw;
            }
        }).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                if (gen != _sessionGeneration)
                {
                    return;
                }

                _loading = false;
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.GetBaseException()
                             ?? new InvalidOperationException("LabSession failed without exception detail.");
                    StartupLog.Write("LabSession.fail", ex.Message);
                    HomeSummary.Text = "Error al cargar.";
                    HomeWarn.Text = Truncate(ex.Message, 400) + " — log: " + App.CrashLogPath();
                    MessageBox.Show(
                        "No se pudo cargar el inventario.\n\n" + Truncate(ex.ToString(), 1200) +
                        "\n\nstartup.log: " + StartupLog.Path +
                        "\ncrash.log: " + App.CrashLogPath(),
                        "WindowsLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ApplySession(t.Result!, presetId);
                StartupLog.Write("LabSession.ready",
                    $"tweaks={t.Result!.Catalog.Count} presets={t.Result.Presets.Count}");
            });
        }, TaskScheduler.Default);
    }

    private void ApplySession(LabSession session, string? selectPresetId = null)
    {
        _session = session;
        RenderHome();
        RenderPresets(selectPresetId);
        RenderSystem();
        RenderSecurity();
        RenderTweaks();
        RenderAdvice();
        RenderChecklist();
        RenderApps();
        RenderChannels();
        Persist();
    }

    private void Reload(UserProfile profile, string? selectPresetId = null)
    {
        HomeSummary.Text = "Recargando…";
        StartupLog.Write("Reload.begin", profile.ToString());
        var gen = Interlocked.Increment(ref _sessionGeneration);
        Task.Run(() =>
        {
            try
            {
                return new LabSession(profile);
            }
            catch (Exception ex)
            {
                App.LogCrash("LabSession.Reload", ex);
                throw;
            }
        }).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                if (gen != _sessionGeneration)
                {
                    return;
                }

                if (t.IsFaulted)
                {
                    var ex = t.Exception?.GetBaseException()
                             ?? new InvalidOperationException("Reload failed.");
                    StartupLog.Write("Reload.fail", ex.Message);
                    MessageBox.Show(
                        Truncate(ex.ToString(), 1200) +
                        "\n\nstartup.log: " + StartupLog.Path +
                        "\ncrash.log: " + App.CrashLogPath(),
                        "WindowsLab",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ApplySession(t.Result!, selectPresetId);
                StartupLog.Write("Reload.ok");
            });
        }, TaskScheduler.Default);
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private void LangBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressProfile)
        {
            return;
        }

        var lang = (LangBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant() ?? "es";
        Loc.Language = lang.StartsWith("en", StringComparison.Ordinal) ? "en" : "es";
        ApplyUiLanguage();
        Persist();
        if (Nav.SelectedIndex == 2)
        {
            RefreshLivePreview();
        }
    }

    private void SelectLangBox(string language)
    {
        var want = Loc.Language == "en" ? "EN" : "ES";
        foreach (ComboBoxItem item in LangBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), want, StringComparison.OrdinalIgnoreCase))
            {
                LangBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ApplyUiLanguage()
    {
        SubtitleText.Text = Loc.T("subtitle");
        LangLabel.Text = Loc.T("lang.label");
        ProfileLabel.Text = Loc.T("profile");
        NavHome.Content = Loc.T("nav.home");
        NavConfig.Content = Loc.T("nav.config");
        NavResources.Content = Loc.T("nav.resources");
        NavSystem.Content = Loc.T("nav.system");
        NavSecurity.Content = Loc.T("nav.security");
        NavTweaks.Content = Loc.T("nav.tweaks");
        NavAdvice.Content = Loc.T("nav.advice");
        NavChecklist.Content = Loc.T("nav.checklist");
        NavApps.Content = Loc.T("nav.apps");
        NavSettings.Content = Loc.T("nav.settings");
        ThemeLabel.Text = Loc.T("theme.label");
        UpdateThemeBoxLabels();
        HomePacksTitle.Text = Loc.T("home.packs");
        HomeProbesTitle.Text = Loc.T("home.probes");
        ConfigHint.Text = Loc.T("config.hint");
        BtnActivatePack.Content = Loc.T("btn.activatePack");
        BtnSimulatePack.Content = Loc.T("btn.simulatePack");
        BtnActivateRow.Content = Loc.T("btn.activateRow");
        BtnApplyPack.Content = Loc.T("btn.applyPack");
        BtnApplyPack.ToolTip = Loc.T("btn.applyDisabled");
        BtnApplyTweak.Content = Loc.T("btn.applyTweak");
        BtnApplyTweak.ToolTip = Loc.T("btn.applyDisabled");
        TweaksHint.Text = Loc.T("tweaks.hint");
        ChecklistHint.Text = Loc.T("checklist.hint");
        AppsHint.Text = Loc.T("apps.hint");
        AppsFilterLabel.Text = Loc.T("apps.filter");
        BtnInstallApp.Content = Loc.T("apps.install");
        BtnDefaultApps.Content = Loc.T("apps.defaults");
        TelemetryLimitHint.Text = Loc.T("telemetry.limit");
        ResourcesTitle.Text = Loc.T("resources.title");
        ResourcesHint.Text = Loc.T("resources.hint");
        BtnDesktopInfo.Content = Loc.T("btn.desktopInfo");
        BtnBgInfo.Content = Loc.T("btn.bginfo.activate");
        BtnBgInfoDocs.Content = Loc.T("btn.bginfo.docs");
        BtnGodMode.Content = Loc.T("tools.godmode");
        BtnActivateWindows.Content = Loc.T("btn.activateWindows");
        BtnLibreOffice.Content = Loc.T("btn.libreoffice");
        BtnBgInfoQuick.Content = Loc.T("btn.bginfo.activate");
        BtnDesktopInfoQuick.Content = Loc.T("btn.desktopInfo");
        BtnActivateWindowsRes.Content = Loc.T("btn.activateWindows");
        BtnLibreOfficeRes.Content = Loc.T("btn.libreoffice");
        HomeQuickTitle.Text = Loc.T("home.quick");
        AdviceHint.Text = Loc.T("advice.hint");
        AdviceBgTitle.Text = Loc.T("advice.bg.title");
        SettingsThemeTitle.Text = Loc.T("theme.label");
        SettingsChannelsTitle.Text = Loc.T("settings.channels");
        SettingsChannelsHint.Text = Loc.T("settings.channelsHint");
        BtnOpenUser.Content = Loc.T("btn.openUser");
        BtnOpenMachine.Content = Loc.T("btn.openMachine");
        SettingsInstallPath.Text = Loc.IsEnglish
            ? "Setup: D:\\WindowsLab\\artifacts\\installer\\WindowsLab-Setup.exe  ·  Inno: WindowsLab-Setup-Inno.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip"
            : "Instalador: D:\\WindowsLab\\artifacts\\installer\\WindowsLab-Setup.exe  ·  Inno: WindowsLab-Setup-Inno.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip";
    }

    private void UpdateThemeBoxLabels()
    {
        foreach (ComboBoxItem item in ThemeBox.Items)
        {
            var tag = item.Tag?.ToString() ?? "dark";
            item.Content = tag switch
            {
                "light" => Loc.T("theme.light"),
                "system" => Loc.T("theme.system"),
                _ => Loc.T("theme.dark")
            };
        }
    }

    private void SelectThemeBox(string theme)
    {
        var want = theme?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "system" => "system",
            _ => "dark"
        };
        foreach (ComboBoxItem item in ThemeBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), want, StringComparison.OrdinalIgnoreCase))
            {
                ThemeBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressTheme)
        {
            return;
        }

        var tag = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "dark";
        ThemeService.Apply(OperatorSettingsStore.ParseTheme(tag));
        Persist();
    }

    private void OpenGodMode_OnClick(object sender, RoutedEventArgs e) =>
        Launch("shell:::{ED7BA470-8E54-465E-825C-99712043E01C}", Loc.T("tools.godmodeHow"));

    private void ProfileBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressProfile)
        {
            return;
        }

        var item = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced";
        Reload(ParseProfile(item));
    }

    private static UserProfile ParseProfile(string? item) => item?.ToLowerInvariant() switch
    {
        "developer" => UserProfile.Developer,
        "gaming" => UserProfile.Gaming,
        "virtualization" => UserProfile.Virtualization,
        _ => UserProfile.Balanced
    };

    private void SelectProfileBox(string profile)
    {
        foreach (ComboBoxItem item in ProfileBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), profile, StringComparison.OrdinalIgnoreCase))
            {
                ProfileBox.SelectedItem = item;
                return;
            }
        }
    }

    private void Nav_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageHome is null || PageConfig is null || PageResources is null)
        {
            return;
        }

        var index = Nav.SelectedIndex;
        PageHome.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageConfig.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageResources.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageSystem.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageSecurity.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        PageTweaks.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        PageAdvice.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;
        PageChecklist.Visibility = index == 7 ? Visibility.Visible : Visibility.Collapsed;
        PageApps.Visibility = index == 8 ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = index == 9 ? Visibility.Visible : Visibility.Collapsed;
        if (index == 2)
        {
            RefreshLivePreview();
        }
    }

    private void RenderHome()
    {
        if (_session is null)
        {
            return;
        }

        var inv = _session.Inventory;
        HomeSummary.Text =
            $"{inv.Os.FamilyLabel} {inv.Os.DisplayVersion} (build {inv.Os.Build})  ·  {inv.Cpu.Name}  ·  {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB RAM  ·  {_session.Catalog.Count} tweaks  ·  {_session.Presets.Count} packs  ·  {_session.ChecklistResults.Count(c => c.Verdict == ChecklistVerdict.Gap)} huecos en checklist";

        var c = _session.SystemVolume;
        if (c is not null)
        {
            var freeGb = c.FreeBytes / (1024d * 1024 * 1024);
            HomeWarn.Text = freeGb < 20
                ? $"C: tiene {freeGb:0.0} GB libres. Los puntos de restauración pueden fallar. WindowsLab no mueve VSS en Beta 0."
                : $"C: {freeGb:0.0} GB libres. Usa Configurar para packs. Apply lab = HKCU seguro con backup.";
        }
        else
        {
            HomeWarn.Text = "";
        }

        HomePresetList.ItemsSource = _session.PresetEvals
            .Where(p => !p.Preset.IsCustom)
            .Select(e => new PresetPick { Eval = e })
            .ToList();
        ProbeList.ItemsSource = inv.Probes.Select(p => $"{p.Status,-12} {p.ProbeId}  {p.Message}").ToArray();
    }

    private void RenderPresets(string? selectPresetId = null)
    {
        if (_session is null)
        {
            return;
        }

        var picks = _session.PresetEvals.Select(e => new PresetPick { Eval = e }).ToList();
        PresetList.ItemsSource = picks;
        if (picks.Count == 0)
        {
            return;
        }

        if (selectPresetId is not null)
        {
            var match = picks.FindIndex(p => string.Equals(p.Eval.Preset.Id, selectPresetId, StringComparison.OrdinalIgnoreCase));
            PresetList.SelectedIndex = match >= 0 ? match : 0;
        }
        else
        {
            PresetList.SelectedIndex = 0;
        }
    }

    private void PresetList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetList.SelectedItem is not PresetPick pick)
        {
            return;
        }

        var eval = pick.Eval;
        PresetTitle.Text = eval.Preset.Title;
        PresetSummary.Text = eval.Preset.Summary;
        PresetProgress.Text = eval.Total == 0
            ? "Marca los tweaks que quieras en Configuración específica."
            : $"{eval.ReadyCount} listos · {eval.GapCount} pendientes · {eval.Items.Count} filas";
        PresetSimulateText.Text = "";
        var custom = eval.Preset.IsCustom;
        PresetGrid.ItemsSource = eval.Items.Select(i => new PresetItemRow
        {
            Include = custom ? i.Estado == "FALTA" : i.Kind != "step",
            Id = i.Id,
            Estado = i.Estado,
            Title = i.Title,
            Actual = i.Actual,
            Desired = i.Desired,
            HowTo = i.HowTo,
            SettingsUri = i.SettingsUri
        }).ToList();
        Persist();
    }

    private void HomePreset_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not PresetPick pick)
        {
            return;
        }

        Nav.SelectedIndex = 1;
        RenderPresets(pick.Eval.Preset.Id);
    }

    private void ActivateRow_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is PresetItemRow row)
        {
            Launch(row.SettingsUri, "Esta fila no tiene enlace. Sigue la columna Cómo.");
        }
    }

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

        var category = (AppsCategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filter = string.Equals(category, "all", StringComparison.OrdinalIgnoreCase) ? null : category;
        var rows = _session.AppRecommendations
            .Where(r => filter is null || string.Equals(r.Category, filter, StringComparison.OrdinalIgnoreCase))
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
        AppsStatusText.Text = $"{rows.Count} apps · perfil {_session.Profile}";
    }

    private void AppsCategoryBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
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
        if (_session is null)
        {
            return;
        }

        var profile = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced";
        var preset = (PresetList.SelectedItem as PresetPick)?.Eval.Preset.Id;
        var lang = Loc.Language;
        var theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "dark";
        OperatorSettingsStore.Save(new OperatorSettings(profile, preset, DateTimeOffset.UtcNow, lang, theme));
    }

    private void OpenUserChannel_OnClick(object sender, RoutedEventArgs e) =>
        Launch(ConfigChannels.UserRoot, "No hay carpeta de usuario.");

    private void OpenMachineChannel_OnClick(object sender, RoutedEventArgs e) =>
        Launch(ConfigChannels.MachineRoot, "No hay carpeta de máquina.");

    private IEnumerable<PresetItemRow> SelectedRows() =>
        PresetGrid.Items.OfType<PresetItemRow>().Where(r => r.Include);

    private void OpenPresetSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var uri = (PresetList.SelectedItem as PresetPick)?.Eval.Preset.SettingsUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            uri = SelectedRows().Select(r => r.SettingsUri).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        }

        Launch(uri, "Este pack no tiene página de Configuración asociada.");
    }

    private void OpenSelectedSetting_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetGrid.SelectedItem is PresetItemRow row)
        {
            Launch(row.SettingsUri, "Esta fila no tiene enlace. Sigue la columna Cómo.");
            return;
        }

        PresetSimulateText.Text = "Selecciona una fila de la tabla.";
    }

    private void SimulatePreset_OnClick(object sender, RoutedEventArgs e)
    {
        var rows = SelectedRows().ToArray();
        if (rows.Length == 0)
        {
            PresetSimulateText.Text = "Marca al menos un ítem (columna Incluir).";
            return;
        }

        var ready = rows.Count(r => r.Estado == "OK");
        var gap = rows.Count(r => r.Estado == "FALTA");
        PresetSimulateText.Text =
            $"Simulación (0 escrituras): {rows.Length} ítems, {ready} ya coinciden, {gap} pendientes. " +
            $"Aplicar pack escribe solo HKCU elegibles (con backup).";
    }

    private void ApplyPack_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            PresetSimulateText.Text = "Aún cargando.";
            return;
        }

        var ids = SelectedRows()
            .Where(r => r.Estado == "FALTA")
            .Select(r => r.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ApplyTweaksById(ids, status => PresetSimulateText.Text = status);
    }

    private void ApplyTweak_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            SimulateText.Text = "Aún cargando.";
            return;
        }

        if (TweakGrid.SelectedItem is not TweakRow row)
        {
            SimulateText.Text = "Selecciona un tweak.";
            return;
        }

        ApplyTweaksById([row.Id], status => SimulateText.Text = status);
    }

    private void ApplyTweaksById(string[] ids, Action<string> report)
    {
        if (_session is null)
        {
            report("Sin sesión.");
            return;
        }

        if (ids.Length == 0)
        {
            report("Nada pendiente (marca Incluir + FALTA, o selecciona un tweak).");
            return;
        }

        var tweaks = ids
            .Select(id => _session.Catalog.FirstOrDefault(t =>
                string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(t => t is not null)
            .Cast<TweakDefinition>()
            .Where(TweakApplicator.IsLabEligible)
            .ToArray();

        var skipped = ids.Length - tweaks.Length;
        if (tweaks.Length == 0)
        {
            report($"Ningún ítem elegible para apply lab (HKCU/LOW/OFFICIAL|STRONG). Omitidos: {skipped}.");
            return;
        }

        var list = string.Join("\n", tweaks.Select(t => "• " + t.Id));
        var confirm = MessageBox.Show(
            $"Se escribirán {tweaks.Length} valor(es) en HKCU (usuario actual).\n" +
            $"Backup previo en %LocalAppData%\\WindowsLab\\backups\\\n\n{list}\n\n" +
            (skipped > 0 ? $"({skipped} omitidos por política)\n\n" : "") +
            "¿Continuar?",
            "WindowsLab — aplicar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            report("Cancelado.");
            return;
        }

        var reader = new LiveRegistryReader();
        var writer = new LiveRegistryWriter();
        var ok = 0;
        var fail = 0;
        var lines = new List<string>();

        foreach (var tweak in tweaks)
        {
            var result = TweakApplicator.Apply(tweak, reader, writer);
            if (result.Backup is not null && result.Outcome is ApplyOutcome.Ok or ApplyOutcome.VerifyFailed)
            {
                try
                {
                    TweakApplicator.PersistBackup(result.Backup);
                }
                catch (Exception ex)
                {
                    lines.Add($"{tweak.Id}: backup falló ({ex.Message})");
                }
            }

            lines.Add($"{tweak.Id}: {result.Outcome} — {result.Message}");
            if (result.Succeeded)
            {
                ok++;
            }
            else
            {
                fail++;
            }
        }

        StartupLog.Write("Apply.batch", $"ok={ok} fail={fail}");
        RefreshDetectionsAfterApply();
        report($"Apply lab: OK {ok}, fallos {fail}.\n" + string.Join("\n", lines));
    }

    private void RefreshDetectionsAfterApply()
    {
        if (_session is null)
        {
            return;
        }

        var profile = ParseProfile((ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "balanced");
        var presetId = (PresetList.SelectedItem as PresetPick)?.Eval.Preset.Id
                       ?? OperatorSettingsStore.Load().LastPresetId;
        Reload(profile, presetId);
    }

    private void Launch(string? target, string ifMissing)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            PresetSimulateText.Text = ifMissing;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            PresetSimulateText.Text = "Abierto: " + target;
        }
        catch (Exception ex)
        {
            PresetSimulateText.Text = "No se pudo abrir " + target + ": " + ex.Message;
        }
    }

    private void OpenDesktopInfo_OnClick(object sender, RoutedEventArgs e)
    {
        var w = new DesktopInfoWindow { Owner = this };
        w.Show();
    }

    private void OpenBgInfoOfficial_OnClick(object sender, RoutedEventArgs e)
    {
        Launch("https://learn.microsoft.com/sysinternals/downloads/bginfo", "No se pudo abrir el navegador.");
    }

    private void ActivateWindows_OnClick(object sender, RoutedEventArgs e)
    {
        var (ok, msg) = QuickTools.LaunchActivateWindows();
        SetQuickStatus(ok ? msg : "Error: " + msg);
        if (!ok)
        {
            MessageBox.Show(msg, "WindowsLab", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ActivateBgInfo_OnClick(object sender, RoutedEventArgs e)
    {
        SetQuickStatus(Loc.IsEnglish ? "BGInfo…" : "BGInfo…");
        BtnBgInfo.IsEnabled = false;
        BtnBgInfoQuick.IsEnabled = false;
        Task.Run(QuickTools.LaunchOrInstallBgInfo).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnBgInfo.IsEnabled = true;
                BtnBgInfoQuick.IsEnabled = true;
                if (t.IsFaulted)
                {
                    SetQuickStatus(Truncate(t.Exception?.GetBaseException().Message ?? "error", 300));
                    return;
                }

                var (ok, msg) = t.Result;
                SetQuickStatus(msg);
                if (!ok)
                {
                    MessageBox.Show(msg, "BGInfo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }, TaskScheduler.Default);
    }

    private async void DownloadLibreOffice_OnClick(object sender, RoutedEventArgs e)
    {
        SetQuickStatus(Loc.IsEnglish ? "Downloading LibreOffice…" : "Descargando LibreOffice…");
        BtnLibreOffice.IsEnabled = false;
        BtnLibreOfficeRes.IsEnabled = false;
        try
        {
            var (ok, msg) = await QuickTools.DownloadLibreOfficeAsync().ConfigureAwait(true);
            SetQuickStatus(msg);
            if (!ok)
            {
                MessageBox.Show(msg, "LibreOffice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            BtnLibreOffice.IsEnabled = true;
            BtnLibreOfficeRes.IsEnabled = true;
        }
    }

    private void SetQuickStatus(string text)
    {
        HomeQuickStatus.Text = text;
        ResourcesStatus.Text = text;
    }

    private void RefreshLivePreview()
    {
        try
        {
            var s = LiveSystemReader.Read();
            LivePreview.Text =
                $"{s.OsLine}\nCPU {s.CpuLine}\nRAM {s.RamLine}\n{s.DiskLine}\nIP {s.NetworkLine}\nActualizado {s.CapturedUtc:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            LivePreview.Text = ex.Message;
        }
    }

    private void RenderSystem()
    {
        if (_session is null)
        {
            return;
        }

        SystemPanel.Children.Clear();
        var inv = _session.Inventory;
        AddLine(SystemPanel, $"OS: {inv.Os.FamilyLabel} {inv.Os.DisplayVersion} {inv.Os.EditionId}  ProductName={inv.Os.ProductName} (ignorado)");
        AddLine(SystemPanel, $"CPU: {inv.Cpu.Name}  cores={inv.Cpu.Cores}  threads={inv.Cpu.LogicalProcessors}  {inv.Cpu.MaxClockMhz} MHz  [{inv.Cpu.Status}]");
        AddLine(SystemPanel, $"RAM: {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB  módulos={inv.Ram.ModuleCount}  {inv.Ram.SpeedMhz} MT/s  [{inv.Ram.Status}]");
        foreach (var gpu in inv.Gpus)
        {
            var gb = gpu.DedicatedBytes is null ? "?" : $"{gpu.DedicatedBytes.Value / (1024d * 1024 * 1024):0.1} GB";
            AddLine(SystemPanel, $"GPU: {gpu.Name}  {gpu.Vendor}  VRAM {gb} via {gpu.VramSource}");
        }

        AddLine(SystemPanel, $"Placa: {inv.Board.Manufacturer} {inv.Board.Product}  BIOS {inv.Board.BiosVersion} {inv.Board.BiosDate}");
        foreach (var vol in inv.Volumes)
        {
            AddLine(SystemPanel, $"Disco {vol.Root} {vol.Label}  {vol.FreeBytes / (1024d * 1024 * 1024):0.0} / {vol.TotalBytes / (1024d * 1024 * 1024):0.0} GB  {vol.DriveType}");
        }

        AddLine(SystemPanel, "Herramientas: " + string.Join(", ", inv.Toolchain.Present));
    }

    private void RenderSecurity()
    {
        if (_session is null)
        {
            return;
        }

        SecurityPanel.Children.Clear();
        var s = _session.Inventory.Security;
        AddLine(SecurityPanel, $"VBS (Device Guard): {s.VirtualizationBasedSecurityStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "denied/unknown"}");
        AddLine(SecurityPanel, $"Defender AntivirusEnabled: {Fmt(s.DefenderEnabled)}  RTP: {Fmt(s.RealTimeProtection)}");
        AddLine(SecurityPanel, $"Firewall (perfil estándar): {Fmt(s.FirewallEnabled)}");
        AddLine(SecurityPanel, s.Notes ?? "Sin notas.");
        AddLine(SecurityPanel, "Beta 0 no recomienda desactivar Defender, HVCI ni Secure Boot.");
    }

    private void RenderTweaks()
    {
        if (_session is null)
        {
            return;
        }

        var rows = _session.Catalog.Select(t =>
        {
            var d = _session.Detections.FirstOrDefault(x => x.TweakId == t.Id);
            return new TweakRow
            {
                Id = t.Id,
                Title = t.Title,
                Category = t.Category,
                Risk = t.Risk.ToString(),
                Evidence = t.Evidence.ToString(),
                Actual = d?.ActualDisplay,
                Desired = d?.DesiredDisplay,
                Match = d?.MatchesDesired,
                Status = d?.Status.ToString()
            };
        }).ToList();
        TweakGrid.ItemsSource = rows;
    }

    private void RenderAdvice()
    {
        if (_session is null)
        {
            return;
        }

        AdviceTipsList.ItemsSource = BuildAdviceTips();
        AdviceGrid.ItemsSource = _session.Recommendations.Select(r => new
        {
            r.Score,
            r.TweakId,
            r.Title,
            r.Category,
            r.Risk,
            r.Evidence,
            r.Why
        }).ToList();
    }

    private static List<AdviceTip> BuildAdviceTips() =>
    [
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Microsoft Edge background" : "Microsoft Edge en segundo plano",
            Body = Loc.IsEnglish
                ? "Turn off Startup boost and background apps. Do not force-kill msedge.exe or remove Edge with random scripts (breaks WebView2)."
                : "Desactiva inicio al arrancar y apps en segundo plano. No mates msedge.exe ni desinstales Edge con scripts (rompe WebView2).",
            Uri = "ms-settings:startupapps"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Startup apps" : "Apps al inicio",
            Body = Loc.IsEnglish
                ? "Disable OneDrive, Teams, Outlook, Phone Link, Xbox Game Bar if unused. Prefer Settings over Task Manager End task loops."
                : "Quita OneDrive, Teams, Outlook, Phone Link, Xbox Game Bar si no los usas. Mejor Configuración que matar procesos en bucle.",
            Uri = "ms-settings:startupapps"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Widgets / taskbar feed" : "Widgets / feed de barra",
            Body = Loc.IsEnglish
                ? "Widgets keep network/CPU awake. Turn them off under Taskbar settings."
                : "Los widgets mantienen red/CPU. Apágalos en Personalización > Barra de tareas.",
            Uri = "ms-settings:personalization-taskbar"
        },
        new AdviceTip
        {
            Title = Loc.IsEnglish ? "Do not kill" : "No matar",
            Body = Loc.IsEnglish
                ? "WindowsLab never recommends killing Defender (MsMpEng), SearchHost, SysMain, or DiagTrack as a default 'optimization'."
                : "WindowsLab no recomienda matar Defender (MsMpEng), SearchHost, SysMain ni DiagTrack como 'optimización' por defecto.",
            Uri = "ms-settings:windowsdefender"
        }
    ];

    private void AdviceTipOpen_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AdviceTip tip)
        {
            Launch(tip.Uri, tip.Body);
        }
    }

    private void RenderChecklist()
    {
        if (_session is null)
        {
            return;
        }

        var rows = _session.ChecklistResults.Select(r => new ChecklistRow
        {
            Id = r.Id,
            Estado = r.Verdict switch
            {
                ChecklistVerdict.Ok => "OK",
                ChecklistVerdict.Gap => "FALTA",
                ChecklistVerdict.Unknown => "?",
                _ => "INFO"
            },
            Section = r.Section,
            Title = r.Title,
            HowTo = r.HowTo,
            SettingsUri = r.SettingsUri,
            Verdict = r.Verdict,
            Policy = r.Policy
        }).ToList();
        ChecklistGrid.ItemsSource = rows;
        var ok = rows.Count(x => x.Estado == "OK");
        var gap = rows.Count(x => x.Estado == "FALTA");
        var info = rows.Count(x => x.Estado == "INFO");
        var unk = rows.Count(x => x.Estado == "?");
        ChecklistSummary.Text = $"OK {ok}  ·  FALTA {gap}  ·  INFO {info}  ·  ? {unk}  ·  total {rows.Count}";
    }

    private void ChecklistActivate_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChecklistRow row)
        {
            return;
        }

        if (row.Policy == ChecklistPolicy.NeverDisable)
        {
            MessageBox.Show(row.HowTo, row.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Prefer linked HKCU lab tweak when title/id maps to catalog and is eligible.
        if (_session is not null)
        {
            var tweak = MapChecklistToTweak(row.Id, _session.Catalog);
            if (tweak is not null && TweakApplicator.IsLabEligible(tweak))
            {
                var confirm = MessageBox.Show(
                    Loc.IsEnglish
                        ? $"Apply HKCU tweak {tweak.Id}?\n{tweak.Title}"
                        : $"¿Aplicar tweak HKCU {tweak.Id}?\n{tweak.Title}",
                    "WindowsLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    var result = TweakApplicator.Apply(tweak, new LiveRegistryReader(), new LiveRegistryWriter(), dryRun: false);
                    if (result.Backup is not null)
                    {
                        TweakApplicator.PersistBackup(result.Backup);
                    }

                    MessageBox.Show(result.Message, "Apply", MessageBoxButton.OK,
                        result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    Reload(ParseProfile((ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString()));
                    return;
                }
            }
        }

        var uri = row.SettingsUri
                  ?? GuessSettingsUri(row.Id, row.Section);
        if (!string.IsNullOrWhiteSpace(uri))
        {
            Launch(uri, row.HowTo);
            ChecklistSummary.Text = (Loc.IsEnglish ? "Opened: " : "Abierto: ") + uri + " — " + row.Title;
            return;
        }

        MessageBox.Show(row.HowTo, row.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static TweakDefinition? MapChecklistToTweak(string checklistId, IReadOnlyList<TweakDefinition> catalog)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["telemetry.tailored-off"] = "privacy.tailored-experiences-off",
            ["telemetry.advertising-id-off"] = "privacy.advertising-id-off",
            ["telemetry.ink-off"] = "privacy.ink-personalization-off",
            ["telemetry.text-off"] = "privacy.restrict-implicit-text",
            ["telemetry.content-delivery-off"] = "privacy.content-delivery-off"
        };
        if (!map.TryGetValue(checklistId, out var tweakId))
        {
            return null;
        }

        return catalog.FirstOrDefault(t => string.Equals(t.Id, tweakId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GuessSettingsUri(string id, string section)
    {
        if (id.StartsWith("bg.", StringComparison.OrdinalIgnoreCase)
            || section.Contains("segundo plano", StringComparison.OrdinalIgnoreCase)
            || section.Contains("background", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:startupapps";
        }

        if (id.StartsWith("telemetry.", StringComparison.OrdinalIgnoreCase)
            || section.Contains("Telemetr", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:privacy-feedback";
        }

        if (id.StartsWith("account.", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:yourinfo";
        }

        if (section.Contains("Programas", StringComparison.OrdinalIgnoreCase))
        {
            return "ms-settings:appsfeatures";
        }

        return null;
    }

    private void Simulate_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            SimulateText.Text = "Aún cargando.";
            return;
        }

        if (TweakGrid.SelectedItem is not TweakRow row)
        {
            SimulateText.Text = "Selecciona un tweak.";
            return;
        }

        var detection = _session.Detections.FirstOrDefault(d => d.TweakId == row.Id);
        SimulateText.Text = detection is null
            ? "Sin detección."
            : $"Simulación (0 escrituras): {detection.TweakId} actual={detection.ActualDisplay} desired={detection.DesiredDisplay} match={detection.MatchesDesired}";
    }

    private static void AddLine(Panel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private static string Fmt(bool? value) => value is null ? "unknown" : value.Value ? "on" : "off";
}
