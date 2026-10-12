using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class FileDialogStructureTests
{
    [Theory]
    [InlineData("#32770", 1, 0, false, FileDialogKind.Modern)]
    [InlineData("#32770", 0, 1, true, FileDialogKind.Legacy)]
    [InlineData("#32770", 0, 1, false, FileDialogKind.None)]
    [InlineData("#32770", 0, 0, true, FileDialogKind.None)]
    [InlineData("#32770", 2, 0, true, FileDialogKind.None)]
    [InlineData("#32770", 0, 2, true, FileDialogKind.None)]
    [InlineData("ApplicationDialog", 1, 0, true, FileDialogKind.None)]
    public void StructureClassification_RequiresConstrainedFileDialogEvidence(
        string windowClass,
        int modernAddressPathCount,
        int legacyFileNamePathCount,
        bool hasStandardConfirmButton,
        FileDialogKind expected)
    {
        var structure = new FileDialogStructure(
            windowClass,
            modernAddressPathCount,
            legacyFileNamePathCount,
            hasStandardConfirmButton);

        Assert.Equal(expected, structure.Classify());
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
