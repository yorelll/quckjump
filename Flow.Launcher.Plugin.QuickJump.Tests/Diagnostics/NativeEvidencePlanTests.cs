using Flow.Launcher.Plugin.QuickJump.Diagnostics;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Diagnostics;

public sealed class NativeEvidencePlanTests
{
    [Fact]
    public void Classify_QueueFailureIsReportedFirst()
    {
        Assert.Equal(
            NativeFocusEvidenceOutcome.QueueFailure,
            NativeEvidencePlan.Classify(queueCreated: false, attached: true, setFocusVerified: true));
    }

    [Fact]
    public void Classify_AttachAndFocusFailuresRemainDistinct()
    {
        Assert.Equal(
            NativeFocusEvidenceOutcome.AttachFailure,
            NativeEvidencePlan.Classify(queueCreated: true, attached: false, setFocusVerified: false));
        Assert.Equal(
            NativeFocusEvidenceOutcome.FocusVerificationFailure,
            NativeEvidencePlan.Classify(queueCreated: true, attached: true, setFocusVerified: false));
    }

    [Fact]
    public void Classify_VerifiedPathIsReported()
    {
        Assert.Equal(
            NativeFocusEvidenceOutcome.Verified,
            NativeEvidencePlan.Classify(queueCreated: true, attached: true, setFocusVerified: true));
    }
}
