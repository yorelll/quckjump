using System;
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
