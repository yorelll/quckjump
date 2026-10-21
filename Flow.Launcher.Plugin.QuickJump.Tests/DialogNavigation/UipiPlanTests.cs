using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class UipiPlanTests
{
    [Fact]
    public void Decide_BlocksHigherIntegrityTarget()
    {
        var flow = new ProcessIntegrityResult(0x2000, 0);
        var target = new ProcessIntegrityResult(0x3000, 0);

        Assert.Equal(UipiDecision.BlockElevatedTarget, UipiPlan.Decide(flow, target));
    }

    [Fact]
    public void Decide_AllowsEqualOrLowerIntegrityTarget()
    {
        var flow = new ProcessIntegrityResult(0x3000, 0);

        Assert.Equal(UipiDecision.Proceed, UipiPlan.Decide(flow, new ProcessIntegrityResult(0x3000, 0)));
        Assert.Equal(UipiDecision.Proceed, UipiPlan.Decide(flow, new ProcessIntegrityResult(0x2000, 0)));
    }

    [Fact]
    public void Decide_TreatsAccessDeniedTargetTokenAsLikelyElevated()
    {
        var flow = new ProcessIntegrityResult(0x2000, 0);
        var target = new ProcessIntegrityResult(null, UipiPlan.ErrorAccessDenied);

        Assert.Equal(UipiDecision.BlockElevatedTarget, UipiPlan.Decide(flow, target));
    }

    [Fact]
    public void Decide_ProceedsWhenIntegrityCannotBeQueriedInNormalCases()
    {
        var flow = new ProcessIntegrityResult(null, 0);
        var target = new ProcessIntegrityResult(null, 0);

        Assert.Equal(UipiDecision.Proceed, UipiPlan.Decide(flow, target));
    }
}
