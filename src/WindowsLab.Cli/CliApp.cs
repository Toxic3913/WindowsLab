using WindowsLab.Core;

namespace WindowsLab.Cli;

public static class CliApp
{
    public const string HelpText =
        """
        WindowsLab — laboratorio de Windows 11 (fase 1)

        Uso:
          windowslab --help
          windowslab audit --os

        audit --os  Identidad del sistema (build + DisplayVersion).
                    No usa ProductName para decidir Windows 11.
                    No requiere administrador. No aplica tweaks.

        Código y docs: D:\WindowsLab
        Programa e artefactos: C: (%ProgramData%\WindowsLab)
        """;

    public static int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        Func<OsIdentity>? readOs = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Count == 0 || IsHelp(args))
        {
            stdout.WriteLine(HelpText);
            return 0;
        }

        if (args.Count >= 2
            && string.Equals(args[0], "audit", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[1], "--os", StringComparison.OrdinalIgnoreCase))
        {
            var identity = (readOs ?? OsIdentityReader.Read)();
            WriteOs(stdout, identity);
            return 0;
        }

        if (string.Equals(args[0], "audit", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Phase 1: only 'windowslab audit --os' is implemented.");
            return 1;
        }

        stderr.WriteLine("Unknown command. Use windowslab --help.");
        return 1;
    }

    private static bool IsHelp(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is "-h" or "--help" or "-?" or "help")
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteOs(TextWriter stdout, OsIdentity identity)
    {
        stdout.WriteLine($"family: {identity.FamilyLabel}");
        stdout.WriteLine($"isWindows11: {identity.IsWindows11}");
        stdout.WriteLine($"build: {identity.Build}");
        stdout.WriteLine($"ubr: {identity.Ubr}");
        stdout.WriteLine($"displayVersion: {identity.DisplayVersion}");
        stdout.WriteLine($"editionId: {identity.EditionId}");
        stdout.WriteLine($"productName: {identity.ProductName} (not used for family)");
        if (identity.CompositionEditionId is not null)
        {
            stdout.WriteLine($"compositionEditionId: {identity.CompositionEditionId}");
        }

        if (identity.ProductNameWarning is not null)
        {
            stdout.WriteLine($"warning: {identity.ProductNameWarning}");
        }
    }
}
