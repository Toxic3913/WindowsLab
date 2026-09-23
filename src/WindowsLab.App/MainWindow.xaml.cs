using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsLab.Applications;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public sealed class PerfProcessRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string GroupLabel { get; init; } = "";
    public ProcessGroup Group { get; init; }
    public string CpuText { get; init; } = "";
    public string WorkingSetText { get; init; } = "";
    public string PrivateText { get; init; } = "";
    public string Path { get; init; } = "";
}

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
    private readonly DispatcherTimer _perfTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private IReadOnlyList<PerfProcessRow> _perfRows = [];
    private bool _perfBusy;

    public MainWindow()
    {
        InitializeComponent();
        ConfigChannels.EnsureRuntimeFolders();
        StartupLog.Write("MainWindow.ctor.begin");
        _perfTimer.Tick += (_, _) => RefreshPerformanceDashboard();
        var saved = OperatorSettingsStore.Load();
        Loc.Language = saved.Language;
        _suppressProfile = true;
        _suppressTheme = true;
        SelectLangBox(saved.Language);
        SelectProfileBox(saved.Profile);
        SelectThemeBox(saved.Theme);
        ThemeService.Apply(OperatorSettingsStore.ParseTheme(saved.Theme));
        ChkAllowSystemApply.IsChecked = saved.AllowSystemApply;
        _suppressProfile = false;
        _suppressTheme = false;
        ApplyUiLanguage();
        HomeSummary.Text = Loc.T("home.loading");
        HomeWarn.Text = Loc.T("home.loadingHint");
        Loaded += (_, _) =>
        {
            StartupLog.Write("MainWindow.Loaded");
            RefreshBackupList();
            BeginLoad();
        };
        Closed += (_, _) => _perfTimer.Stop();
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
        if (Nav.SelectedItem is ListBoxItem navItem &&
            string.Equals(navItem.Tag as string, "resources", StringComparison.OrdinalIgnoreCase))
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
        NavPrepHeader.Content = Loc.T("nav.prep");
        NavAdvHeader.Content = Loc.T("nav.adv");
        NavMaintainHeader.Content = Loc.T("nav.maintain");
        NavHome.Content = Loc.T("nav.home");
        NavConfig.Content = Loc.T("nav.config");
        NavResources.Content = Loc.T("nav.resources");
        NavPerformance.Content = Loc.T("nav.performance");
        NavSystem.Content = Loc.T("nav.system");
        NavSecurity.Content = Loc.T("nav.security");
        NavTweaks.Content = Loc.T("nav.tweaks");
        NavAdvice.Content = Loc.T("nav.advice");
        NavChecklist.Content = Loc.T("nav.checklist");
        NavApps.Content = Loc.T("nav.apps");
        NavSettings.Content = Loc.T("nav.settings");
        MenuFile.Header = Loc.T("menu.file");
        MenuView.Header = Loc.T("menu.view");
        MenuTools.Header = Loc.T("menu.tools");
        MenuHelp.Header = Loc.T("menu.help");
        MenuExit.Header = Loc.T("menu.exit");
        MenuBackups.Header = Loc.T("menu.backups");
        MenuUpdates.Header = Loc.T("menu.updates");
        MenuReleases.Header = Loc.T("menu.releases");
        MenuAbout.Header = Loc.T("menu.about");
        ThemeLabel.Text = Loc.T("theme.label");
        UpdateThemeBoxLabels();
        HomeModesTitle.Text = Loc.T("home.modes");
        HomeModesHint.Text = Loc.T("home.modesHint");
        BtnModeGamingTitle.Text = Loc.T("mode.gaming");
        BtnModeGamingSub.Text = Loc.T("mode.gaming.sub");
        BtnModeOptimizedTitle.Text = Loc.T("mode.optimized");
        BtnModeOptimizedSub.Text = Loc.T("mode.optimized.sub");
        BtnModeDevTitle.Text = Loc.T("mode.dev");
        BtnModeDevSub.Text = Loc.T("mode.dev.sub");
        BtnModeBalancedTitle.Text = Loc.T("mode.balanced");
        BtnModeBalancedSub.Text = Loc.T("mode.balanced.sub");
        BtnModeEmpresaTitle.Text = Loc.T("mode.empresa");
        BtnModeEmpresaSub.Text = Loc.T("mode.empresa.sub");
        BtnModePruebasTitle.Text = Loc.T("mode.pruebas");
        BtnModePruebasSub.Text = Loc.T("mode.pruebas.sub");
        ConfigHint.Text = Loc.T("config.hint");
        SettingsSystemApplyTitle.Text = Loc.T("settings.systemApply");
        SettingsSystemApplyHint.Text = Loc.T("settings.systemApplyHint");
        HomeToolsTitle.Text = Loc.T("home.tools");
        HomePacksTitle.Text = Loc.T("home.morePacks");
        HomeProbesTitle.Text = Loc.T("home.details");
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
        BtnOpenPerformance.Content = Loc.T("btn.openPerformance");
        PerfTitle.Text = Loc.T("perf.title");
        PerfHint.Text = Loc.T("perf.hint");
        PerfCpuLabel.Text = Loc.T("perf.cpu");
        PerfRamLabel.Text = Loc.T("perf.ram");
        PerfDiskLabel.Text = Loc.T("perf.disk");
        PerfFilterLabel.Text = Loc.T("perf.filter");
        PerfSortHint.Text = Loc.T("perf.sortHint");
        PerfFooter.Text = Loc.T("perf.footer");
        UpdatePerfFilterLabels();
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
        BtnWindowsSecurity.Content = Loc.T("btn.windowsSecurity");
        BtnWindowsSecurity.ToolTip = Loc.T("btn.windowsSecurity.tip");
        BtnWindowsSecurityRes.Content = Loc.T("btn.windowsSecurity");
        BtnWindowsSecurityRes.ToolTip = Loc.T("btn.windowsSecurity.tip");
        BtnDefenderRealtimeOff.Content = Loc.T("btn.defenderRealtimeOff");
        BtnDefenderRealtimeOff.ToolTip = Loc.T("btn.defenderRealtimeOff.tip");
        BtnDefenderRealtimeOffRes.Content = Loc.T("btn.defenderRealtimeOff");
        BtnDefenderRealtimeOffRes.ToolTip = Loc.T("btn.defenderRealtimeOff.tip");
        AdviceHint.Text = Loc.T("advice.hint");
        AdviceBgTitle.Text = Loc.T("advice.bg.title");
        SettingsThemeTitle.Text = Loc.T("theme.label");
        SettingsChannelsTitle.Text = Loc.T("settings.channels");
        SettingsChannelsHint.Text = Loc.T("settings.channelsHint");
        BtnOpenUser.Content = Loc.T("btn.openUser");
        BtnOpenMachine.Content = Loc.T("btn.openMachine");
        SettingsUpdateTitle.Text = Loc.T("update.title");
        BtnCheckUpdates.Content = Loc.T("update.check");
        BtnCheckUpdatesSettings.Content = Loc.T("update.check");
        BtnOpenReleases.Content = Loc.T("update.open");
        SettingsVersionText.Text = Loc.IsEnglish
            ? "Installed version: " + AppUpdateChecker.GetCurrentVersion()
            : "Versión instalada: " + AppUpdateChecker.GetCurrentVersion();
        SettingsInstallPath.Text = Loc.IsEnglish
            ? "Setup: D:\\WindowsLab\\artifacts\\installer\\WindowsLab-Setup.exe  ·  Inno: WindowsLab-Setup-Inno.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip"
            : "Instalador: D:\\WindowsLab\\artifacts\\installer\\WindowsLab-Setup.exe  ·  Inno: WindowsLab-Setup-Inno.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip";
        if (_session is not null)
        {
            RenderHome();
        }
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

    private void OpenReleases_OnClick(object sender, RoutedEventArgs e) =>
        Launch(AppUpdateChecker.ReleasesPageUrl, Loc.IsEnglish
            ? "Could not open the browser."
            : "No se pudo abrir el navegador.");

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = Loc.T("update.checking");
        HomeQuickStatus.Text = Loc.T("update.checking");
        BtnCheckUpdates.IsEnabled = false;
        BtnCheckUpdatesSettings.IsEnabled = false;
        try
        {
            var result = await AppUpdateChecker.CheckAsync().ConfigureAwait(true);
            UpdateStatusText.Text = result.Message;
            HomeQuickStatus.Text = result.Message;

            if (result.Status is UpdateCheckStatus.UpdateAvailable or UpdateCheckStatus.NoReleasePublished)
            {
                var open = MessageBox.Show(
                    result.Message + "\n\n" +
                    (Loc.IsEnglish ? "Open GitHub releases page?" : "¿Abrir la página de releases en GitHub?"),
                    Loc.T("update.title"),
                    MessageBoxButton.YesNo,
                    result.Status == UpdateCheckStatus.UpdateAvailable
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Question);
                if (open == MessageBoxResult.Yes)
                {
                    Launch(result.ReleaseUrl ?? AppUpdateChecker.ReleasesPageUrl,
                        Loc.IsEnglish ? "Could not open the browser." : "No se pudo abrir el navegador.");
                }
            }
            else if (result.Status == UpdateCheckStatus.UpToDate)
            {
                MessageBox.Show(result.Message, Loc.T("update.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var open = MessageBox.Show(
                    result.Message + "\n\n" +
                    (Loc.IsEnglish ? "Open releases page anyway?" : "¿Abrir releases de todos modos?"),
                    Loc.T("update.title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (open == MessageBoxResult.Yes)
                {
                    Launch(AppUpdateChecker.ReleasesPageUrl,
                        Loc.IsEnglish ? "Could not open the browser." : "No se pudo abrir el navegador.");
                }
            }
        }
        catch (Exception ex)
        {
            var msg = Loc.IsEnglish ? "Update check failed: " + ex.Message : "Fallo al buscar updates: " + ex.Message;
            UpdateStatusText.Text = msg;
            HomeQuickStatus.Text = msg;
        }
        finally
        {
            BtnCheckUpdates.IsEnabled = true;
            BtnCheckUpdatesSettings.IsEnabled = true;
        }
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
        if (PageHome is null || PageConfig is null || PageResources is null || PagePerformance is null)
        {
            return;
        }

        if (Nav.SelectedItem is not ListBoxItem item || item.Tag is not string tag || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        ShowPage(tag);
        if (tag == "resources")
        {
            RefreshLivePreview();
        }

        if (tag == "performance")
        {
            RefreshPerformanceDashboard();
            _perfTimer.Start();
        }
        else
        {
            _perfTimer.Stop();
        }
    }

    private void ShowPage(string tag)
    {
        PageHome.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        PageConfig.Visibility = tag == "config" ? Visibility.Visible : Visibility.Collapsed;
        PageResources.Visibility = tag == "resources" ? Visibility.Visible : Visibility.Collapsed;
        PagePerformance.Visibility = tag == "performance" ? Visibility.Visible : Visibility.Collapsed;
        PageSystem.Visibility = tag == "system" ? Visibility.Visible : Visibility.Collapsed;
        PageSecurity.Visibility = tag == "security" ? Visibility.Visible : Visibility.Collapsed;
        PageTweaks.Visibility = tag == "tweaks" ? Visibility.Visible : Visibility.Collapsed;
        PageAdvice.Visibility = tag == "advice" ? Visibility.Visible : Visibility.Collapsed;
        PageChecklist.Visibility = tag == "checklist" ? Visibility.Visible : Visibility.Collapsed;
        PageApps.Visibility = tag == "apps" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectNav(string tag)
    {
        foreach (var obj in Nav.Items)
        {
            if (obj is ListBoxItem item && string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                Nav.SelectedItem = item;
                return;
            }
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
            $"{inv.Os.FamilyLabel} {inv.Os.DisplayVersion}  ·  {inv.Cpu.Name}  ·  {inv.Ram.TotalBytes / (1024d * 1024 * 1024):0.0} GB";

        var c = _session.SystemVolume;
        if (c is not null)
        {
            var freeGb = c.FreeBytes / (1024d * 1024 * 1024);
            HomeWarn.Text = freeGb < 20
                ? $"C: {freeGb:0.0} GB libres — espacio bajo."
                : "";
        }
        else
        {
            HomeWarn.Text = "";
        }

        UpdateModeButton(BtnModeGamingStat, "gaming");
        UpdateModeButton(BtnModeOptimizedStat, "perf.max");
        UpdateModeButton(BtnModeDevStat, "developer");
        UpdateModeButton(BtnModeBalancedStat, "privacy.lab");
        UpdateModeButton(BtnModeEmpresaStat, "stack.empresa");
        UpdateModeButton(BtnModePruebasStat, "stack.pruebas");

        HomePresetList.ItemsSource = _session.PresetEvals
            .Where(p => !p.Preset.IsCustom)
            .Select(e => new PresetPick { Eval = e })
            .ToList();
        ProbeList.ItemsSource = inv.Probes.Select(p => $"{p.Status,-12} {p.ProbeId}  {p.Message}").ToArray();
    }

    private void UpdateModeButton(TextBlock stat, string presetId)
    {
        if (_session is null)
        {
            stat.Text = "";
            return;
        }

        var eval = _session.PresetEvals.FirstOrDefault(p =>
            string.Equals(p.Preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (eval is null || eval.Total == 0)
        {
            stat.Text = Loc.IsEnglish ? "Pack missing" : "Pack no encontrado";
            return;
        }

        stat.Text = Loc.IsEnglish
            ? $"{eval.ReadyCount}/{eval.Total} ready · {eval.GapCount} gaps"
            : $"{eval.ReadyCount}/{eval.Total} listos · {eval.GapCount} pendientes";
    }

    private void QuickMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || sender is not Button btn || btn.Tag is not string mode)
        {
            return;
        }

        var (profile, presetId, labelKey) = mode.ToLowerInvariant() switch
        {
            "gaming" => (UserProfile.Gaming, "gaming", "mode.gaming"),
            "optimized" => (UserProfile.Balanced, "perf.max", "mode.optimized"),
            "developer" => (UserProfile.Developer, "developer", "mode.dev"),
            "balanced" => (UserProfile.Balanced, "privacy.lab", "mode.balanced"),
            "empresa" => (UserProfile.Balanced, "stack.empresa", "mode.empresa"),
            "pruebas" => (UserProfile.Developer, "stack.pruebas", "mode.pruebas"),
            _ => (UserProfile.Balanced, "privacy.lab", "mode.balanced")
        };

        if (mode is not ("gaming" or "optimized" or "developer" or "balanced" or "empresa" or "pruebas"))
        {
            HomeModeStatus.Text = "Unknown mode: " + mode;
            return;
        }

        var label = Loc.T(labelKey);

        _suppressProfile = true;
        SelectProfileBox(profile switch
        {
            UserProfile.Gaming => "gaming",
            UserProfile.Developer => "developer",
            UserProfile.Virtualization => "virtualization",
            UserProfile.Balanced => "balanced",
            _ => "balanced"
        });
        _suppressProfile = false;

        HomeModeStatus.Text = Loc.IsEnglish
            ? $"Mode: {label} → pack {presetId}"
            : $"Modo: {label} → pack {presetId}";

        SelectNav("config");
        Reload(profile, presetId);
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

        SelectNav("config");
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
        var profile = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                      ?? _session?.Profile.ToString().ToLowerInvariant()
                      ?? "balanced";
        var preset = (PresetList.SelectedItem as PresetPick)?.Eval.Preset.Id
                     ?? OperatorSettingsStore.Load().LastPresetId;
        var lang = Loc.Language;
        var theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "dark";
        var allowSystem = ChkAllowSystemApply.IsChecked == true;
        OperatorSettingsStore.Save(new OperatorSettings(profile, preset, DateTimeOffset.UtcNow, lang, theme, allowSystem));
    }

    private void AllowSystemApply_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Persist();
    }

    private void RefreshBackups_OnClick(object sender, RoutedEventArgs e) => RefreshBackupList();

    private void RefreshBackupList()
    {
        try
        {
            var store = new FileBackupStore();
            BackupList.ItemsSource = store.List()
                .Select(b => $"{b.BackupId}  ·  {b.CreatedUtc:u}  ·  {b.HighestRisk}  ·  {b.Reason}")
                .ToList();
            BackupStatus.Text = "";
        }
        catch (Exception ex)
        {
            BackupStatus.Text = Loc.IsEnglish ? "Could not list backups: " + ex.Message : "No se pudo listar backups: " + ex.Message;
        }
    }

    private async void RestoreBackup_OnClick(object sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is not string line)
        {
            BackupStatus.Text = Loc.IsEnglish ? "Select a backup first." : "Selecciona un backup primero.";
            return;
        }

        var backupId = line.Split('·', 2, StringSplitOptions.TrimEntries)[0].Trim();
        if (string.IsNullOrWhiteSpace(backupId))
        {
            BackupStatus.Text = Loc.IsEnglish ? "Invalid backup id." : "Id de backup inválido.";
            return;
        }

        var settings = OperatorSettingsStore.Load();
        BackupStatus.Text = Loc.IsEnglish ? "Restoring…" : "Restaurando…";
        try
        {
            var result = await SystemApplyEngine.RollbackAsync(
                backupId,
                new FileBackupStore(),
                settings,
                iAmOnLabVm: false).ConfigureAwait(true);
            BackupStatus.Text = result.Message;
            RefreshBackupList();
        }
        catch (Exception ex)
        {
            BackupStatus.Text = Loc.IsEnglish ? "Restore failed: " + ex.Message : "Restauración falló: " + ex.Message;
        }
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

    private async void ApplyPack_OnClick(object sender, RoutedEventArgs e)
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

        await ApplyTweaksById(ids, status => PresetSimulateText.Text = status).ConfigureAwait(true);
    }

    private async void ApplyTweak_OnClick(object sender, RoutedEventArgs e)
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

        await ApplyTweaksById([row.Id], status => SimulateText.Text = status).ConfigureAwait(true);
    }

    private async Task ApplyTweaksById(string[] ids, Action<string> report)
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
            .Where(t => TweakApplicator.IsApplyEligible(t) || TweakApplicator.IsLabEligible(t))
            .ToArray();

        var skipped = ids.Length - tweaks.Length;
        if (tweaks.Length == 0)
        {
            report($"Ningún ítem elegible. Omitidos: {skipped}.");
            return;
        }

        var needsElev = tweaks.Any(TweakApplicator.NeedsElevation);
        var settings = OperatorSettingsStore.Load();
        if (needsElev && !SystemApplyPolicy.IsAllowed(settings, iAmOnLabVmFlag: false))
        {
            report(SystemApplyPolicy.RefuseMessage +
                   " Activa «Permitir apply de sistema» en Ajustes (solo VM de lab).");
            return;
        }

        var list = string.Join("\n", tweaks.Select(t =>
            "• " + t.Id + (TweakApplicator.NeedsElevation(t) ? " [UAC]" : "")));
        var confirm = MessageBox.Show(
            $"Se aplicarán {tweaks.Length} tweak(s).\n" +
            (needsElev
                ? "Algunos requieren Worker elevado (UAC) + backup en %ProgramData%\\WindowsLab\\backups\\\n\n"
                : "Backup en %LocalAppData% / ProgramData según el caso.\n\n") +
            $"{list}\n\n" +
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

        var securityTweaks = tweaks.Where(t => t.AffectsSecurity).ToArray();
        if (securityTweaks.Length > 0)
        {
            var secList = string.Join("\n", securityTweaks.Select(t => "• " + t.Id + " (" + t.Risk + ")"));
            var confirm2 = MessageBox.Show(
                Loc.T("security.confirm2") + "\n\n" + secList,
                "WindowsLab — impacto de seguridad (D021)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);
            if (confirm2 != MessageBoxResult.Yes)
            {
                report(Loc.T("security.cancelled"));
                return;
            }
        }

        var catalogDir = CatalogLocator.FindTweaksDirectory();
        if (catalogDir is null)
        {
            report("Catálogo no encontrado.");
            return;
        }

        report(needsElev
            ? (Loc.IsEnglish ? "Waiting for UAC / Worker…" : "Esperando UAC / Worker…")
            : (Loc.IsEnglish ? "Applying…" : "Aplicando…"));
        try
        {
            var job = await SystemApplyEngine.ApplyAsync(
                tweaks,
                new FileBackupStore(),
                new LiveRegistryReader(),
                settings,
                iAmOnLabVm: false,
                dryRun: false,
                catalogDir).ConfigureAwait(true);

            StartupLog.Write("Apply.system", job.Message);
            RefreshDetectionsAfterApply();
            RefreshBackupList();
            report(job.Message + "\n" + string.Join("\n",
                job.Results.Select(r => $"{r.Outcome}: {r.Message}")));

            if (job.Succeeded && tweaks.Any(t => t.RequiresReboot))
            {
                var reboot = MessageBox.Show(
                    Loc.T("apply.rebootPrompt"),
                    "WindowsLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (reboot == MessageBoxResult.Yes)
                {
                    Launch("ms-settings:recovery", Loc.IsEnglish
                        ? "Could not open Recovery settings."
                        : "No se pudo abrir Recuperación.");
                }
            }
        }
        catch (Exception ex)
        {
            report("Apply falló: " + ex.Message);
            if (securityTweaks.Length > 0)
            {
                var openSec = MessageBox.Show(
                    Loc.IsEnglish
                        ? "Apply failed (Tamper Protection?). Open Windows Security?"
                        : "Apply falló (¿Tamper Protection?). ¿Abrir Seguridad de Windows?",
                    "WindowsLab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (openSec == MessageBoxResult.Yes)
                {
                    Launch("ms-settings:windowsdefender", Loc.T("btn.windowsSecurity.fail"));
                }
            }
        }
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

    private void OpenWindowsSecurity_OnClick(object sender, RoutedEventArgs e)
    {
        HomeQuickStatus.Text = Loc.T("btn.windowsSecurity.status");
        if (ResourcesStatus is not null)
        {
            ResourcesStatus.Text = Loc.T("btn.windowsSecurity.status");
        }

        Launch("ms-settings:windowsdefender", Loc.T("btn.windowsSecurity.fail"));
    }

    private async void DefenderRealtimeOff_OnClick(object sender, RoutedEventArgs e)
    {
        var c1 = MessageBox.Show(
            Loc.T("btn.defenderRealtimeOff.confirm1"),
            "WindowsLab — D021",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (c1 != MessageBoxResult.Yes)
        {
            SetQuickStatus(Loc.T("btn.defenderRealtimeOff.cancelled"));
            return;
        }

        await ApplyTweaksById(
            ["security.defender-realtime-off"],
            status =>
            {
                SetQuickStatus(Loc.T("btn.defenderRealtimeOff.done").Replace("{0}", Truncate(status, 280), StringComparison.Ordinal));
                if (ResourcesStatus is not null)
                {
                    ResourcesStatus.Text = Truncate(status, 280);
                }
            }).ConfigureAwait(true);
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

    private void MenuExit_OnClick(object sender, RoutedEventArgs e) => Close();

    private void MenuBackups_OnClick(object sender, RoutedEventArgs e)
    {
        SelectNav("settings");
        RefreshBackupList();
    }

    private void MenuNav_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag })
        {
            SelectNav(tag);
        }
    }

    private void MenuAbout_OnClick(object sender, RoutedEventArgs e)
    {
        var ver = AppUpdateChecker.GetCurrentVersion();
        MessageBox.Show(
            Loc.IsEnglish
                ? $"WindowsLab {ver}\nConfigure Windows 11 with audit, curated apply, backups, and live Performance.\nSystem apply stays opt-in (D020)."
                : $"WindowsLab {ver}\nConfigura Windows 11 con auditoría, apply curado, backups y Performance en vivo.\nEl apply de sistema sigue con opt-in (D020).",
            Loc.T("menu.about"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenPerformance_OnClick(object sender, RoutedEventArgs e) => SelectNav("performance");

    private void PerfGroupFilter_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PerfProcessGrid is null)
        {
            return;
        }

        ApplyPerfProcessFilter();
    }

    private void UpdatePerfFilterLabels()
    {
        if (PerfGroupFilter is null)
        {
            return;
        }

        foreach (ComboBoxItem item in PerfGroupFilter.Items)
        {
            var tag = item.Tag as string ?? "";
            item.Content = tag switch
            {
                "windows" => Loc.T("perf.group.windows"),
                "microsoft" => Loc.T("perf.group.microsoft"),
                "external" => Loc.T("perf.group.external"),
                _ => Loc.T("perf.group.all")
            };
        }
    }

    private void RefreshPerformanceDashboard()
    {
        if (_perfBusy || PagePerformance is null || PagePerformance.Visibility != Visibility.Visible)
        {
            return;
        }

        _perfBusy = true;
        Task.Run(() => LiveSystemReader.ReadDashboard(includeProcesses: true, processTopN: 25)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                _perfBusy = false;
                if (PagePerformance.Visibility != Visibility.Visible)
                {
                    return;
                }

                if (t.IsFaulted)
                {
                    PerfMeta.Text = Truncate(t.Exception?.GetBaseException().Message ?? "error", 300);
                    return;
                }

                ApplyDashboardToUi(t.Result);
            });
        }, TaskScheduler.Default);
    }

    private void ApplyDashboardToUi(LiveDashboardSnapshot dash)
    {
        PerfCpuValue.Text = $"{dash.CpuPercent:0.0}%";
        var ram = dash.Ram;
        var commit = ram.CommitPercent is null
            ? ""
            : Loc.IsEnglish
                ? $" · commit {ram.CommitPercent:0.0}%"
                : $" · commit {ram.CommitPercent:0.0}%";
        PerfRamValue.Text =
            $"{ram.UsedGb:0.0}/{ram.TotalGb:0.0} GB ({ram.Percent:0.0}%)" +
            $"\n{Loc.T("perf.ram.avail")}: {ram.AvailableGb:0.0} GB{commit}";

        PerfDiskValue.Text = dash.Disks.Count == 0
            ? "—"
            : string.Join("\n", dash.Disks.Select(d =>
                $"{d.Root} {d.FreeGb:0.0}/{d.TotalGb:0.0} GB ({d.FreePercent:0.0}% {Loc.T("perf.disk.free")})"));

        var io = dash.DiskIo.PercentDiskTime is null
            ? ""
            : $" · disk time {dash.DiskIo.PercentDiskTime:0.0}% q={dash.DiskIo.AvgQueueLength:0.00}";
        var totals = dash.Processes is null
            ? ""
            : string.Join(" · ", dash.Processes.GroupTotals.Select(g =>
                $"{GroupLabel(g.Group)}={g.Count} ({FormatBytes(g.WorkingSetBytes)})"));
        PerfMeta.Text = $"{dash.CapturedUtc:HH:mm:ss} · {dash.OsLine} · IP {dash.NetworkLine}{io}\n{totals}";

        _perfRows = (dash.Processes?.TopByWorkingSet ?? [])
            .Select(r => new PerfProcessRow
            {
                Id = r.Id,
                Name = r.Name,
                Group = r.Group,
                GroupLabel = GroupLabel(r.Group),
                CpuText = $"{r.CpuPercent:0.0}",
                WorkingSetText = FormatBytes(r.WorkingSetBytes),
                PrivateText = FormatBytes(r.PrivateBytes),
                Path = r.Path ?? ""
            })
            .ToArray();
        ApplyPerfProcessFilter();
    }

    private void ApplyPerfProcessFilter()
    {
        var tag = (PerfGroupFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        IEnumerable<PerfProcessRow> rows = _perfRows;
        rows = tag switch
        {
            "windows" => rows.Where(r => r.Group == ProcessGroup.Windows),
            "microsoft" => rows.Where(r => r.Group == ProcessGroup.Microsoft),
            "external" => rows.Where(r => r.Group == ProcessGroup.External),
            _ => rows
        };
        PerfProcessGrid.ItemsSource = rows.ToArray();
    }

    private static string GroupLabel(ProcessGroup g) => g switch
    {
        ProcessGroup.Windows => Loc.T("perf.group.windows"),
        ProcessGroup.Microsoft => Loc.T("perf.group.microsoft"),
        ProcessGroup.External => Loc.T("perf.group.external"),
        _ => throw new ArgumentOutOfRangeException(nameof(g), g, null)
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:0.0} KB";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return $"{mb:0.0} MB";
        }

        return $"{mb / 1024d:0.00} GB";
    }

    private void RefreshLivePreview()
    {
        try
        {
            var s = LiveSystemReader.Read();
            LivePreview.Text =
                Loc.T("resources.liveHint") + "\n" +
                $"{s.OsLine}\nCPU {s.CpuLine}\nRAM {s.RamLine}\n{s.DiskLine}\nIP {s.NetworkLine}\n{s.CapturedUtc:HH:mm:ss}";
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
        AddLine(SecurityPanel, Loc.IsEnglish
            ? "WindowsLab never recommends turning off Defender, HVCI, or Secure Boot by default."
            : "WindowsLab no recomienda desactivar Defender, HVCI ni Secure Boot por defecto.");
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
