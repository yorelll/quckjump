namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Pure classifications for read-only security, desktop, and UIA evidence.</summary>
internal enum IntegrityLevel
{
    Unavailable,
    Low,
    Medium,
    High,
    System,
    Other,
}

internal enum EvidenceSafetyDecision
{
    ObserveOnly,
    AbortUnavailable,
}

internal static class ReadOnlyEvidencePlan
{
    public static IntegrityLevel ClassifyIntegrityRid(uint? rid) => rid switch
    {
        null => IntegrityLevel.Unavailable,
        0x1000 => IntegrityLevel.Low,
        0x2000 => IntegrityLevel.Medium,
        0x3000 => IntegrityLevel.High,
        0x4000 => IntegrityLevel.System,
        _ => IntegrityLevel.Other,
    };

    public static EvidenceSafetyDecision Decide(bool targetWindowQualified, bool addressQualified) =>
        targetWindowQualified && addressQualified
            ? EvidenceSafetyDecision.ObserveOnly
            : EvidenceSafetyDecision.AbortUnavailable;
}
