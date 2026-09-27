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
        if (!string.IsNullOrWhiteSpace(selectPresetId))
        {
            _lastMontageId = selectPresetId;
        }
        else if (string.IsNullOrWhiteSpace(_lastMontageId))
        {
            _lastMontageId = OperatorSettingsStore.Load().LastPresetId;
        }

        RenderHome();
        RenderSystem();
        RenderSecurity();
        RenderTweaks();
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
    }

    private void SelectLangBox(string language)
    {
        var want = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "EN" : "ES";
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
        NavAdjust.Content = Loc.T("nav.adjust");
        NavApps.Content = Loc.T("nav.apps");
        NavPerformance.Content = Loc.T("nav.performance");
        NavWorkloads.Content = Loc.T("nav.workloads");
        NavBackups.Content = Loc.T("nav.backups");
        NavTools.Content = Loc.T("nav.tools");
        NavMore.Content = Loc.T("nav.more");
        var headers = Nav.Items.OfType<ListBoxItem>().Where(i => i.Tag is null).ToList();
        if (headers.Count >= 3)
        {
            headers[0].Content = Loc.T("nav.group.primary");
            headers[1].Content = Loc.T("nav.group.ops");
            headers[2].Content = Loc.T("nav.group.meta");
        }

        MenuNavHome.Header = Loc.T("nav.home");
        MenuNavAdjust.Header = Loc.T("nav.adjust");
        MenuNavApps.Header = Loc.T("nav.apps");
        MenuNavPerf.Header = Loc.T("nav.performance");
        MenuNavWorkloads.Header = Loc.T("nav.workloads");
        MenuNavBackups.Header = Loc.T("nav.backups");
        MenuNavTools.Header = Loc.T("nav.tools");
        MenuNavMore.Header = Loc.T("nav.more");
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
        HomePendingTitle.Text = Loc.T("home.pending");
        HomePendingHint.Text = Loc.T("home.pendingHint");
        HomeCapabilitiesTitle.Text = Loc.T("home.capabilities");
        HomeCapabilitiesHint.Text = Loc.T("home.capabilitiesHint");
        CapAuditTitle.Text = Loc.T("home.cap.audit");
        CapAuditBody.Text = Loc.T("home.cap.auditBody");
        CapTweaksTitle.Text = Loc.T("home.cap.tweaks");
        CapTweaksBody.Text = Loc.T("home.cap.tweaksBody");
        CapAppsTitle.Text = Loc.T("home.cap.apps");
        CapAppsBody.Text = Loc.T("home.cap.appsBody");
        CapPerfTitle.Text = Loc.T("home.cap.perf");
        CapPerfBody.Text = Loc.T("home.cap.perfBody");
        CapLoadsTitle.Text = Loc.T("home.cap.loads");
        CapLoadsBody.Text = Loc.T("home.cap.loadsBody");
        CapBackupsTitle.Text = Loc.T("home.cap.backups");
        CapBackupsBody.Text = Loc.T("home.cap.backupsBody");
        BtnModeGamingTitle.Text = Loc.T("mode.gaming");
        BtnModeGamingSub.Text = Loc.T("mode.gaming.sub");
        BtnModeOptimizedTitle.Text = Loc.T("mode.optimized");
        BtnModeOptimizedSub.Text = Loc.T("mode.optimized.sub");
        BtnModeDevTitle.Text = Loc.T("mode.dev");
        BtnModeDevSub.Text = Loc.T("mode.dev.sub");
        BtnModeWorkTitle.Text = Loc.T("mode.work");
        BtnModeWorkSub.Text = Loc.T("mode.work.sub");
        BtnModeBalancedTitle.Text = Loc.T("mode.balanced");
        BtnModeBalancedSub.Text = Loc.T("mode.balanced.sub");
        BtnModeEmpresaTitle.Text = Loc.T("mode.empresa");
        BtnModeEmpresaSub.Text = Loc.T("mode.empresa.sub");
        BtnModePruebasTitle.Text = Loc.T("mode.pruebas");
        BtnModePruebasSub.Text = Loc.T("mode.pruebas.sub");
        AdjustHint.Text = Loc.T("adjust.hint");
        AdjustPresetsLabel.Text = Loc.T("adjust.presets");
        AdjustFilterLabel.Text = Loc.T("adjust.filter");
        AdjustSearchBox.ToolTip = Loc.T("adjust.search");
        BtnAdjustClear.Content = Loc.T("adjust.clear");
        BtnAdjustSimulate.Content = Loc.T("adjust.simulate");
        BtnAdjustApply.Content = Loc.T("adjust.apply");
        BtnAdjustApply.ToolTip = Loc.T("btn.applyDisabled");
        foreach (ComboBoxItem item in AdjustFilterBox.Items)
        {
            item.Content = (item.Tag?.ToString()) switch
            {
                "gaps" => Loc.T("adjust.filter.gaps"),
                "recommended" => Loc.T("adjust.filter.recommended"),
                _ => Loc.T("adjust.filter.all")
            };
        }

        SettingsSystemApplyTitle.Text = Loc.T("settings.systemApply");
        SettingsSystemApplyHint.Text = Loc.T("settings.systemApplyHint");
        ChkAllowSystemApply.Content = Loc.T("settings.allowSystemApply");
        MoreWorkloadsTitle.Text = Loc.T("more.workloads");
        MoreWorkloadsHint.Text = Loc.T("more.workloadsHint");
        BtnWorkloadRefresh.Content = Loc.T("workload.refresh");
        BtnWorkloadStopAll.Content = Loc.T("workload.stopAll");
        SettingsThemeTitle.Text = Loc.T("theme.label");
        SettingsThemeHint.Text = Loc.T("theme.hint");
        SettingsChannelsTitle.Text = Loc.T("settings.channels");
        SettingsChannelsHint.Text = Loc.T("settings.channelsHint");
        MoreBackupsTitle.Text = Loc.T("more.backups");
        BackupsHint.Text = Loc.T("backups.hint");
        BtnCreateBackup.Content = Loc.T("backups.create");
        BtnRefreshBackups.Content = Loc.T("backups.refresh");
        BtnRestoreBackup.Content = Loc.T("backups.restore");
        ToolsPageTitle.Text = Loc.T("tools.pageTitle");
        ToolsPageHint.Text = Loc.T("tools.pageHint");
        MoreSystemTitle.Text = Loc.T("more.system");
        MoreSecurityTitle.Text = Loc.T("more.security");
        MoreToolsTitle.Text = Loc.T("more.tools");
        BtnOpenUser.Content = Loc.T("btn.openUser");
        BtnOpenMachine.Content = Loc.T("btn.openMachine");
        AppsHint.Text = Loc.T("apps.hint");
        AppsFilterLabel.Text = Loc.T("apps.filter");
        AppsSearchBox.ToolTip = Loc.T("apps.search");
        BtnInstallApp.Content = Loc.T("apps.install");
        BtnDefaultApps.Content = Loc.T("apps.defaults");
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
        BtnWindowsSecurity.Content = Loc.T("btn.windowsSecurity");
        BtnWindowsSecurity.ToolTip = Loc.T("btn.windowsSecurity.tip");
        BtnDefenderRealtimeOff.Content = Loc.T("btn.defenderRealtimeOff");
        BtnDefenderRealtimeOff.ToolTip = Loc.T("btn.defenderRealtimeOff.tip");
        SettingsUpdateTitle.Text = Loc.T("update.title");
        BtnCheckUpdatesSettings.Content = Loc.T("update.check");
        BtnOpenReleases.Content = Loc.T("update.open");
        SettingsVersionText.Text = Loc.T("update.version").Replace("{0}", AppUpdateChecker.GetCurrentVersion(), StringComparison.Ordinal);
        SettingsInstallPath.Text = Loc.IsEnglish
            ? "Setup: artifacts\\installer\\WindowsLab-Setup.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip"
            : "Instalador: artifacts\\installer\\WindowsLab-Setup.exe  ·  Zip: artifacts\\zip\\WindowsLab-portable-win-x64.zip";
        if (_session is not null)
        {
            RenderHome();
            BuildAdjustPresetBar();
            RefreshTweakCategoryView();
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
        BtnCheckUpdatesSettings.IsEnabled = false;
        try
        {
            var token = AppUpdateChecker.ResolveToken();
            var result = await AppUpdateChecker.CheckAsync(token).ConfigureAwait(true);
            UpdateStatusText.Text = result.Message;
            HomeQuickStatus.Text = result.Message;

            if (result.Status == UpdateCheckStatus.UpdateAvailable)
            {
                var install = MessageBox.Show(
                    result.Message + "\n\n" +
                    (Loc.IsEnglish
                        ? "Download Setup, install over this folder, and restart WindowsLab?\n(UAC may prompt.)"
                        : "¿Descargar el Setup, instalar encima y reiniciar WindowsLab?\n(Puede pedir UAC.)"),
                    Loc.T("update.title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (install != MessageBoxResult.Yes)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(result.SetupAssetUrl))
                {
                    var open = MessageBox.Show(
                        Loc.IsEnglish
                            ? "No Setup.exe asset on the release. Open the releases page?"
                            : "La release no trae Setup.exe. ¿Abrir la página de releases?",
                        Loc.T("update.title"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (open == MessageBoxResult.Yes)
                    {
                        Launch(result.ReleaseUrl ?? AppUpdateChecker.ReleasesPageUrl,
                            Loc.IsEnglish ? "Could not open the browser." : "No se pudo abrir el navegador.");
                    }

                    return;
                }

                var progress = new Progress<double>(p =>
                {
                    UpdateStatusText.Text = Loc.IsEnglish
                        ? $"Downloading… {(p * 100):0}%"
                        : $"Descargando… {(p * 100):0}%";
                    HomeQuickStatus.Text = UpdateStatusText.Text;
                });

                var setupPath = await AppSelfUpdater.DownloadSetupAsync(result, token, progress).ConfigureAwait(true);
                UpdateStatusText.Text = Loc.IsEnglish
                    ? "Starting Setup — WindowsLab will close and reopen."
                    : "Iniciando Setup — WindowsLab se cerrará y volverá a abrir.";
                AppSelfUpdater.LaunchSetupAndExit(
                    setupPath,
                    AppSelfUpdater.ResolveInstallDirectory(),
                    Environment.ProcessId);
                Application.Current.Shutdown();
                return;
            }

            if (result.Status == UpdateCheckStatus.UpToDate)
            {
                MessageBox.Show(result.Message, Loc.T("update.title"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var openPage = MessageBox.Show(
                result.Message + "\n\n" +
                (Loc.IsEnglish ? "Open GitHub releases page?" : "¿Abrir la página de releases en GitHub?"),
                Loc.T("update.title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (openPage == MessageBoxResult.Yes)
            {
                Launch(result.ReleaseUrl ?? AppUpdateChecker.ReleasesPageUrl,
                    Loc.IsEnglish ? "Could not open the browser." : "No se pudo abrir el navegador.");
            }
        }
        catch (Exception ex)
        {
            var msg = Loc.IsEnglish ? "Update failed: " + ex.Message : "Fallo al actualizar: " + ex.Message;
            UpdateStatusText.Text = msg;
            HomeQuickStatus.Text = msg;
            MessageBox.Show(msg, Loc.T("update.title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
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
