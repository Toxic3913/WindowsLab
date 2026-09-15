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
    Error
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
/// Beta 0.1 lab apply: HKCU registry only, LOW risk, OFFICIAL/STRONG evidence.
/// Backup previous value (or missing) before write; refuse otherwise (D005/D018).
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

    public static ApplyResult Apply(
        TweakDefinition tweak,
        IRegistryReader reader,
        IRegistryWriter writer,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        if (!IsLabEligible(tweak))
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
