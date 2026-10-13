namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure, bounded timing policy for modern common-dialog address-bar navigation.</summary>
internal sealed record ModernAddressNavigationPlan(int FocusAttempts, int FocusDelayMilliseconds, int NavigationDelayMilliseconds)
{
    public static ModernAddressNavigationPlan Default { get; } = new(FocusAttempts: 6, FocusDelayMilliseconds: 75, NavigationDelayMilliseconds: 75);

    public bool ShouldTry(int attempt) => attempt >= 0 && attempt < FocusAttempts;
}

/// <summary>Decides whether the observed focus after the address shortcut is safe to write to.</summary>
internal static class ModernAddressFocusPlan
{
    public static bool IsReady(bool dialogIsForeground, bool addressEditExists, bool addressEditVisible, bool addressEditHasFocus) =>
        dialogIsForeground && addressEditExists && addressEditVisible && addressEditHasFocus;
}
