using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class CustomEntryDialogSessionPlanTests
{
    [Fact]
    public void Decide_NoLiveDialogCreatesModelessEditorWithoutBlockingSettings()
    {
        Assert.Equal(
            CustomEntryDialogSessionAction.CreateModeless,
            CustomEntryDialogSessionPlan.Decide(hasLiveDialog: false));
    }

    [Fact]
    public void Decide_LiveDialogReusesAndActivatesItRatherThanCreatingAnother()
    {
        Assert.Equal(
            CustomEntryDialogSessionAction.ReuseAndActivate,
            CustomEntryDialogSessionPlan.Decide(hasLiveDialog: true));
    }

    [Fact]
    public void SavedCallback_RunsOnlyAfterSuccessfulSaveAndClose()
    {
        Assert.True(CustomEntryDialogSessionPlan.InvokeSavedCallbackAfterClose(savedSuccessfully: true));
        Assert.False(CustomEntryDialogSessionPlan.InvokeSavedCallbackAfterClose(savedSuccessfully: false));
    }
}
