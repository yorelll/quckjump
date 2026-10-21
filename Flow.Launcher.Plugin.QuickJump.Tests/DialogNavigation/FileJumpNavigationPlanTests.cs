using System.IO;
using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class FileJumpNavigationPlanTests
{
    [Fact]
    public void GetParentFolder_ReturnsActualParentAndFileName()
    {
        string path = Path.Combine(Path.GetTempPath(), "quickjump-parent", "report.txt");

        Assert.Equal(Path.GetFullPath(Path.GetDirectoryName(path)!), FileJumpNavigationPlan.GetParentFolder(path));
        Assert.Equal("report.txt", FileJumpNavigationPlan.GetFileName(path));
    }

    [Fact]
    public void GetParentFolder_RejectsInvalidPath()
    {
        Assert.Null(FileJumpNavigationPlan.GetParentFolder(string.Empty));
        Assert.Null(FileJumpNavigationPlan.GetParentFolder("   "));
        Assert.Null(FileJumpNavigationPlan.GetFileName(string.Empty));
    }

    [Fact]
    public void Verify_ConfirmsMatchingReadableAddressText()
    {
        const string folder = @"C:\Users\me\quickjump-parent";
        var address = new ControlTextRead(true, @"C:\Users\me\quickjump-parent\");

        Assert.Equal(NavigationVerification.Confirmed, FileJumpNavigationPlan.Verify(folder, address, null));
    }

    [Fact]
    public void Verify_ReportsNotNavigatedForReadableMismatch()
    {
        const string folder = @"C:\Users\me\quickjump-parent";
        var address = new ControlTextRead(true, @"C:\Users\me\other\");

        Assert.Equal(NavigationVerification.NotNavigated, FileJumpNavigationPlan.Verify(folder, address, "Open - other"));
    }

    [Fact]
    public void Verify_UnreadableTextIsUnverifiableAndDoesNotBecomeProofOfFailure()
    {
        const string folder = @"C:\Users\me\quickjump-parent";
        var address = new ControlTextRead(false, null);

        Assert.Equal(NavigationVerification.Unverifiable, FileJumpNavigationPlan.Verify(folder, address, null));
        Assert.Equal(NavigationVerification.Unverifiable, FileJumpNavigationPlan.Verify(folder, address, "Open - quickjump-parent"));
    }

    [Fact]
    public void Verify_UsesWindowTitleOnlyAsAuxiliaryEvidence()
    {
        const string folder = @"C:\Users\me\quickjump-parent";
        var emptyReadableAddress = new ControlTextRead(true, string.Empty);

        Assert.Equal(NavigationVerification.Confirmed, FileJumpNavigationPlan.Verify(folder, emptyReadableAddress, "Open - quickjump-parent"));
        Assert.Equal(NavigationVerification.NotNavigated, FileJumpNavigationPlan.Verify(folder, emptyReadableAddress, null));
    }

    [Fact]
    public void UnverifiableFallback_StillFillsFileNameWithoutSecondEnter()
    {
        Assert.Equal(NavigationFallback.FillFileName, FileJumpNavigationPlan.DefaultUnverifiableFallback);
    }

    [Fact]
    public void VerificationIsBounded()
    {
        Assert.True(FileJumpNavigationPlan.ShouldVerify(0));
        Assert.True(FileJumpNavigationPlan.ShouldVerify(FileJumpNavigationPlan.DefaultVerificationAttempts - 1));
        Assert.False(FileJumpNavigationPlan.ShouldVerify(FileJumpNavigationPlan.DefaultVerificationAttempts));
        Assert.False(FileJumpNavigationPlan.ShouldVerify(-1));
    }
}
