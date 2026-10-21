using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Describes only the constrained control structures that identify a Windows common file dialog.</summary>
internal enum FileDialogKind
{
    None,
    Modern,
    Legacy,
}

/// <summary>Distinguishes a native file picker from a folder picker by standard control structure.</summary>
internal enum FileDialogPurpose
{
    Unknown,
    FilePicker,
    FolderPicker,
}

/// <summary>
/// Pure classification data used to prevent a generic <c>#32770</c> dialog from
/// being mistaken for a file picker. A class name is only a preliminary filter;
/// a verified address-bar or legacy file-name structure is also required.
/// </summary>
internal sealed record FileDialogStructure(
    string? WindowClass,
    int ModernAddressPathCount,
    int LegacyFileNamePathCount,
    bool HasStandardConfirmButton)
{
    public FileDialogKind Classify()
    {
        if (!AddressEditDiscovery.IsEligibleDialogClass(WindowClass))
        {
            return FileDialogKind.None;
        }

        // More than one address-bar candidate is ambiguous. Do not select a
        // window that could receive keyboard input through the wrong control.
        if (ModernAddressPathCount == 1)
        {
            return FileDialogKind.Modern;
        }
        if (ModernAddressPathCount > 1)
        {
            return FileDialogKind.None;
        }

        // A legacy file dialog has exactly one standard file-name combo path
        // and the standard IDOK confirmation button. A generic combo/edit
        // dialog does not satisfy this evidence.
        return LegacyFileNamePathCount == 1 && HasStandardConfirmButton
            ? FileDialogKind.Legacy
            : FileDialogKind.None;
    }

    /// <summary>
    /// Modern file pickers expose a filename combo/edit outside Address Band
    /// Root; folder pickers expose the address band only. The distinction is
    /// used to avoid writing a filename into a folder picker.
    /// </summary>
    public FileDialogPurpose ClassifyPurpose()
    {
        if (Classify() != FileDialogKind.Modern)
        {
            return FileDialogPurpose.Unknown;
        }

        return LegacyFileNamePathCount >= 1
            ? FileDialogPurpose.FilePicker
            : FileDialogPurpose.FolderPicker;
    }
}

/// <summary>Selects the appropriate path-fill strategy after a dialog has been structurally verified.</summary>
internal enum FileDialogJumpRoute
{
    None,
    ModernFolder,
    ModernFilePickerFile,
    ModernFolderPickerFile,
    LegacyFile,
    LegacyFolder,
}

/// <summary>Pure routing rule that keeps filename controls out of the address-bar path and vice versa.</summary>
internal static class FileDialogJumpPlan
{
    public static FileDialogJumpRoute Select(FileDialogKind dialogKind, FileDialogPurpose purpose, CustomEntryType entryType) =>
        (dialogKind, purpose, entryType) switch
        {
            (FileDialogKind.Modern, FileDialogPurpose.FolderPicker, CustomEntryType.Folder) => FileDialogJumpRoute.ModernFolder,
            (FileDialogKind.Modern, FileDialogPurpose.FilePicker, CustomEntryType.Folder) => FileDialogJumpRoute.ModernFolder,
            (FileDialogKind.Modern, FileDialogPurpose.FilePicker, CustomEntryType.File) => FileDialogJumpRoute.ModernFilePickerFile,
            (FileDialogKind.Modern, FileDialogPurpose.FolderPicker, CustomEntryType.File) => FileDialogJumpRoute.ModernFolderPickerFile,
            (FileDialogKind.Legacy, _, CustomEntryType.File) => FileDialogJumpRoute.LegacyFile,
            (FileDialogKind.Legacy, _, CustomEntryType.Folder) => FileDialogJumpRoute.LegacyFolder,
            _ => FileDialogJumpRoute.None,
        };
}

/// <summary>Chooses a candidate only when exactly one structurally verified file dialog exists.</summary>
internal static class FileDialogCandidateSelection
{
    /// <returns>The single eligible index, or -1 when none or more than one candidate is eligible.</returns>
    public static int SelectSingleEligible(IReadOnlyList<FileDialogKind> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        int selected = -1;
        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] == FileDialogKind.None)
            {
                continue;
            }

            if (selected >= 0)
            {
                return -1;
            }

            selected = index;
        }

        return selected;
    }
}
