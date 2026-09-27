using System.Diagnostics;

namespace WindowsLab.Core;

/// <summary>
/// Downloads WindowsLab-Setup.exe and relaunches setup in --update mode after this process exits (D029).
/// </summary>
public static class AppSelfUpdater
{
    public static string UpdatesDirectory =>
        Path.Combine(ConfigChannels.UserRoot, "updates");

    public static string ResolveInstallDirectory()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // When running from publish output / Program Files, BaseDirectory is the install root.
        return baseDir;
    }

    public static async Task<string> DownloadSetupAsync(
        UpdateCheckResult check,
        string? githubToken = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(check.SetupAssetUrl))
        {
            throw new InvalidOperationException(
                "La release no incluye WindowsLab-Setup.exe. Abre la página de releases y descarga a mano.");
        }

        Directory.CreateDirectory(UpdatesDirectory);
        var fileName = $"WindowsLab-Setup-{Sanitize(check.LatestTag ?? "latest")}.exe";
        var path = Path.Combine(UpdatesDirectory, fileName);
        await AppUpdateChecker.DownloadAssetAsync(
            check.SetupAssetUrl,
            path,
            githubToken,
            progress,
            cancellationToken).ConfigureAwait(false);
        return path;
    }

    /// <summary>
    /// Starts elevated Setup with --update, then the caller should shut down the UI.
    /// </summary>
    public static void LaunchSetupAndExit(string setupExePath, string installDirectory, int currentProcessId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupExePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        if (!File.Exists(setupExePath))
        {
            throw new FileNotFoundException("Setup no encontrado.", setupExePath);
        }

        var args =
            $"--update --dir \"{installDirectory}\" --wait-pid {currentProcessId} --launch";
        var psi = new ProcessStartInfo
        {
            FileName = setupExePath,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas" // UAC — Setup also has requireAdministrator
        };

        Process.Start(psi);
    }

    private static string Sanitize(string tag)
    {
        var chars = tag.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray();
        return new string(chars);
    }
}
