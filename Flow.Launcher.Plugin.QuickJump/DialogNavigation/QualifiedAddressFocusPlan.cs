namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure safety decisions for queue attachment and UIA focus fallback.</summary>
internal enum QualifiedAddressFocusPath
{
    AbortUnqualified,
    AttachSetFocus,
    UiaFocusFallback,
    AbortUnverified,
}

internal static class QualifiedAddressFocusPlan
{
    public static QualifiedAddressFocusPath Select(
        bool addressQualified,
        bool attachSucceeded,
        bool attachFocusVerified,
        bool uiaFocusVerified) =>
        !addressQualified ? QualifiedAddressFocusPath.AbortUnqualified :
        attachSucceeded && attachFocusVerified ? QualifiedAddressFocusPath.AttachSetFocus :
        uiaFocusVerified ? QualifiedAddressFocusPath.UiaFocusFallback :
        QualifiedAddressFocusPath.AbortUnverified;
}
