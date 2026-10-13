using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class CrossThreadFocusPlanTests
{
    [Fact]
    public void Decide_QualifiedAttachSetFocusAndVerificationAllowsWriting()
    {
        Assert.Equal(
            CrossThreadFocusDecision.WriteText,
            CrossThreadFocusPlan.Decide(
                addressEditQualified: true,
                attachSucceeded: true,
                setFocusSucceeded: true,
                verifiedFocus: true));
    }

    [Fact]
    public void Decide_UnqualifiedAddressEditAlwaysAborts()
    {
        Assert.Equal(
            CrossThreadFocusDecision.AbortUnqualified,
            CrossThreadFocusPlan.Decide(
                addressEditQualified: false,
                attachSucceeded: true,
                setFocusSucceeded: true,
                verifiedFocus: true));
    }

    [Fact]
    public void Decide_FailedNativeStepsAbortWithoutWriting()
    {
        Assert.Equal(
            CrossThreadFocusDecision.AbortAttachFailed,
            CrossThreadFocusPlan.Decide(addressEditQualified: true, attachSucceeded: false, setFocusSucceeded: true, verifiedFocus: false));
        Assert.Equal(
            CrossThreadFocusDecision.AbortSetFocusFailed,
            CrossThreadFocusPlan.Decide(addressEditQualified: true, attachSucceeded: true, setFocusSucceeded: false, verifiedFocus: false));
        Assert.Equal(
            CrossThreadFocusDecision.AbortVerificationFailed,
            CrossThreadFocusPlan.Decide(addressEditQualified: true, attachSucceeded: true, setFocusSucceeded: true, verifiedFocus: false));
    }
}
