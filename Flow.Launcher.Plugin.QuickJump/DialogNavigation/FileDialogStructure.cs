using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Describes only the constrained control structures that identify a Windows common file dialog.</summary>
internal enum FileDialogKind
{
    None,
    Modern,
    Legacy,
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
}

/// <summary>Selects the appropriate path-fill strategy after a dialog has been structurally verified.</summary>
internal enum FileDialogJumpRoute
{
    None,
    ModernFile,
    ModernFolder,
    LegacyFile,
    LegacyFolder,
}

/// <summary>Pure routing rule that keeps a legacy filename control out of the modern address-bar path.</summary>
internal static class FileDialogJumpPlan
{
    public static FileDialogJumpRoute Select(FileDialogKind dialogKind, CustomEntries.CustomEntryType entryType) =>
        (dialogKind, entryType) switch
        {
            (FileDialogKind.Modern, CustomEntries.CustomEntryType.File) => FileDialogJumpRoute.ModernFile,
            (FileDialogKind.Modern, CustomEntries.CustomEntryType.Folder) => FileDialogJumpRoute.ModernFolder,
            (FileDialogKind.Legacy, CustomEntries.CustomEntryType.File) => FileDialogJumpRoute.LegacyFile,
            (FileDialogKind.Legacy, CustomEntries.CustomEntryType.Folder) => FileDialogJumpRoute.LegacyFolder,
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
