using System.Xml.Linq;

namespace WindowsLab.Core.Tests;

public sealed class PublishNameCollisionTests
{
    [Fact]
    public void App_and_Cli_assembly_names_differ_on_case_insensitive_windows()
    {
        var root = FindRepoRoot();
        var app = XDocument.Load(Path.Combine(root, "src", "WindowsLab.App", "WindowsLab.App.csproj"));
        var cli = XDocument.Load(Path.Combine(root, "src", "WindowsLab.Cli", "WindowsLab.Cli.csproj"));

        var appName = app.Descendants("AssemblyName").Select(e => e.Value).FirstOrDefault() ?? "WindowsLab";
        var cliName = cli.Descendants("AssemblyName").Select(e => e.Value).FirstOrDefault() ?? "windowslab";

        Assert.False(
            string.Equals(appName, cliName, StringComparison.OrdinalIgnoreCase),
            $"App '{appName}' and CLI '{cliName}' collide on Windows case-insensitive FS and overwrite each other in publish.");
        Assert.Equal("WindowsLab", appName, ignoreCase: false);
        Assert.Equal("windowslab-cli", cliName, ignoreCase: false);
    }

    [Fact]
    public void App_is_WinExe_Cli_is_Exe()
    {
        var root = FindRepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "WindowsLab.App", "WindowsLab.App.csproj"));
        var cli = File.ReadAllText(Path.Combine(root, "src", "WindowsLab.Cli", "WindowsLab.Cli.csproj"));
        Assert.Contains("<OutputType>WinExe</OutputType>", app, StringComparison.Ordinal);
        Assert.Contains("<OutputType>Exe</OutputType>", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("<OutputType>WinExe</OutputType>", cli, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
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

        throw new DirectoryNotFoundException("WindowsLab.sln");
    }
}
