using System;
using System.Runtime.InteropServices;
using FlaUI.UIA3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Flow.Launcher.Plugin.QuickJump.Diagnostics;

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
            QuickJumpDiagnostics.Warn($"qualified-focus abort=unqualified targetThread={targetThread} currentThread={currentThread}");
            return false;
        }

        bool queueCreated = EnsureCurrentThreadMessageQueue();
        QuickJumpDiagnostics.Info($"qualified-focus queueCreated={queueCreated} targetThread={targetThread} targetProcess={targetProcessId} currentThread={currentThread}");

        bool attached = false;
        bool attachVerified = false;
        try
        {
            attached = targetThread == currentThread || PInvoke.AttachThreadInput(currentThread, targetThread, true);
            NativeEvidenceCollector.LogImmediateLastError("AttachThreadInput");
            QuickJumpDiagnostics.Info($"qualified-focus attachSucceeded={attached}");
            if (attached)
            {
                var previousFocus = PInvoke.SetFocus(addressEdit);
                NativeEvidenceCollector.LogImmediateLastError("SetFocus");
                attachVerified = GetDialogFocusedWindow(dialogHandle) == addressEdit;
                QuickJumpDiagnostics.Info($"qualified-focus setFocusPrevious={previousFocus} attachVerified={attachVerified}");
                if (attachVerified)
                {
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            QuickJumpDiagnostics.Exception("qualified-focus attach", exception);
        }
        finally
        {
            if (attached && targetThread != currentThread)
            {
                bool detached = PInvoke.AttachThreadInput(currentThread, targetThread, false);
                NativeEvidenceCollector.LogImmediateLastError("AttachThreadInputDetach");
                QuickJumpDiagnostics.Info($"qualified-focus detachSucceeded={detached}");
            }
        }

        bool uiaVerified = TryUiaFocus(dialogHandle, addressEdit);
        var path = QualifiedAddressFocusPlan.Select(
            addressQualified: true,
            attachSucceeded: attached,
            attachFocusVerified: attachVerified,
            uiaFocusVerified: uiaVerified);
        QuickJumpDiagnostics.Info($"qualified-focus finalPath={path}");
        return path is QualifiedAddressFocusPath.AttachSetFocus or QualifiedAddressFocusPath.UiaFocusFallback;
    }

    private static unsafe bool EnsureCurrentThreadMessageQueue()
    {
        MSG message = default;
        // PM_NOREMOVE creates this worker thread's queue without consuming an
        // input message, satisfying AttachThreadInput's documented precondition.
        _ = PInvoke.PeekMessage(&message, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        NativeEvidenceCollector.LogImmediateLastError("PeekMessage");
        return true;
    }

    private static bool TryUiaFocus(HWND dialogHandle, HWND addressEdit)
    {
        try
        {
            using var automation = new UIA3Automation();
            var element = automation.FromHandle(addressEdit);
            element.Focus();
            bool verified = GetDialogFocusedWindow(dialogHandle) == addressEdit;
            QuickJumpDiagnostics.Info($"qualified-focus uiaFocusVerified={verified}");
            return verified;
        }
        catch (Exception exception)
        {
            QuickJumpDiagnostics.Exception("qualified-focus uia", exception);
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
