using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class FileDialogStructureTests
{
    [Fact]
    public void StructureClassification_RequiresConstrainedFileDialogEvidence()
    {
        Assert.Equal(FileDialogKind.Modern, Classify("#32770", modernPaths: 1, legacyPaths: 0, hasStandardConfirmButton: false));
        Assert.Equal(FileDialogKind.Legacy, Classify("#32770", modernPaths: 0, legacyPaths: 1, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, legacyPaths: 1, hasStandardConfirmButton: false));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, legacyPaths: 0, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 2, legacyPaths: 0, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, legacyPaths: 2, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("ApplicationDialog", modernPaths: 1, legacyPaths: 0, hasStandardConfirmButton: true));
    }

    private static FileDialogKind Classify(string windowClass, int modernPaths, int legacyPaths, bool hasStandardConfirmButton) =>
        new FileDialogStructure(windowClass, modernPaths, legacyPaths, hasStandardConfirmButton).Classify();

    [Fact]
    public void JumpPlan_LegacyFolderUsesLegacyRouteWithTwoConfirmStrategy()
    {
        Assert.Equal(
            FileDialogJumpRoute.LegacyFolder,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, CustomEntryType.Folder));
    }

    [Fact]
    public void JumpPlan_LegacyFileUsesLegacyRouteWithSingleConfirmStrategy()
    {
        Assert.Equal(
            FileDialogJumpRoute.LegacyFile,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, CustomEntryType.File));
    }

    [Fact]
    public void JumpPlan_LegacyStructureCannotEnterModernAddressPath()
    {
        Assert.NotEqual(
            FileDialogJumpRoute.ModernFolder,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, CustomEntryType.Folder));
        Assert.NotEqual(
            FileDialogJumpRoute.ModernFile,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, CustomEntryType.File));
    }

    [Fact]
    public void CandidateSelection_ExcludesGenericStandardDialogAndSelectsOnlyVerifiedFileDialog()
    {
        var candidates = new[]
        {
            new FileDialogStructure("#32770", 0, 0, true).Classify(),
            new FileDialogStructure("#32770", 1, 0, false).Classify(),
        };

        Assert.Equal(1, FileDialogCandidateSelection.SelectSingleEligible(candidates));
    }

    [Fact]
    public void CandidateSelection_WithOnlyUnverifiedStandardDialogs_DoesNotSelectOrActivateAnyWindow()
    {
        var candidates = new[]
        {
            new FileDialogStructure("#32770", 0, 0, true).Classify(),
            new FileDialogStructure("#32770", 0, 1, false).Classify(),
        };

        Assert.Equal(-1, FileDialogCandidateSelection.SelectSingleEligible(candidates));
    }

    [Fact]
    public void CandidateSelection_WithMultipleVerifiedDialogs_RefusesAmbiguousTarget()
    {
        var candidates = new[]
        {
            new FileDialogStructure("#32770", 1, 0, false).Classify(),
            new FileDialogStructure("#32770", 0, 1, true).Classify(),
        };

        Assert.Equal(-1, FileDialogCandidateSelection.SelectSingleEligible(candidates));
    }
}
