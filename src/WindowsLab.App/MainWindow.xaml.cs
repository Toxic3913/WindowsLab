using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsLab.Applications;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

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
}
