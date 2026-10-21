using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class CustomEntryDialogPresentationPlanTests
{
    [Fact]
    public void Create_UsableUnrelatedOwnerUsesOwnerWithoutTopmost()
    {
        var plan = CustomEntryDialogPresentationPlan.Create(hasUsableUnrelatedOwner: true);

        Assert.True(plan.UseOwner);
        Assert.False(plan.CenterOnPrimaryScreen);
        Assert.False(plan.TemporarilyTopmost);
        Assert.True(plan.IsModeless);
    }

    [Fact]
    public void Create_NoUsableUnrelatedOwnerUsesTopLevelCenteredTemporaryTopmostWindow()
    {
        var plan = CustomEntryDialogPresentationPlan.Create(hasUsableUnrelatedOwner: false);

        Assert.False(plan.UseOwner);
        Assert.True(plan.CenterOnPrimaryScreen);
        Assert.True(plan.TemporarilyTopmost);
        Assert.True(plan.IsModeless);
    }
}
