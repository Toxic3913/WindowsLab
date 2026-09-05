using WindowsLab.Cli;
using WindowsLab.Core;

namespace WindowsLab.Cli.Tests;

public sealed class CliAppTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Help_lists_audit_os(string flag)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run([flag], stdout, stderr);

        Assert.Equal(0, code);
        Assert.Contains("audit --os", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_os_prints_windows_11_from_build_not_product_name()
    {
        var os = OsIdentityMapper.Map(
            new OsIdentityFacts(10, 0, 26200, 9168, "25H2", "Professional", "Windows 10 Pro", null));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run(["audit", "--os"], stdout, stderr, () => os);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("family: Windows 11", text, StringComparison.Ordinal);
        Assert.Contains("isWindows11: True", text, StringComparison.Ordinal);
        Assert.Contains("build: 26200", text, StringComparison.Ordinal);
        Assert.Contains("displayVersion: 25H2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("family: Windows 10", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_command_exits_nonzero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run(["tweak", "apply"], stdout, stderr);

        Assert.Equal(1, code);
        Assert.Contains("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_without_os_flag_exits_nonzero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run(["audit"], stdout, stderr);

        Assert.Equal(1, code);
        Assert.Contains("audit --os", stderr.ToString(), StringComparison.Ordinal);
    }
}
