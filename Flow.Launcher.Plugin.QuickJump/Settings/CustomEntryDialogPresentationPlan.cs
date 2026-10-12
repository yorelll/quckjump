namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>描述自定义条目编辑窗口如何安全地呈现给用户。</summary>
internal sealed record CustomEntryDialogPresentationPlan(
    bool UseOwner,
    bool CenterOnPrimaryScreen,
    bool TemporarilyTopmost)
{
    public static CustomEntryDialogPresentationPlan Create(bool ownerIsVisibleAndEnabled) =>
        ownerIsVisibleAndEnabled
            ? new CustomEntryDialogPresentationPlan(UseOwner: true, CenterOnPrimaryScreen: false, TemporarilyTopmost: false)
            : new CustomEntryDialogPresentationPlan(UseOwner: false, CenterOnPrimaryScreen: true, TemporarilyTopmost: true);
}
