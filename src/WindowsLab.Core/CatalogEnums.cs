namespace WindowsLab.Core;

public static class ExitCodes
{
    public const int Ok = 0;
    public const int Generic = 1;
    public const int PolicyBlocked = 13;
}

public enum EvidenceGrade
{
    Official,
    Strong,
    Community,
    Experimental,
    Unknown
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum UserProfile
{
    Balanced,
    Developer,
    Gaming,
    Virtualization
}
