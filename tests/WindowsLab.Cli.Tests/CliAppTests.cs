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
        Assert.Contains("checklist", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("preset", stdout.ToString(), StringComparison.Ordinal);
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

        var code = CliApp.Run(["not-a-command"], stdout, stderr);

        Assert.Equal(1, code);
        Assert.Contains("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Tweak_apply_is_policy_blocked()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(["tweak", "apply", "explorer.show-file-extensions"], stdout, stderr);
        Assert.Equal(13, code);
        Assert.Contains("Beta 0", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Preset_apply_is_policy_blocked()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(["preset", "apply", "perf.max"], stdout, stderr);
        Assert.Equal(13, code);
        Assert.Contains("preset apply", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_without_os_flag_prints_inventory()
    {
        var os = OsIdentityMapper.Map(
            new OsIdentityFacts(10, 0, 26200, 9168, "25H2", "Professional", "Windows 10 Pro", null));
        var inventory = new MachineInventory(
            DateTimeOffset.UtcNow,
            os,
            new CpuInventory("cpu", 4, 8, 3000, ProbeStatus.Ok),
            [new GpuInventory("gpu", "NVIDIA", 8L * 1024 * 1024 * 1024, "dxgi", ProbeStatus.Ok)],
            new RamInventory(16L * 1024 * 1024 * 1024, 2, 3600, ProbeStatus.Ok),
            [new VolumeInventory(@"C:\", "Windows", 100, 50, DriveType.Fixed)],
            new BoardInventory("m", "p", "1", null, ProbeStatus.Ok),
            new SecurityInventory(2, true, true, true, ProbeStatus.Ok, null),
            new ToolchainInventory(["git"]),
            []);
        var services = new CliServices { Audit = () => inventory, ReadOs = () => os };
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run(["audit"], stdout, stderr, services: services);

        Assert.Equal(0, code);
        Assert.Contains("Windows 11", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Checklist_json_reports_local_account_ok()
    {
        var os = OsIdentityMapper.Map(
            new OsIdentityFacts(10, 0, 26200, 9168, "25H2", "Professional", "Windows 10 Pro", null));
        var inventory = new MachineInventory(
            DateTimeOffset.UtcNow,
            os,
            new CpuInventory("cpu", 4, 8, 3000, ProbeStatus.Ok),
            [],
            new RamInventory(16L * 1024 * 1024 * 1024, 2, 3600, ProbeStatus.Ok),
            [new VolumeInventory(@"C:\", "Windows", 200L * 1024 * 1024 * 1024, 40L * 1024 * 1024 * 1024, DriveType.Fixed)],
            new BoardInventory("m", "p", "1", null, ProbeStatus.Ok),
            new SecurityInventory(2, true, true, true, ProbeStatus.Ok, null),
            new ToolchainInventory(["git"]),
            []);
        var item = new ChecklistDefinition(
            "account.local",
            "Cuenta",
            "Sesión local",
            "",
            "",
            ChecklistPolicy.Recommend,
            "localaccount",
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var services = new CliServices
        {
            Audit = () => inventory,
            ReadOs = () => os,
            LoadChecklists = () => [item],
            WindowsIdentityName = () => @"PC-HUGO\Hugo"
        };
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CliApp.Run(["checklist", "--output", "json"], stdout, stderr, services: services);

        Assert.Equal(0, code);
        var text = stdout.ToString();
        Assert.Contains("\"ok\": 1", text, StringComparison.Ordinal);
        Assert.Contains("account.local", text, StringComparison.Ordinal);
    }
}
