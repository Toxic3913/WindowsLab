using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using WindowsLab.Setup;

namespace WindowsLab.Uninstall;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var silent = args.Any(a =>
            string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase));

        // Second-stage: delete install folder after the in-place Uninstall.exe has exited.
        var finishIdx = Array.FindIndex(args, a => string.Equals(a, "--finish-delete", StringComparison.OrdinalIgnoreCase));
        if (finishIdx >= 0 && finishIdx + 1 < args.Length)
        {
            return FinishDelete(args[finishIdx + 1], silent);
        }

        var es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        var installDir = InstallRegistration.TryReadInstallLocation()
                         ?? Path.GetDirectoryName(Environment.ProcessPath)
                         ?? AppContext.BaseDirectory;

        installDir = Path.GetFullPath(installDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (!silent)
        {
            var msg = es
                ? $"¿Desinstalar WindowsLab de?\n\n{installDir}\n\nNo se borran %LocalAppData%\\WindowsLab ni %ProgramData%\\WindowsLab (backups/ajustes)."
                : $"Uninstall WindowsLab from?\n\n{installDir}\n\n%LocalAppData%\\WindowsLab and %ProgramData%\\WindowsLab are kept (backups/settings).";
            var r = MessageBox.Show(msg, es ? "Desinstalar WindowsLab" : "Uninstall WindowsLab",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
            {
                return 1;
            }
        }

        try
        {
            var sentinel = Path.Combine(installDir, "WindowsLab.exe");
            if (!File.Exists(sentinel) && !File.Exists(Path.Combine(installDir, "WindowsLab-Uninstall.exe")))
            {
                throw new InvalidOperationException(
                    es
                        ? $"No parece una instalación de WindowsLab:\n{installDir}"
                        : $"Does not look like a WindowsLab install:\n{installDir}");
            }

            InstallRegistration.StopProductProcesses();
            Thread.Sleep(500);
            // Shortcuts + ARP stay until FinishDelete so a cancelled UAC does not orphan the install.

            // Relocate self to TEMP so we can delete the install directory including this EXE.
            var self = Environment.ProcessPath ?? Application.ExecutablePath;
            var tempExe = Path.Combine(
                Path.GetTempPath(),
                "WindowsLab-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(self, tempExe, overwrite: true);

            // UseShellExecute + Verb=runas required for UAC; ArgumentList is unsupported then.
            var psi = new ProcessStartInfo
            {
                FileName = tempExe,
                Arguments = $"--finish-delete \"{installDir}\"" + (silent ? " --silent" : ""),
                UseShellExecute = true,
                Verb = "runas"
            };
            var proc = Process.Start(psi);
            if (proc is null)
            {
                throw new InvalidOperationException(
                    es
                        ? "No se pudo iniciar la segunda etapa del desinstalador (¿UAC cancelado?)."
                        : "Could not start the uninstall finish stage (UAC cancelled?).");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(
                    (es ? "La desinstalación falló:\n\n" : "Uninstall failed:\n\n") + ex.Message,
                    "WindowsLab",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 2;
        }
    }

    private static int FinishDelete(string installDir, bool silent)
    {
        try
        {
            Thread.Sleep(1200);
            InstallRegistration.StopProductProcesses();
            Thread.Sleep(400);

            var sentinel = Path.Combine(installDir, "WindowsLab.exe");
            var uninstallHere = Path.Combine(installDir, "WindowsLab-Uninstall.exe");
            if (!Directory.Exists(installDir)
                || (!File.Exists(sentinel) && !File.Exists(uninstallHere)))
            {
                throw new InvalidOperationException(
                    $"Refusing to delete '{installDir}' — WindowsLab sentinel not found.");
            }

            TryDeleteDirectory(installDir);

            InstallRegistration.Unregister();
            InstallRegistration.RemoveShortcuts();

            if (!silent)
            {
                var es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.StartsWith("es", StringComparison.OrdinalIgnoreCase);
                MessageBox.Show(
                    es ? "WindowsLab se ha desinstalado." : "WindowsLab has been uninstalled.",
                    "WindowsLab",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            // Schedule self-delete of the temp uninstaller
            var self = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(self) && self.Contains("WindowsLab-Uninstall-", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C timeout /T 2 /NOBREAK >NUL & del /F /Q \"{self}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // ignore
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(ex.Message, "WindowsLab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 3;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch
            {
                Thread.Sleep(700);
            }
        }

        // Last resort: mark files for delete on reboot
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
                try
                {
                    NativeMethods.MoveFileEx(file, null, NativeMethods.MoveFileDelayUntilReboot);
                }
                catch
                {
                    // ignore
                }
            }
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            NativeMethods.MoveFileEx(path, null, NativeMethods.MoveFileDelayUntilReboot);
        }
    }
}

internal static class NativeMethods
{
    public const int MoveFileDelayUntilReboot = 0x4;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);
}
