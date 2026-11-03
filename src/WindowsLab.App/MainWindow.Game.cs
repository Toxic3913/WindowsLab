using System.Windows;
using System.Windows.Controls;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
    private const string MemReductHubId = "hub.performance.memreduct";
    private const string MemReductAppId = "app.tool.memreduct";

    private void RefreshGamePage()
    {
        if (GameScsStatus is null)
        {
            return;
        }

        var installed = GameModePrep.FindScsKillerExe() is not null;
        GameScsStatus.Text = Loc.T(installed
            ? "game.scs.status.installed"
            : "game.scs.status.missing");
        if (TryFindResource(installed ? "OkBrush" : "AccentBrush") is System.Windows.Media.Brush brush)
        {
            GameScsStatus.Foreground = brush;
        }

        BtnScsKillerAction.Content = Loc.T(installed
            ? "game.scs.open"
            : "game.scs.download");

        if (PageGame?.Visibility == Visibility.Visible)
        {
            RefreshGameSessionLists();
        }
    }

    private async void StartGameMode_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            Loc.T("game.mode.confirm"),
            Loc.T("game.pageTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            GameStatusText.Text = Loc.T("game.cancelled");
            return;
        }

        SelectGamingMontageQuiet();

        EnsureWorkloadsLoaded();
        var (proc, svc, titles) = GameModePrep.StopDistractors(_workloads);
        var stopLine = titles.Count == 0
            ? Loc.T("game.mode.noDistractors")
            : Loc.T("game.mode.stopped")
                .Replace("{0}", string.Join(", ", titles), StringComparison.Ordinal)
                .Replace("{1}", proc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", svc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        GameStatusText.Text = Loc.T("game.mode.applying");
        await ApplyTweaksById(GameModePrep.GameModeTweakIds, status => GameStatusText.Text = status)
            .ConfigureAwait(true);

        var ram = DescribeAvailableRam();
        GameStatusText.Text = stopLine + "\n" + GameStatusText.Text + "\n" + ram;
        RefreshGamePage();
        if (PageWorkloads?.Visibility == Visibility.Visible)
        {
            RenderWorkloads();
        }
    }

    private void SelectGamingMontageQuiet()
    {
        _suppressProfile = true;
        SelectProfileBox("gaming");
        _suppressProfile = false;
        _lastMontageId = "gaming";
        if (_session is null)
        {
            return;
        }

        if (_session.Profile != UserProfile.Gaming)
        {
            Reload(UserProfile.Gaming, "gaming");
        }
        else
        {
            SelectMontage("gaming");
        }
    }

    private void GameFreeRam_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = ToolHubService.LoadEntries()
            .FirstOrDefault(x => string.Equals(x.Id, MemReductHubId, StringComparison.OrdinalIgnoreCase));
        var apps = _session?.Applications ?? Array.Empty<ApplicationDefinition>();
        var facts = InstalledAppDetector.Scan();
        var present = entry is not null && ToolHubService.IsPresent(entry, apps, facts);

        if (present && entry is not null && ToolHubService.TryLaunch(entry))
        {
            GameStatusText.Text = Loc.T("game.ram.opened") + "\n" + DescribeAvailableRam() + "\n" + Loc.T("game.ram.policy");
            return;
        }

        var app = apps.FirstOrDefault(a =>
            string.Equals(a.Id, MemReductAppId, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            GameStatusText.Text = Loc.T("game.ram.missingCatalog");
            MessageBox.Show(Loc.T("game.ram.missingCatalog"), Loc.T("game.pageTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("game.ram.installConfirm")
                .Replace("{0}", app.Title, StringComparison.Ordinal)
                .Replace("{1}", app.WingetId, StringComparison.Ordinal),
            Loc.T("game.pageTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            GameStatusText.Text = Loc.T("game.cancelled");
            return;
        }

        GameStatusText.Text = Loc.T("game.ram.installing");
        BtnGameFreeRam.IsEnabled = false;
        Task.Run(() => WingetClient.Install(app, allowElevate: true)).ContinueWith(t =>
        {
            Dispatcher.Invoke(() =>
            {
                BtnGameFreeRam.IsEnabled = true;
                if (t.IsFaulted)
                {
                    var err = Truncate(t.Exception?.GetBaseException().Message ?? "error", 400);
                    GameStatusText.Text = err;
                    return;
                }

                var result = t.Result;
                GameStatusText.Text = $"{result.Outcome}: {Truncate(result.Message, 240)}";
                if (result.Outcome is WingetOutcome.Ok or WingetOutcome.AlreadyInstalled)
                {
                    entry = ToolHubService.LoadEntries()
                        .FirstOrDefault(x => string.Equals(x.Id, MemReductHubId, StringComparison.OrdinalIgnoreCase));
                    if (entry is not null && ToolHubService.TryLaunch(entry))
                    {
                        GameStatusText.Text = Loc.T("game.ram.opened") + "\n" + DescribeAvailableRam();
                    }
                }
            });
        }, TaskScheduler.Default);
    }

    private void GameStopWorkloads_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            Loc.T("game.ram.confirm"),
            Loc.T("game.pageTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            GameStatusText.Text = Loc.T("game.cancelled");
            return;
        }

        EnsureWorkloadsLoaded();
        var (proc, svc, titles) = GameModePrep.StopDistractors(_workloads);
        var detail = titles.Count == 0
            ? Loc.T("game.mode.noDistractors")
            : Loc.T("game.mode.stopped")
                .Replace("{0}", string.Join(", ", titles), StringComparison.Ordinal)
                .Replace("{1}", proc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", svc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        GameStatusText.Text = detail + "\n" + DescribeAvailableRam() + "\n" + Loc.T("game.ram.policy");
        if (PageWorkloads?.Visibility == Visibility.Visible)
        {
            RenderWorkloads();
        }

        RefreshGameSessionLists();
    }

    private void GameSessionRefresh_OnClick(object sender, RoutedEventArgs e) => RefreshGameSessionLists();

    private void RefreshGameSessionLists()
    {
        if (GameSessionProcGrid is null || GameSessionSvcGrid is null)
        {
            return;
        }

        GameSessionProcGrid.ItemsSource = SessionResourceController.ListProcesses(80)
            .Select(p => new SessionProcessRow
            {
                Id = p.Id,
                Name = p.Name,
                WorkingSetText = FormatBytes(p.WorkingSetBytes),
                GroupLabel = GroupLabel(p.Group),
                Path = p.Path ?? "",
                IsBlocked = p.IsBlocked,
                BlockLabel = p.IsBlocked ? Loc.T("game.session.blocked") : Loc.T("game.session.ok")
            })
            .ToArray();

        GameSessionSvcGrid.ItemsSource = SessionResourceController.ListRunningServices(100)
            .Select(s => new SessionServiceRow
            {
                Name = s.Name,
                DisplayName = s.DisplayName,
                Status = s.Status,
                CanStop = s.CanStop,
                IsBlocked = s.IsBlocked,
                BlockLabel = s.IsBlocked
                    ? Loc.T("game.session.blocked")
                    : (s.CanStop ? Loc.T("game.session.stoppable") : Loc.T("game.session.locked"))
            })
            .ToArray();
    }

    private void GameSessionStop_OnClick(object sender, RoutedEventArgs e)
    {
        if (GameSessionProcGrid is null || GameSessionSvcGrid is null)
        {
            return;
        }

        var procs = (GameSessionProcGrid.ItemsSource as IEnumerable<SessionProcessRow> ?? [])
            .Where(r => r.IsSelected && !r.IsBlocked)
            .Select(r => r.Id)
            .ToArray();
        var svcs = (GameSessionSvcGrid.ItemsSource as IEnumerable<SessionServiceRow> ?? [])
            .Where(r => r.IsSelected && !r.IsBlocked && r.CanStop)
            .Select(r => r.Name)
            .ToArray();

        if (procs.Length == 0 && svcs.Length == 0)
        {
            GameStatusText.Text = Loc.T("game.session.noneSelected");
            return;
        }

        var confirm = MessageBox.Show(
            Loc.T("game.session.confirm")
                .Replace("{0}", procs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", svcs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
            Loc.T("game.pageTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            GameStatusText.Text = Loc.T("game.cancelled");
            return;
        }

        var result = SessionResourceController.StopSelected(procs, svcs);
        GameStatusText.Text = Loc.T("game.session.result")
            .Replace("{0}", result.ProcessesStopped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{1}", result.ServicesStopped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{2}", result.BlockedSkipped.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            + "\n" + DescribeAvailableRam();
        RefreshGameSessionLists();
    }

    private static string DescribeAvailableRam()
    {
        try
        {
            var s = LiveSystemReader.Read();
            return Loc.T("game.ram.snapshot")
                .Replace("{0}", s.RamPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{1}", s.RamLine, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            return Truncate(ex.Message, 120);
        }
    }

    private void OpenWindowsGameMode_OnClick(object sender, RoutedEventArgs e) =>
        Launch("ms-settings:gaming-gamemode", Loc.T("game.settingsFail"));

    private void GameOpenPerf_OnClick(object sender, RoutedEventArgs e) => SelectNav("performance");

    private void GameOpenWorkloads_OnClick(object sender, RoutedEventArgs e) => SelectNav("workloads");

    private void GameOpenTools_OnClick(object sender, RoutedEventArgs e) => SelectNav("tools");

    private void ScsKillerAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (GameModePrep.TryLaunchScsKiller())
        {
            GameStatusText.Text = Loc.T("game.scs.opened");
            RefreshGamePage();
            return;
        }

        Launch(GameModePrep.ScsKillerReleasesUrl, Loc.T("game.scs.openFail"));
        GameStatusText.Text = Loc.T("game.scs.downloadHint");
    }

    private void ScsKillerDocs_OnClick(object sender, RoutedEventArgs e) =>
        Launch(GameModePrep.ScsKillerRepoUrl, Loc.T("game.scs.openFail"));
}
