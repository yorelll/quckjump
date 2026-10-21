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
    public void IsNavigationVerified_RequiresObservedEvidenceOfTheExpectedFolder()
    {
        const string folder = @"C:\Users\me\quickjump-parent";

        Assert.True(FileJumpNavigationPlan.IsNavigationVerified(folder, @"C:\Users\me\quickjump-parent\", null));
        Assert.True(FileJumpNavigationPlan.IsNavigationVerified(folder, null, "Open - quickjump-parent"));
        Assert.False(FileJumpNavigationPlan.IsNavigationVerified(folder, @"C:\Users\me\other\", "Open - other"));
        Assert.False(FileJumpNavigationPlan.IsNavigationVerified(string.Empty, @"C:\Users\me\quickjump-parent", null));
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
