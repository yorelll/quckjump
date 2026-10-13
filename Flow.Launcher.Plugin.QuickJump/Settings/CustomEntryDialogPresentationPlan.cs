namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Describes how the modeless context-menu editor should be presented safely.</summary>
internal sealed record CustomEntryDialogPresentationPlan(
    bool UseOwner,
    bool CenterOnPrimaryScreen,
    bool TemporarilyTopmost,
    bool IsModeless)
{
    public static CustomEntryDialogPresentationPlan Create(bool ownerIsVisibleAndEnabled) =>
        ownerIsVisibleAndEnabled
            ? new CustomEntryDialogPresentationPlan(UseOwner: true, CenterOnPrimaryScreen: false, TemporarilyTopmost: false, IsModeless: true)
            : new CustomEntryDialogPresentationPlan(UseOwner: false, CenterOnPrimaryScreen: true, TemporarilyTopmost: true, IsModeless: true);
}
