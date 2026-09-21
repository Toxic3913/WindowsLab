using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsLab.Core;

namespace WindowsLab.Backup;

/// <summary>
/// Attempts a System Restore checkpoint. Failure is non-fatal for LOW/MEDIUM (caller decides).
/// </summary>
public static class RestorePointService
{
    public static BackupRestorePointInfo TryCreate(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        try
        {
            if (!IsSystemRestoreAvailable())
            {
                return new BackupRestorePointInfo(true, false, null, "System Protection / restore service unavailable.");
            }

            var seq = 0;
            var info = new RestorePointInfo
            {
                dwEventType = BeginSystemChange,
                dwRestorePtType = ApplicationInstall,
                llSequenceNumber = 0,
                szDescription = Truncate(description, 256)
            };

            var ok = SRSetRestorePointW(ref info, out seq);
            if (!ok)
            {
                var err = Marshal.GetLastWin32Error();
                return new BackupRestorePointInfo(true, false, null,
                    $"SRSetRestorePoint failed (win32={err}). Often 24h limit or System Protection off.");
            }

            return new BackupRestorePointInfo(true, true, seq, "Restore point created.");
        }
        catch (Exception ex)
        {
            return new BackupRestorePointInfo(true, false, null, ex.Message);
        }
    }

    public static bool RequiresRestorePointSuccess(RiskLevel risk) =>
        risk is RiskLevel.High or RiskLevel.Critical;

    private static bool IsSystemRestoreAvailable()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private const int BeginSystemChange = 100;
    private const int ApplicationInstall = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestorePointInfo
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SRSetRestorePointW(ref RestorePointInfo pRestorePtSpec, out int pSrpStatus);

    /// <summary>Fallback via PowerShell when P/Invoke is unavailable (rare).</summary>
    public static BackupRestorePointInfo TryCreateViaPowerShell(string description)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -Command \"Checkpoint-Computer -Description '{Escape(description)}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return new BackupRestorePointInfo(true, false, null, "Could not start PowerShell.");
            }

            p.WaitForExit(120_000);
            if (p.ExitCode == 0)
            {
                return new BackupRestorePointInfo(true, true, null, "Restore point created (PowerShell).");
            }

            var err = p.StandardError.ReadToEnd();
            return new BackupRestorePointInfo(true, false, null, string.IsNullOrWhiteSpace(err) ? $"Exit {p.ExitCode}" : err.Trim());
        }
        catch (Exception ex)
        {
            return new BackupRestorePointInfo(true, false, null, ex.Message);
        }
    }

    private static string Escape(string s) => s.Replace("'", "''", StringComparison.Ordinal);
}
