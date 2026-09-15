using System.IO;
using System.Text;
using WindowsLab.Core;

namespace WindowsLab.App;

/// <summary>
/// Append-only startup trail under %LocalAppData%\WindowsLab\startup.log
/// Written before heavy work so a silent crash still leaves evidence.
/// </summary>
internal static class StartupLog
{
    private static readonly object Gate = new();

    public static string Path =>
        System.IO.Path.Combine(ConfigChannels.UserRoot, "startup.log");

    public static void Write(string step, string? detail = null)
    {
        try
        {
            ConfigChannels.EnsureRuntimeFolders();
            var line = new StringBuilder();
            line.Append(DateTimeOffset.Now.ToString("O"));
            line.Append(" [");
            line.Append(Environment.ProcessId);
            line.Append("] ");
            line.Append(step);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                line.Append(" - ");
                line.Append(detail);
            }

            line.AppendLine();
            lock (Gate)
            {
                File.AppendAllText(Path, line.ToString());
            }
        }
        catch
        {
            // never throw from logging
        }
    }
}
