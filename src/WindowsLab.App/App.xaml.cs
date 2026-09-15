using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WindowsLab.Core;

namespace WindowsLab.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        StartupLog.Write("OnStartup.begin",
            $"args={e.Args.Length} base={AppContext.BaseDirectory} user={Environment.UserName}");

        DispatcherUnhandledException += (_, args) =>
        {
            StartupLog.Write("DispatcherUnhandledException", args.Exception.Message);
            LogCrash("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                "WindowsLab falló al arrancar.\n\n" + Truncate(args.Exception.ToString(), 1200) +
                "\n\nstartup.log: " + StartupLog.Path +
                "\ncrash.log: " + CrashLogPath(),
                "WindowsLab — error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                StartupLog.Write("UnhandledException", ex.Message);
                LogCrash("UnhandledException", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write("UnobservedTaskException", args.Exception.Message);
            LogCrash("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        try
        {
            base.OnStartup(e);
            StartupLog.Write("OnStartup.base.ok");
        }
        catch (Exception ex)
        {
            StartupLog.Write("OnStartup.base.fail", ex.Message);
            LogCrash("OnStartup", ex);
            MessageBox.Show(ex.ToString(), "WindowsLab — error de arranque");
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupLog.Write("OnExit", "code=" + e.ApplicationExitCode);
        base.OnExit(e);
    }

    internal static string CrashLogPath()
    {
        ConfigChannels.EnsureRuntimeFolders();
        return Path.Combine(ConfigChannels.UserRoot, "crash.log");
    }

    internal static void LogCrash(string kind, Exception ex)
    {
        try
        {
            var path = CrashLogPath();
            var sb = new StringBuilder();
            sb.AppendLine("==== " + DateTimeOffset.Now.ToString("O") + " " + kind + " ====");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // last resort: ignore
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
