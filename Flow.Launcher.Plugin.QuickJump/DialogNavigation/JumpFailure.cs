using System;
using System.Collections.Generic;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Original foreground dialog captured before Flow took focus, with its process.</summary>
internal readonly record struct FallbackDialogCapture(HWND Handle, uint ProcessId)
{
    public bool Found => Handle != HWND.Null;
}

/// <summary>User-visible jump outcomes. Every value must map to a localized dialog message.</summary>
internal enum JumpFailure
{
    ElevatedTarget,
    DialogNotFound,
    FolderPickerWithFile,
    ParentFolderMissing,
    NavigationFailed,
    FileNameNotFound,
    AddressBarFocusFailed,
    DialogStructureChanged,
    LegacyFileNameFieldMissing,
}

/// <summary>Minimal identity of a visible standard (#32770) dialog candidate.</summary>
internal readonly record struct StandardDialogCandidate(HWND Handle, uint ProcessId);

/// <summary>
/// Pure selection policy for the fallback capture. Flow's own windows are
/// excluded, and an ambiguous set of candidates yields no attribution so an
/// unrelated application dialog is never blamed on the user's target.
/// </summary>
internal static class FallbackDialogSelection
{
    public static StandardDialogCandidate? Select(
        IReadOnlyList<StandardDialogCandidate> candidates,
        uint currentProcessId)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        StandardDialogCandidate? selected = null;
        uint selectedProcess = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.Handle == HWND.Null || candidate.ProcessId == 0 || candidate.ProcessId == currentProcessId)
            {
                continue;
            }

            if (selectedProcess != 0 && selectedProcess != candidate.ProcessId)
            {
                // More than one unrelated application owns a standard dialog:
                // attribution is ambiguous, so report a generic failure instead.
                return null;
            }

            selectedProcess = candidate.ProcessId;
            selected ??= candidate;
        }

        return selected;
    }

    /// <summary>Selects one unambiguous process id, as used by the UIPI fallback.</summary>
    public static uint? SelectProcessId(IReadOnlyList<StandardDialogCandidate> candidates, uint currentProcessId) =>
        Select(candidates, currentProcessId)?.ProcessId;
}

/// <summary>Pure routing decisions for failures that are not tied to a verified dialog.</summary>
internal static class JumpFailurePlan
{
    /// <summary>
    /// When no verified dialog was captured, the fallback capture still tells
    /// us whether the user's elevated target blocked the jump.
    /// </summary>
    public static JumpFailure DecideNoDialogFailure(bool fallbackFound, bool fallbackElevated) =>
        fallbackFound && fallbackElevated ? JumpFailure.ElevatedTarget : JumpFailure.DialogNotFound;

    public static JumpFailure DecideNoDialogFailure(bool fallbackFound, uint fallbackProcessId, uint currentProcessId, Func<uint, bool> isElevated)
    {
        ArgumentNullException.ThrowIfNull(isElevated);
        bool elevated = fallbackFound &&
            fallbackProcessId != 0 &&
            fallbackProcessId != currentProcessId &&
            isElevated(fallbackProcessId);
        return DecideNoDialogFailure(fallbackFound, elevated);
    }
}
