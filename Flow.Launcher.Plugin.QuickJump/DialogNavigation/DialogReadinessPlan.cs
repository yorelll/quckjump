namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure state machine for preventing global keyboard input before a native dialog is ready.</summary>
internal enum DialogReadinessDecision
{
    Wait,
    ActivateTarget,
    Ready,
    Fail,
}

/// <summary>
/// A target must be a usable native file dialog and the foreground window must
/// match that target before QuickJump sends Alt+D/Ctrl+L/Enter globally.
/// </summary>
internal static class DialogReadinessPlan
{
    public static DialogReadinessDecision Decide(
        bool targetIsUsable,
        bool targetIsForeground,
        bool flowIsForeground,
        bool mayActivateTarget,
        bool retriesExhausted) =>
        targetIsUsable && targetIsForeground && !flowIsForeground ? DialogReadinessDecision.Ready :
        retriesExhausted ? DialogReadinessDecision.Fail :
        targetIsUsable && !flowIsForeground && mayActivateTarget ? DialogReadinessDecision.ActivateTarget :
        DialogReadinessDecision.Wait;
}
