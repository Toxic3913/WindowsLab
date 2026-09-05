namespace WindowsLab.Core;

/// <summary>
/// Shared probe/tweak outcome. Denied is a successful audit gap, not a missing field.
/// </summary>
public enum ProbeStatus
{
    Ok,
    Denied,
    Unsupported,
    Error,
    Timeout
}
