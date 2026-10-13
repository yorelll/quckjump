namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Pure classification of evidence-only native focus attempts.</summary>
internal enum NativeFocusEvidenceOutcome
{
    QueueFailure,
    AttachFailure,
    SetFocusFailure,
    FocusVerificationFailure,
    Verified,
}

internal static class NativeEvidencePlan
{
    public static NativeFocusEvidenceOutcome Classify(
        bool queueCreated,
        bool attached,
        bool setFocusVerified) =>
        !queueCreated ? NativeFocusEvidenceOutcome.QueueFailure :
        !attached ? NativeFocusEvidenceOutcome.AttachFailure :
        !setFocusVerified ? NativeFocusEvidenceOutcome.FocusVerificationFailure :
        NativeFocusEvidenceOutcome.Verified;
}
