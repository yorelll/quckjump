namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure safety gate for focusing a verified modern common-dialog address edit.</summary>
internal enum CrossThreadFocusDecision
{
    AbortUnqualified,
    AbortAttachFailed,
    AbortSetFocusFailed,
    AbortVerificationFailed,
    WriteText,
}

internal static class CrossThreadFocusPlan
{
    public static CrossThreadFocusDecision Decide(
        bool addressEditQualified,
        bool attachSucceeded,
        bool setFocusSucceeded,
        bool verifiedFocus) =>
        !addressEditQualified ? CrossThreadFocusDecision.AbortUnqualified :
        !attachSucceeded ? CrossThreadFocusDecision.AbortAttachFailed :
        !setFocusSucceeded ? CrossThreadFocusDecision.AbortSetFocusFailed :
        !verifiedFocus ? CrossThreadFocusDecision.AbortVerificationFailed :
        CrossThreadFocusDecision.WriteText;
}
