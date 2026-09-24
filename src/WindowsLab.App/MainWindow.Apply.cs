using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsLab.Applications;
using WindowsLab.Backup;
using WindowsLab.Core;
using WindowsLab.Tweaks;

namespace WindowsLab.App;

public partial class MainWindow
{
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
        var presetId = _lastMontageId ?? OperatorSettingsStore.Load().LastPresetId;
        Reload(profile, presetId);
    }

    private void Launch(string? target, string ifMissing)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            SetQuickStatus(ifMissing);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            SetQuickStatus((Loc.IsEnglish ? "Opened: " : "Abierto: ") + target);
        }
        catch (Exception ex)
        {
            SetQuickStatus((Loc.IsEnglish ? "Could not open " : "No se pudo abrir ") + target + ": " + ex.Message);
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
        SetQuickStatus(Loc.T("btn.windowsSecurity.status"));
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
            status => SetQuickStatus(Loc.T("btn.defenderRealtimeOff.done").Replace("{0}", Truncate(status, 280), StringComparison.Ordinal))).ConfigureAwait(true);
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
        Task.Run(QuickTools.LaunchOrInstallBgInfo).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnBgInfo.IsEnabled = true;
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
        }
    }

    private void SetQuickStatus(string text)
    {
        HomeQuickStatus.Text = text;
    }

    private void MenuExit_OnClick(object sender, RoutedEventArgs e) => Close();

    private void MenuBackups_OnClick(object sender, RoutedEventArgs e)
    {
        SelectNav("more");
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
}
