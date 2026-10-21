namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>
/// Context-menu editors must never be owned by Flow's main window: Flow hides
/// that window after a context action, which would hide an owned editor too.
/// </summary>
internal sealed record CustomEntryDialogPresentationPlan(
    bool UseOwner,
    bool CenterOnPrimaryScreen,
    bool TemporarilyTopmost,
    bool IsModeless)
{
    /// <summary>Flow-owned windows are excluded from owner selection entirely.</summary>
    public static CustomEntryDialogPresentationPlan Create(bool hasUsableUnrelatedOwner) =>
        hasUsableUnrelatedOwner
            ? new CustomEntryDialogPresentationPlan(UseOwner: true, CenterOnPrimaryScreen: false, TemporarilyTopmost: false, IsModeless: true)
            : new CustomEntryDialogPresentationPlan(UseOwner: false, CenterOnPrimaryScreen: true, TemporarilyTopmost: true, IsModeless: true);
}
