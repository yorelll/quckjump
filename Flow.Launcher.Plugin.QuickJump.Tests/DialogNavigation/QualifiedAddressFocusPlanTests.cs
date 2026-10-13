using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class QualifiedAddressFocusPlanTests
{
    [Fact]
    public void Select_UnqualifiedAddressAlwaysAborts()
    {
        Assert.Equal(
            QualifiedAddressFocusPath.AbortUnqualified,
            QualifiedAddressFocusPlan.Select(
                addressQualified: false,
                attachSucceeded: true,
                attachFocusVerified: true,
                uiaFocusVerified: true));
    }

    [Fact]
    public void Select_VerifiedAttachPathWins()
    {
        Assert.Equal(
            QualifiedAddressFocusPath.AttachSetFocus,
            QualifiedAddressFocusPlan.Select(
                addressQualified: true,
                attachSucceeded: true,
                attachFocusVerified: true,
                uiaFocusVerified: false));
    }

    [Fact]
    public void Select_AttachFailureUsesOnlyVerifiedUiaFallback()
    {
        Assert.Equal(
            QualifiedAddressFocusPath.UiaFocusFallback,
            QualifiedAddressFocusPlan.Select(
                addressQualified: true,
                attachSucceeded: false,
                attachFocusVerified: false,
                uiaFocusVerified: true));
        Assert.Equal(
            QualifiedAddressFocusPath.AbortUnverified,
            QualifiedAddressFocusPlan.Select(
                addressQualified: true,
                attachSucceeded: false,
                attachFocusVerified: false,
                uiaFocusVerified: false));
    }
}
