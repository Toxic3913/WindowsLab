namespace WindowsLab.Core;

/// <summary>
/// Gates system (elevated / HKLM) apply. Default off on daily-driver hosts (D020).
/// Opt-in: operator setting, CLI --i-am-on-lab-vm, or machine name containing WindowsLab-Test.
/// </summary>
public static class SystemApplyPolicy
{
    public const string LabVmNameHint = "WindowsLab-Test";

    public static bool IsAllowed(OperatorSettings settings, bool iAmOnLabVmFlag = false)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (iAmOnLabVmFlag || settings.AllowSystemApply)
        {
            return true;
        }

        var name = Environment.MachineName ?? "";
        return name.Contains(LabVmNameHint, StringComparison.OrdinalIgnoreCase);
    }

    public static string RefuseMessage =>
        "System apply blocked on this host. Use the lab VM, enable AllowSystemApply in settings, or pass --i-am-on-lab-vm.";
}
