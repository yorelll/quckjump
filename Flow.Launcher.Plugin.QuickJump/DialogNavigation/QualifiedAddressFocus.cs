using System;
using System.Runtime.InteropServices;
using FlaUI.UIA3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>
/// Focuses only a structurally proven common-dialog address Edit. It first uses
/// the documented input-queue attachment route, then a UIA3 fallback; both are
/// verified through the target dialog's GUI thread before any text is written.
/// </summary>
internal static class QualifiedAddressFocus
{
    public static unsafe bool TryFocus(HWND dialogHandle, HWND addressEdit)
    {
        uint targetProcessId;
        uint targetThread = PInvoke.GetWindowThreadProcessId(dialogHandle, &targetProcessId);
        uint currentThread = PInvoke.GetCurrentThreadId();
        if (dialogHandle == HWND.Null || addressEdit == HWND.Null || targetThread == 0 || currentThread == 0)
        {
            return false;
        }

        _ = EnsureCurrentThreadMessageQueue();
        bool attached = false;
        bool attachVerified = false;
        try
        {
            attached = targetThread == currentThread || PInvoke.AttachThreadInput(currentThread, targetThread, true);
            if (attached)
            {
                _ = PInvoke.SetFocus(addressEdit);
                attachVerified = GetDialogFocusedWindow(dialogHandle) == addressEdit;
                if (attachVerified)
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Fall through to the verified UIA path only.
        }
        finally
        {
            if (attached && targetThread != currentThread)
            {
                _ = PInvoke.AttachThreadInput(currentThread, targetThread, false);
            }
        }

        bool uiaVerified = TryUiaFocus(dialogHandle, addressEdit);
        var path = QualifiedAddressFocusPlan.Select(
            addressQualified: true,
            attachSucceeded: attached,
            attachFocusVerified: attachVerified,
            uiaFocusVerified: uiaVerified);
        return path is QualifiedAddressFocusPath.AttachSetFocus or QualifiedAddressFocusPath.UiaFocusFallback;
    }

    private static unsafe bool EnsureCurrentThreadMessageQueue()
    {
        MSG message = default;
        // PM_NOREMOVE creates this worker thread's queue without consuming an
        // input message, satisfying AttachThreadInput's documented precondition.
        _ = PInvoke.PeekMessage(&message, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        return true;
    }

    private static bool TryUiaFocus(HWND dialogHandle, HWND addressEdit)
    {
        try
        {
            using var automation = new UIA3Automation();
            var element = automation.FromHandle(addressEdit);
            element.Focus();
            return GetDialogFocusedWindow(dialogHandle) == addressEdit;
        }
        catch (Exception)
        {
            return false;
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
