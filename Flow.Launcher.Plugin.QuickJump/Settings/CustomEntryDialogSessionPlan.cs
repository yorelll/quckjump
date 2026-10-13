namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Pure lifecycle rules for the guarded, modeless context-menu editor.</summary>
internal enum CustomEntryDialogSessionAction
{
    CreateModeless,
    ReuseAndActivate,
}

internal static class CustomEntryDialogSessionPlan
{
    public static CustomEntryDialogSessionAction Decide(bool hasLiveDialog) =>
        hasLiveDialog
            ? CustomEntryDialogSessionAction.ReuseAndActivate
            : CustomEntryDialogSessionAction.CreateModeless;

    public static bool InvokeSavedCallbackAfterClose(bool savedSuccessfully) => savedSuccessfully;
}
