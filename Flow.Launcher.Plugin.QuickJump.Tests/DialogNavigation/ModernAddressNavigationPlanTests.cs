using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class ModernAddressNavigationPlanTests
{
    [Fact]
    public void FocusPlan_AllConditionsMustBeSatisfiedBeforeWritingAddressText()
    {
        Assert.True(ModernAddressFocusPlan.IsReady(
            dialogIsForeground: true,
            addressEditExists: true,
            addressEditVisible: true,
            addressEditHasFocus: true));

        Assert.False(ModernAddressFocusPlan.IsReady(
            dialogIsForeground: true,
            addressEditExists: true,
            addressEditVisible: true,
            addressEditHasFocus: false));
        Assert.False(ModernAddressFocusPlan.IsReady(
            dialogIsForeground: false,
            addressEditExists: true,
            addressEditVisible: true,
            addressEditHasFocus: true));
        Assert.False(ModernAddressFocusPlan.IsReady(
            dialogIsForeground: true,
            addressEditExists: true,
            addressEditVisible: false,
            addressEditHasFocus: true));
    }

    [Fact]
    public void DefaultPlan_UsesBoundedFocusRetriesAndNonZeroSequencingDelays()
    {
        var plan = ModernAddressNavigationPlan.Default;

        Assert.True(plan.FocusAttempts > 0);
        Assert.True(plan.FocusDelayMilliseconds > 0);
        Assert.True(plan.NavigationDelayMilliseconds > 0);
        Assert.True(plan.ShouldTry(plan.FocusAttempts - 1));
        Assert.False(plan.ShouldTry(plan.FocusAttempts));
    }
}
