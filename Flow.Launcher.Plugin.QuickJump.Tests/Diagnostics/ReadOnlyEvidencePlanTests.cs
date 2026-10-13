using Flow.Launcher.Plugin.QuickJump.Diagnostics;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Diagnostics;

public sealed class ReadOnlyEvidencePlanTests
{
    [Fact]
    public void ClassifyIntegrityRid_MapsWellKnownIntegrityLevels()
    {
        Assert.Equal(IntegrityLevel.Low, ReadOnlyEvidencePlan.ClassifyIntegrityRid(0x1000));
        Assert.Equal(IntegrityLevel.Medium, ReadOnlyEvidencePlan.ClassifyIntegrityRid(0x2000));
        Assert.Equal(IntegrityLevel.High, ReadOnlyEvidencePlan.ClassifyIntegrityRid(0x3000));
        Assert.Equal(IntegrityLevel.System, ReadOnlyEvidencePlan.ClassifyIntegrityRid(0x4000));
        Assert.Equal(IntegrityLevel.Other, ReadOnlyEvidencePlan.ClassifyIntegrityRid(0x1234));
    }

    [Fact]
    public void ClassifyIntegrityRid_NullIsUnavailable()
    {
        Assert.Equal(IntegrityLevel.Unavailable, ReadOnlyEvidencePlan.ClassifyIntegrityRid(null));
    }

    [Fact]
    public void Decide_OnlyQualifiedTargetAndAddressPermitReadOnlyObservation()
    {
        Assert.Equal(EvidenceSafetyDecision.ObserveOnly, ReadOnlyEvidencePlan.Decide(targetWindowQualified: true, addressQualified: true));
        Assert.Equal(EvidenceSafetyDecision.AbortUnavailable, ReadOnlyEvidencePlan.Decide(targetWindowQualified: false, addressQualified: true));
        Assert.Equal(EvidenceSafetyDecision.AbortUnavailable, ReadOnlyEvidencePlan.Decide(targetWindowQualified: true, addressQualified: false));
    }
}
