using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class FileDialogStructureTests
{
    [Fact]
    public void StructureClassification_RequiresConstrainedFileDialogEvidence()
    {
        Assert.Equal(FileDialogKind.Modern, Classify("#32770", modernPaths: 1, fileNamePaths: 0, hasStandardConfirmButton: false));
        Assert.Equal(FileDialogKind.Legacy, Classify("#32770", modernPaths: 0, fileNamePaths: 1, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, fileNamePaths: 1, hasStandardConfirmButton: false));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, fileNamePaths: 0, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 2, fileNamePaths: 0, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("#32770", modernPaths: 0, fileNamePaths: 2, hasStandardConfirmButton: true));
        Assert.Equal(FileDialogKind.None, Classify("ApplicationDialog", modernPaths: 1, fileNamePaths: 0, hasStandardConfirmButton: true));
    }

    [Fact]
    public void PurposeClassification_DistinguishesFilenameControlFromFolderOnlyDialog()
    {
        Assert.Equal(
            FileDialogPurpose.FilePicker,
            new FileDialogStructure("#32770", 1, 1, true).ClassifyPurpose());
        Assert.Equal(
            FileDialogPurpose.FolderPicker,
            new FileDialogStructure("#32770", 1, 0, true).ClassifyPurpose());
        Assert.Equal(
            FileDialogPurpose.Unknown,
            new FileDialogStructure("#32770", 0, 1, true).ClassifyPurpose());
    }

    [Fact]
    public void JumpPlan_FileEntryInFolderPickerIsRejectedInsteadOfInjectingInput()
    {
        Assert.Equal(
            FileDialogJumpRoute.ModernFolderPickerFile,
            FileDialogJumpPlan.Select(FileDialogKind.Modern, FileDialogPurpose.FolderPicker, CustomEntryType.File));
    }

    [Fact]
    public void JumpPlan_FileEntryInFilePickerNavigatesToParentThenFillsName()
    {
        Assert.Equal(
            FileDialogJumpRoute.ModernFilePickerFile,
            FileDialogJumpPlan.Select(FileDialogKind.Modern, FileDialogPurpose.FilePicker, CustomEntryType.File));
    }

    [Fact]
    public void JumpPlan_FolderEntryStillNavigatesInBothModernPickerKinds()
    {
        Assert.Equal(
            FileDialogJumpRoute.ModernFolder,
            FileDialogJumpPlan.Select(FileDialogKind.Modern, FileDialogPurpose.FilePicker, CustomEntryType.Folder));
        Assert.Equal(
            FileDialogJumpRoute.ModernFolder,
            FileDialogJumpPlan.Select(FileDialogKind.Modern, FileDialogPurpose.FolderPicker, CustomEntryType.Folder));
    }

    [Fact]
    public void JumpPlan_LegacyFileUsesSingleConfirmAndLegacyFolderUsesTwoConfirms()
    {
        Assert.Equal(
            FileDialogJumpRoute.LegacyFile,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, FileDialogPurpose.Unknown, CustomEntryType.File));
        Assert.Equal(
            FileDialogJumpRoute.LegacyFolder,
            FileDialogJumpPlan.Select(FileDialogKind.Legacy, FileDialogPurpose.Unknown, CustomEntryType.Folder));
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

    private static FileDialogKind Classify(string windowClass, int modernPaths, int fileNamePaths, bool hasStandardConfirmButton) =>
        new FileDialogStructure(windowClass, modernPaths, fileNamePaths, hasStandardConfirmButton).Classify();
}
