using System.Windows;
using System.Windows.Controls;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class MainWindow
{
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

        // 1) Select gaming montage + profile (same as Inicio → Gaming)
        SelectGamingMontageQuiet();

        // 2) Stop distractor workloads (not Steam/Epic/Discord/Riot)
        EnsureWorkloadsLoaded();
        var (proc, svc, titles) = GameModePrep.StopDistractors(_workloads);
        var stopLine = titles.Count == 0
            ? Loc.T("game.mode.noDistractors")
            : Loc.T("game.mode.stopped")
                .Replace("{0}", string.Join(", ", titles), StringComparison.Ordinal)
                .Replace("{1}", proc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{2}", svc.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        // 3) Apply curated HKCU gaming tweaks (Game Mode, less DVR, …)
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
