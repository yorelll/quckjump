using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class JumpFailurePlanTests
{
    [Fact]
    public void DecideNoDialogFailure_NoFallbackReportsDialogNotFound()
    {
        Assert.Equal(JumpFailure.DialogNotFound, JumpFailurePlan.DecideNoDialogFailure(fallbackFound: false, fallbackElevated: false));
    }

    [Fact]
    public void DecideNoDialogFailure_ElevatedFallbackExplainsUipiInsteadOfSilence()
    {
        Assert.Equal(JumpFailure.ElevatedTarget, JumpFailurePlan.DecideNoDialogFailure(fallbackFound: true, fallbackElevated: true));
        Assert.Equal(JumpFailure.DialogNotFound, JumpFailurePlan.DecideNoDialogFailure(fallbackFound: true, fallbackElevated: false));
    }

    [Fact]
    public void DecideNoDialogFailure_UsesProcessElevationLookup()
    {
        Assert.Equal(
            JumpFailure.ElevatedTarget,
            JumpFailurePlan.DecideNoDialogFailure(fallbackFound: true, fallbackProcessId: 42, currentProcessId: 7, processId => processId == 42));
    }

    [Fact]
    public void DecideNoDialogFailure_IgnoresFallbackInTheCurrentProcess()
    {
        Assert.Equal(
            JumpFailure.DialogNotFound,
            JumpFailurePlan.DecideNoDialogFailure(fallbackFound: true, fallbackProcessId: 7, currentProcessId: 7, _ => true));
    }

    [Fact]
    public void EveryFailureHasAVisibleMessageMapping()
    {
        foreach (JumpFailure failure in System.Enum.GetValues<JumpFailure>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Flow.Launcher.Plugin.QuickJump.Localization.QuickJumpText.Failure(failure)), failure.ToString());
        }
    }
}
