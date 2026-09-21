using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using WindowsLab.Core;

namespace WindowsLab.Tweaks;

public enum ApplyOutcome
{
    Ok,
    AlreadyMatched,
    PolicyRefused,
    AccessDenied,
    VerifyFailed,
    Error,
    NeedsElevation
}

public sealed record TweakBackupEntry(
    string TweakId,
    string Hive,
    string Path,
    string Name,
    string ValueKind,
    string? PreviousValue,
    bool ValueExisted,
    DateTimeOffset TimestampUtc);

public sealed record ApplyResult(
    ApplyOutcome Outcome,
    string Message,
    TweakBackupEntry? Backup = null)
{
    public bool Succeeded => Outcome is ApplyOutcome.Ok or ApplyOutcome.AlreadyMatched;
}

/// <summary>
/// Lab apply (D018): HKCU registry, LOW, OFFICIAL/STRONG.
/// System apply eligibility (D020): OFFICIAL/STRONG registry/service/task/power; elevation as needed.
/// </summary>
public static class TweakApplicator
{
    public static bool IsLabEligible(TweakDefinition tweak)
    {
        ArgumentNullException.ThrowIfNull(tweak);

        if (tweak.Risk != RiskLevel.Low)
        {
            return false;
        }

        if (tweak.Evidence is not (EvidenceGrade.Official or EvidenceGrade.Strong))
        {
            return false;
        }

        if (tweak.RequiresReboot || tweak.AffectsSecurity || tweak.AffectsCompatibility)
        {
            return false;
        }

        if (tweak.Ops.Count != 1)
        {
            return false;
        }

        foreach (var op in tweak.Ops)
        {
            if (!string.Equals(op.Kind, "registry", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (op.Hive is null || op.Path is null || op.Name is null)
            {
                return false;
            }

            if (!IsHkcu(op.Hive))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Eligible for system apply pipeline (may require elevation + ProgramData backup).</summary>
    public static bool IsApplyEligible(TweakDefinition tweak)
    {
        ArgumentNullException.ThrowIfNull(tweak);

        if (tweak.Evidence is not (EvidenceGrade.Official or EvidenceGrade.Strong))
        {
            return false;
        }

        if (tweak.Evidence is EvidenceGrade.Unknown or EvidenceGrade.Experimental)
        {
            return false;
        }

        if (tweak.Risk is RiskLevel.Critical)
        {
            return false;
        }

        if (tweak.Ops.Count == 0)
        {
            return false;
        }

        foreach (var op in tweak.Ops)
        {
            var kind = op.Kind?.ToLowerInvariant() ?? "";
            switch (kind)
            {
                case "registry":
                    if (op.Hive is null || op.Path is null || op.Name is null)
                    {
                        return false;
                    }

                    break;
                case "service":
                    if (string.IsNullOrWhiteSpace(op.Name) || string.IsNullOrWhiteSpace(op.Value?.ToString()))
                    {
                        return false;
                    }

                    if (ProtectedServices.IsBlocked(op.Name!))
                    {
                        return false;
                    }

                    break;
                case "task":
                    if (string.IsNullOrWhiteSpace(op.Path ?? op.Name))
                    {
                        return false;
                    }

                    break;
                case "power":
                    if (string.IsNullOrWhiteSpace(op.Value?.ToString()))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    public static bool NeedsElevation(TweakDefinition tweak)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        return tweak.Ops.Any(NeedsElevation);
    }

    public static bool NeedsElevation(TweakOp op)
    {
        var kind = op.Kind?.ToLowerInvariant() ?? "";
        return kind switch
        {
            "registry" => op.Hive is not null && IsHklm(op.Hive),
            "service" or "task" or "power" => true,
            _ => false
        };
    }

    public static ApplyResult Apply(
        TweakDefinition tweak,
        IRegistryReader reader,
        IRegistryWriter writer,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        if (!IsLabEligible(tweak) && !IsApplyEligible(tweak))
        {
            return new ApplyResult(ApplyOutcome.PolicyRefused,
                "No elegible: evidencia OFFICIAL/STRONG y ops registry/service/task/power soportadas.");
        }

        // Lab path keeps HKCU-only writer semantics when called without system pipeline.
        if (IsLabEligible(tweak) && !NeedsElevation(tweak))
        {
            return ApplyRegistryOnly(tweak, reader, writer, dryRun, labOnly: true);
        }

        if (!IsApplyEligible(tweak))
        {
            return new ApplyResult(ApplyOutcome.PolicyRefused,
                "Solo HKCU lab o system-eligible OFFICIAL/STRONG.");
        }

        if (NeedsElevation(tweak))
        {
            return new ApplyResult(ApplyOutcome.NeedsElevation,
                "Este tweak requiere Worker elevado (HKLM/service/task/power).");
        }

        return ApplyRegistryOnly(tweak, reader, writer, dryRun, labOnly: false);
    }

    /// <summary>Applies all ops in-process (caller must be elevated for HKLM/service/…).</summary>
    public static ApplyResult ApplyElevated(
        TweakDefinition tweak,
        IRegistryReader reader,
        IRegistryWriter writer,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        if (!IsApplyEligible(tweak))
        {
            return new ApplyResult(ApplyOutcome.PolicyRefused, "Not system-apply eligible.");
        }

        var before = TweakDetector.Detect(tweak, reader);
        if (before.MatchesDesired)
        {
            return new ApplyResult(ApplyOutcome.AlreadyMatched, "Ya coincide con el objetivo.");
        }

        TweakBackupEntry? lastBackup = null;
        try
        {
            foreach (var op in tweak.Ops)
            {
                var kind = op.Kind?.ToLowerInvariant() ?? "";
                if (kind == "registry")
                {
                    object? previous;
                    try
                    {
                        previous = reader.GetValue(op.Hive!, op.Path!, op.Name!);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        return new ApplyResult(ApplyOutcome.AccessDenied, ex.Message);
                    }

                    var vk = ParseKind(op.ValueKind);
                    var value = CoerceValue(op.Value ?? tweak.DesiredEquals, vk);
                    lastBackup = new TweakBackupEntry(
                        tweak.Id, op.Hive!, op.Path!, op.Name!, vk.ToString(),
                        FormatStored(previous), previous is not null, DateTimeOffset.UtcNow);

                    if (!dryRun)
                    {
                        writer.SetValue(op.Hive!, op.Path!, op.Name!, value, vk);
                    }
                }
                else if (kind == "service")
                {
                    if (dryRun)
                    {
                        continue;
                    }

                    ServiceOpExecutor.SetStartType(op.Name!, op.Value?.ToString() ?? tweak.DesiredEquals);
                }
                else if (kind == "task")
                {
                    if (dryRun)
                    {
                        continue;
                    }

                    var path = op.Path ?? op.Name!;
                    var enable = string.Equals(op.Value?.ToString(), "enable", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(op.Value?.ToString(), "1", StringComparison.OrdinalIgnoreCase);
                    var disable = string.Equals(op.Value?.ToString(), "disable", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(op.Value?.ToString(), "0", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(tweak.DesiredEquals, "0", StringComparison.OrdinalIgnoreCase);
                    TaskOpExecutor.SetEnabled(path, enable || !disable);
                }
                else if (kind == "power")
                {
                    if (dryRun)
                    {
                        continue;
                    }

                    PowerOpExecutor.SetActiveScheme(op.Value?.ToString() ?? tweak.DesiredEquals);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApplyResult(ApplyOutcome.AccessDenied, ex.Message, lastBackup);
        }
        catch (Exception ex)
        {
            return new ApplyResult(ApplyOutcome.Error, ex.Message, lastBackup);
        }

        if (dryRun)
        {
            return new ApplyResult(ApplyOutcome.Ok, "Dry-run: no escrituras.", lastBackup);
        }

        var after = TweakDetector.Detect(tweak, reader);
        if (!after.MatchesDesired)
        {
            return new ApplyResult(ApplyOutcome.VerifyFailed,
                $"Escrito pero no verifica: actual={after.ActualDisplay} desired={after.DesiredDisplay}",
                lastBackup);
        }

        return new ApplyResult(ApplyOutcome.Ok, "Aplicado y verificado.", lastBackup);
    }

    private static ApplyResult ApplyRegistryOnly(
        TweakDefinition tweak,
        IRegistryReader reader,
        IRegistryWriter writer,
        bool dryRun,
        bool labOnly)
    {
        if (labOnly && !IsLabEligible(tweak))
        {
            return new ApplyResult(ApplyOutcome.PolicyRefused,
                "Solo HKCU, riesgo LOW, evidencia OFFICIAL/STRONG, sin reboot/seguridad.");
        }

        var before = TweakDetector.Detect(tweak, reader);
        if (before.MatchesDesired)
        {
            return new ApplyResult(ApplyOutcome.AlreadyMatched, "Ya coincide con el objetivo.");
        }

        TweakBackupEntry? lastBackup = null;
        try
        {
            foreach (var op in tweak.Ops)
            {
                if (!string.Equals(op.Kind, "registry", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? previous;
                try
                {
                    previous = reader.GetValue(op.Hive!, op.Path!, op.Name!);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return new ApplyResult(ApplyOutcome.AccessDenied, ex.Message);
                }

                var kind = ParseKind(op.ValueKind);
                var value = CoerceValue(op.Value ?? tweak.DesiredEquals, kind);
                lastBackup = new TweakBackupEntry(
                    tweak.Id,
                    op.Hive!,
                    op.Path!,
                    op.Name!,
                    kind.ToString(),
                    FormatStored(previous),
                    previous is not null,
                    DateTimeOffset.UtcNow);

                if (dryRun)
                {
                    continue;
                }

                writer.SetValue(op.Hive!, op.Path!, op.Name!, value, kind);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApplyResult(ApplyOutcome.AccessDenied, ex.Message, lastBackup);
        }
        catch (Exception ex)
        {
            return new ApplyResult(ApplyOutcome.Error, ex.Message, lastBackup);
        }

        if (dryRun)
        {
            return new ApplyResult(ApplyOutcome.Ok, "Dry-run: se escribiría el registro (0 escrituras).", lastBackup);
        }

        var after = TweakDetector.Detect(tweak, reader);
        if (!after.MatchesDesired)
        {
            return new ApplyResult(ApplyOutcome.VerifyFailed,
                $"Escrito pero no verifica: actual={after.ActualDisplay} desired={after.DesiredDisplay}",
                lastBackup);
        }

        return new ApplyResult(ApplyOutcome.Ok, "Aplicado y verificado.", lastBackup);
    }

    public static ApplyResult Rollback(TweakBackupEntry backup, IRegistryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(writer);

        try
        {
            var kind = ParseKind(backup.ValueKind);
            if (!backup.ValueExisted || backup.PreviousValue is null)
            {
                writer.DeleteValue(backup.Hive, backup.Path, backup.Name);
            }
            else
            {
                writer.SetValue(backup.Hive, backup.Path, backup.Name, CoerceValue(backup.PreviousValue, kind), kind);
            }

            return new ApplyResult(ApplyOutcome.Ok, "Rollback aplicado.", backup);
        }
        catch (Exception ex)
        {
            return new ApplyResult(ApplyOutcome.Error, ex.Message, backup);
        }
    }

    public static string PersistBackup(TweakBackupEntry backup, string? directory = null)
    {
        ConfigChannels.EnsureRuntimeFolders();
        var dir = directory ?? Path.Combine(ConfigChannels.UserRoot, "backups");
        Directory.CreateDirectory(dir);
        var stamp = backup.TimestampUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeId = backup.TweakId.Replace('.', '-');
        var path = Path.Combine(dir, $"{stamp}-{safeId}.json");
        var json = JsonSerializer.Serialize(backup, BackupJsonOptions);
        File.WriteAllText(path, json);
        return path;
    }

    private static readonly JsonSerializerOptions BackupJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static bool IsHkcu(string hive) =>
        hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
        || hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);

    private static bool IsHklm(string hive) =>
        hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)
        || hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase);

    internal static RegistryValueKind ParseKind(string? valueKind) =>
        (valueKind ?? "DWord").ToUpperInvariant() switch
        {
            "SZ" or "STRING" or "REG_SZ" => RegistryValueKind.String,
            "EXPANDSZ" or "EXPAND_SZ" or "REG_EXPAND_SZ" => RegistryValueKind.ExpandString,
            "MULTI_SZ" or "MULTISZ" or "REG_MULTI_SZ" => RegistryValueKind.MultiString,
            "QWORD" or "REG_QWORD" => RegistryValueKind.QWord,
            "BINARY" or "REG_BINARY" => RegistryValueKind.Binary,
            _ => RegistryValueKind.DWord
        };

    internal static object CoerceValue(object? raw, RegistryValueKind kind)
    {
        var text = raw switch
        {
            null => "0",
            string s => s,
            _ => Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "0"
        };

        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => text,
            RegistryValueKind.QWord => long.Parse(text, CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => Convert.FromHexString(text.Replace("-", "", StringComparison.Ordinal)),
            RegistryValueKind.MultiString => text.Split('\0', StringSplitOptions.RemoveEmptyEntries),
            _ => int.Parse(text, CultureInfo.InvariantCulture)
        };
    }

    private static string? FormatStored(object? value) => value switch
    {
        null => null,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        byte[] b => Convert.ToHexString(b),
        string[] arr => string.Join('\0', arr),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}
