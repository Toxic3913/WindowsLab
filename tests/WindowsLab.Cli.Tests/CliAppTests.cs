using WindowsLab.Cli;
using WindowsLab.Core;
using WindowsLab.Tweaks;

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
        Assert.Contains("windowslab-cli", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("app list", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("app install", stdout.ToString(), StringComparison.Ordinal);
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
        Assert.Contains("lab-apply", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Tweak_apply_dry_run_lab_flag_does_not_require_live_write()
    {
        var store = new DictionaryRegistry();
        store.Set("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1);
        var tweak = new TweakDefinition(
            "explorer.show-file-extensions",
            "Show extensions",
            "d",
            "explorer",
            RiskLevel.Low,
            EvidenceGrade.Official,
            [],
            22000,
            ["*"],
            ["balanced"],
            [],
            [],
            false,
            false,
            false,
            false,
            new RegistryDetect("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", "DWord"),
            "0",
            [new TweakOp("registry", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", "DWord", "0")]);

        var services = new CliServices
        {
            LoadCatalog = () => [tweak],
            Registry = () => store
        };
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(
            ["tweak", "apply", "explorer.show-file-extensions", "--lab-apply", "--dry-run"],
            stdout,
            stderr,
            services: services);
        Assert.Equal(0, code);
        Assert.Contains("Dry-run", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, store.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt"));
    }

    private sealed class DictionaryRegistry : WindowsLab.Tweaks.IRegistryReader
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
        private static string K(string h, string p, string n) => $"{h}|{p}|{n}";
        public void Set(string h, string p, string n, object? v) => _values[K(h, p, n)] = v;
        public object? GetValue(string hive, string path, string name) =>
            _values.TryGetValue(K(hive, path, name), out var v) ? v : null;
    }

    [Fact]
    public void Preset_apply_without_yes_fails()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(["preset", "apply", "perf.max"], stdout, stderr);
        Assert.Equal(1, code);
        Assert.Contains("--yes", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Tweak_apply_without_flag_is_policy_blocked()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(["tweak", "apply", "explorer.show-file-extensions"], stdout, stderr);
        Assert.Equal(13, code);
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

    [Fact]
    public void App_install_without_yes_exits_nonzero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliApp.Run(["app", "install", "app.browser.brave"], stdout, stderr);
        Assert.Equal(1, code);
        Assert.Contains("--yes", stderr.ToString(), StringComparison.Ordinal);
    }
}
