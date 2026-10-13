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

    [Theory]
    [InlineData(false, true, CrossThreadFocusDecision.AbortAttachFailed)]
    [InlineData(true, false, CrossThreadFocusDecision.AbortSetFocusFailed)]
    [InlineData(true, true, CrossThreadFocusDecision.AbortVerificationFailed)]
    public void Decide_FailedNativeStepAbortsWithoutWriting(bool attachSucceeded, bool setFocusSucceeded, CrossThreadFocusDecision expected)
    {
        Assert.Equal(
            expected,
            CrossThreadFocusPlan.Decide(
                addressEditQualified: true,
                attachSucceeded: attachSucceeded,
                setFocusSucceeded: setFocusSucceeded,
                verifiedFocus: false));
    }
}
