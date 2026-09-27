using System.Windows;
using System.Windows.Threading;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class DesktopInfoWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public DesktopInfoWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => Refresh();

    private void TopMostBox_OnChanged(object sender, RoutedEventArgs e)
    {
        Topmost = TopMostBox.IsChecked == true;
    }

    private void OpenBgInfo_OnClick(object sender, RoutedEventArgs e)
    {
        var probe = QuickTools.ProbeBgInfo();
        var ask = MessageBox.Show(
            probe.Details + "\n\n" +
            (probe.NeedsInstall
                ? (Loc.IsEnglish ? "Install and open BGInfo?" : "¿Instalar y abrir BGInfo?")
                : (Loc.IsEnglish ? "Open BGInfo?" : "¿Abrir BGInfo?")),
            "BGInfo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes)
        {
            return;
        }

        var report = QuickTools.LaunchOrInstallBgInfo(allowInstall: probe.NeedsInstall);
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
            Body.Text =
                $"""
                WindowsLab DesktopInfo
                {s.CapturedUtc:HH:mm:ss}

                Host     {s.ComputerName}
                Usuario  {s.UserName}
                SO       {s.OsLine}
                CPU      {s.CpuLine}
                RAM      {s.RamLine}
                Disco    {s.DiskLine}
                Red      {s.NetworkLine}

                Estándar Sysinternals: BGInfo (botón abajo).
                Este overlay no pinta el fondo de escritorio.
                """;
        }
        catch (Exception ex)
        {
            Body.Text = "Error: " + ex.Message;
        }
    }
}
