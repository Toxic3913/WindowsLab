using System.Diagnostics;
using System.IO;
using System.Net.Http;
using WindowsLab.Applications;
using WindowsLab.Core;

namespace WindowsLab.App;

public static class QuickTools
{
    public static string DownloadsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static string ToolsDir
    {
        get
        {
            var nextToApp = Path.Combine(AppContext.BaseDirectory, "tools");
            if (Directory.Exists(nextToApp))
            {
                return nextToApp;
            }

            var repo = Path.Combine(FindRepoRoot() ?? "", "tools");
            if (Directory.Exists(repo))
            {
                return repo;
            }

            var user = Path.Combine(ConfigChannels.UserRoot, "tools");
            Directory.CreateDirectory(user);
            return user;
        }
    }

    public static string? FindActivateWin()
    {
        string[] names = ["activate.win", "activate.win.cmd", "activate.cmd", "Activate.win"];
        foreach (var dir in CandidateToolDirs())
        {
            foreach (var name in names)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    public static string? FindBgInfo()
    {
        string[] names = ["Bginfo64.exe", "Bginfo.exe", "BGInfo64.exe", "BGInfo.exe"];
        foreach (var dir in CandidateToolDirs())
        {
            foreach (var name in names)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        var programFiles = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BGInfo"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages")
        };
        foreach (var root in programFiles)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                var hit = Directory.EnumerateFiles(root, "Bginfo*.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null)
                {
                    return hit;
                }
            }
            catch
            {
                // ignore ACL noise
            }
        }

        return null;
    }

    public static (bool Ok, string Message) LaunchActivateWindows()
    {
        var script = FindActivateWin();
        if (script is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = script,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(script)
                });
                return (true, "Lanzado: " + script);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:activation",
                UseShellExecute = true
            });
            return (true, "Sin activate.win local → abriendo Configuración > Activación. Coloca tu script en tools\\activate.win para usarlo.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Ok, string Message) LaunchOrInstallBgInfo()
    {
        var existing = FindBgInfo();
        if (existing is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = existing,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(existing)
                });
                return (true, "BGInfo abierto: " + existing);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        if (!WingetClient.IsAvailable())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://learn.microsoft.com/sysinternals/downloads/bginfo",
                    UseShellExecute = true
                });
                return (true, "winget no disponible → página oficial de BGInfo abierta.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        var app = new ApplicationDefinition(
            "app.tool.bginfo",
            "BGInfo",
            "Sysinternals desktop info",
            "Microsoft.Sysinternals.BGInfo",
            "tool",
            EvidenceGrade.Official,
            ["https://learn.microsoft.com/sysinternals/downloads/bginfo"],
            RiskLevel.Low,
            new AppAxes(0.7, 0.8, 0.8, 0.5, 0.6),
            ["balanced", "developer"],
            AppInstallScope.User,
            ["BGInfo", "Bginfo"]);

        var result = WingetClient.Install(app, allowElevate: true);
        if (result.Outcome is not (WingetOutcome.Ok or WingetOutcome.AlreadyInstalled))
        {
            return (false, $"BGInfo install: {result.Outcome} — {result.Message}");
        }

        existing = FindBgInfo();
        if (existing is null)
        {
            return (true, "BGInfo instalado. Ábrelo desde el menú Inicio (Bginfo64) o vuelve a pulsar el botón.");
        }

        Process.Start(new ProcessStartInfo { FileName = existing, UseShellExecute = true });
        return (true, "BGInfo instalado y abierto: " + existing);
    }

    public static async Task<(bool Ok, string Message)> DownloadLibreOfficeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(DownloadsDir);
        var destDir = Path.Combine(DownloadsDir, "WindowsLab");
        Directory.CreateDirectory(destDir);

        if (WingetClient.IsAvailable())
        {
            var winget = ResolveWingetForDownload();
            if (winget is not null)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winget,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("download");
                psi.ArgumentList.Add("--id");
                psi.ArgumentList.Add("TheDocumentFoundation.LibreOffice");
                psi.ArgumentList.Add("--exact");
                psi.ArgumentList.Add("--disable-interactivity");
                psi.ArgumentList.Add("--download-directory");
                psi.ArgumentList.Add(destDir);
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("No se pudo iniciar winget download.");
                var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                if (proc.ExitCode == 0)
                {
                    Process.Start(new ProcessStartInfo { FileName = destDir, UseShellExecute = true });
                    return (true, "LibreOffice descargado en " + destDir);
                }

                // fall through to HTTP if winget download unsupported
                _ = stdout;
                _ = stderr;
            }
        }

        // Official mirror (Windows x86_64 installer). Version pinned for stability; update catalog as needed.
        const string url =
            "https://download.documentfoundation.org/libreoffice/stable/25.2.5/win/x86_64/LibreOffice_25.2.5_Win_x86-64.msi";
        var file = Path.Combine(destDir, "LibreOffice_Win_x86-64.msi");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            await using var net = await http.GetStreamAsync(url, ct).ConfigureAwait(false);
            await using var fs = File.Create(file);
            await net.CopyToAsync(fs, ct).ConfigureAwait(false);
            Process.Start(new ProcessStartInfo { FileName = destDir, UseShellExecute = true });
            return (true, "Instalador LibreOffice en " + file);
        }
        catch (Exception ex)
        {
            return (false, "Descarga fallida: " + ex.Message + " — usa Aplicaciones o winget install TheDocumentFoundation.LibreOffice");
        }
    }

    private static string? ResolveWingetForDownload()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(local) ? local : "winget";
    }

    private static IEnumerable<string> CandidateToolDirs()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "tools");
        yield return Path.Combine(ConfigChannels.UserRoot, "tools");
        var repo = FindRepoRoot();
        if (repo is not null)
        {
            yield return Path.Combine(repo, "tools");
        }
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WindowsLab.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
