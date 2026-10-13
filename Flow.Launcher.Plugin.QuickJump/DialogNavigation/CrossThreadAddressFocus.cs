using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Flow.Launcher.Plugin.QuickJump.Diagnostics;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>
/// Uses the documented AttachThreadInput + SetFocus sequence only for an
/// already structurally verified address edit in the foreground common dialog.
/// </summary>
internal static class CrossThreadAddressFocus
{
    public static unsafe bool TryFocus(HWND dialogHandle, HWND addressEdit)
    {
        uint processId;
        uint targetThread = PInvoke.GetWindowThreadProcessId(dialogHandle, &processId);
        uint currentThread = PInvoke.GetCurrentThreadId();
        bool qualified = dialogHandle != HWND.Null && addressEdit != HWND.Null && targetThread != 0 && currentThread != 0;
        if (!qualified)
        {
            QuickJumpDiagnostics.Warn($"cross-thread-focus abort=unqualified targetThread={targetThread} currentThread={currentThread}");
            return false;
        }

        bool attached = targetThread == currentThread || PInvoke.AttachThreadInput(currentThread, targetThread, true);
        QuickJumpDiagnostics.Info($"cross-thread-focus targetThread={targetThread} currentThread={currentThread} attachSucceeded={attached}");
        if (!attached)
        {
            return false;
        }

        try
        {
            var previous = PInvoke.SetFocus(addressEdit);
            // SetFocus returns the previous focus handle, which may legitimately
            // be null. Verify against the target GUI thread instead.
            var verified = GetDialogFocusedWindow(dialogHandle) == addressEdit;
            var decision = CrossThreadFocusPlan.Decide(
                addressEditQualified: true,
                attachSucceeded: true,
                setFocusSucceeded: verified,
                verifiedFocus: verified);
            QuickJumpDiagnostics.Info($"cross-thread-focus setFocusPrevious={previous} verified={verified} decision={decision}");
            return decision == CrossThreadFocusDecision.WriteText;
        }
        catch (Exception exception)
        {
            QuickJumpDiagnostics.Exception("cross-thread-focus", exception);
            return false;
        }
        finally
        {
            if (targetThread != currentThread)
            {
                bool detached = PInvoke.AttachThreadInput(currentThread, targetThread, false);
                QuickJumpDiagnostics.Info($"cross-thread-focus detachSucceeded={detached}");
            }
        }
    }

    private static unsafe HWND GetDialogFocusedWindow(HWND dialogHandle)
    {
        uint processId;
        uint threadId = PInvoke.GetWindowThreadProcessId(dialogHandle, &processId);
        GUITHREADINFO info = new() { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(threadId, ref info) ? info.hwndFocus : HWND.Null;
    }
}
