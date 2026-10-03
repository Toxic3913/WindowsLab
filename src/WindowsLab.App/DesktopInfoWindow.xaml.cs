using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class DesktopInfoWindow : Window
{
    private static DesktopInfoWindow? _instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public DesktopInfoWindow()
    {
        InitializeComponent();
        PlaceInCorner();
        Opacity = OpacitySlider.Value;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            ApplyLanguage();
            RefreshRecommendation();
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        };
    }

    public static DesktopInfoWindow ShowSingleton(Window? owner = null)
    {
        if (_instance is not null)
        {
            if (_instance.WindowState == WindowState.Minimized)
            {
                _instance.WindowState = WindowState.Normal;
            }

            _instance.Activate();
            _instance.ApplyLanguage();
            _instance.RefreshRecommendation();
            _instance.Refresh();
            return _instance;
        }

        _instance = new DesktopInfoWindow();
        if (owner is not null)
        {
            _instance.Owner = owner;
        }

        _instance.Show();
        return _instance;
    }

    private void ApplyLanguage()
    {
        Title = "WindowsLab DesktopInfo";
        TopMostBox.Content = Loc.IsEnglish ? "Always on top" : "Siempre visible";
        OpacityLabel.Text = Loc.IsEnglish ? "Opacity" : "Opacidad";
        BtnRefresh.Content = Loc.IsEnglish ? "Refresh" : "Actualizar";
        BtnCorner.Content = Loc.IsEnglish ? "Corner" : "Esquina";
        BtnOpenBgInfo.Content = Loc.IsEnglish ? "Use BGInfo…" : "Usar BGInfo…";
        HostLabel.Text = Loc.IsEnglish ? "Machine" : "Equipo";
        DiskLabel.Text = Loc.IsEnglish ? "Disk" : "Disco";
        NetLabel.Text = Loc.IsEnglish ? "Network" : "Red";
        FooterHint.Text = Loc.IsEnglish
            ? "Overlay only — does not change the desktop wallpaper. Safe with Wallpaper Engine."
            : "Solo overlay — no cambia el fondo de escritorio. Compatible con Wallpaper Engine.";
    }

    private void RefreshRecommendation()
    {
        var rec = DesktopInfoChooser.Recommend();
        RecommendBanner.Text = rec.Title + "\n" + rec.Reason;
        var brushKey = rec.Recommended == DesktopOverlayChoice.DesktopInfo ? "OkBrush" : "WarnBrush";
        if (TryFindResource(brushKey) is System.Windows.Media.Brush brush)
        {
            RecommendBanner.Foreground = brush;
        }
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => Refresh();

    private void TopMostBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Topmost = TopMostBox.IsChecked == true;
    }

    private void OpacitySlider_OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Opacity = e.NewValue;
    }

    private void Corner_OnClick(object sender, RoutedEventArgs e) => PlaceInCorner();

    private void PlaceInCorner()
    {
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
    }

    private void OpenBgInfo_OnClick(object sender, RoutedEventArgs e)
    {
        var probe = QuickTools.ProbeBgInfo();
        var pause = probe.WallpaperConflicts.Count > 0;
        if (pause)
        {
            var conflictAsk = MessageBox.Show(
                Loc.T("btn.bginfo.enable.conflict.desktopinfo")
                    .Replace("{0}", string.Join(", ", probe.WallpaperConflicts), StringComparison.Ordinal)
                + "\n\n" + probe.Details,
                "BGInfo / DesktopInfo",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (conflictAsk is MessageBoxResult.Cancel or MessageBoxResult.Yes)
            {
                // Yes = keep DesktopInfo (already open); Cancel = do nothing
                return;
            }
        }
        else
        {
            var ask = MessageBox.Show(
                Loc.T("btn.bginfo.enable.confirm") + "\n\n" + probe.Details,
                "BGInfo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (ask != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var report = BgInfoService.Enable(allowInstall: probe.NeedsInstall, pauseConflicts: pause);
        MessageBox.Show(
            report.Details,
            "BGInfo",
            MessageBoxButton.OK,
            report.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void Refresh()
    {
        try
        {
            var s = LiveSystemReader.Read();
            StampText.Text = s.CapturedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            HostValue.Text = s.ComputerName;
            UserValue.Text = (Loc.IsEnglish ? "User: " : "Usuario: ") + s.UserName;
            OsValue.Text = s.OsLine;
            CpuValue.Text = $"{s.CpuPercent:0.0}%";
            CpuDetail.Text = s.CpuLine;
            RamValue.Text = $"{s.RamPercent:0.0}%";
            RamDetail.Text = s.RamLine;
            DiskValue.Text = s.DiskLine;
            NetValue.Text = s.NetworkLine;
        }
        catch (Exception ex)
        {
            HostValue.Text = "Error";
            OsValue.Text = ex.Message;
        }
    }
}
