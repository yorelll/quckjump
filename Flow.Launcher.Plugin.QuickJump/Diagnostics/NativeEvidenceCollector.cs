using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Collects bounded, non-content native-dialog evidence for diagnostic candidates.</summary>
internal static class NativeEvidenceCollector
{
    private const int EditStyleReadOnly = 0x0800;

    public static void LogDialogContext(HWND dialogHandle, HWND addressEdit)
    {
        unsafe
        {
            uint targetProcessId;
            uint targetThreadId = PInvoke.GetWindowThreadProcessId(dialogHandle, &targetProcessId);
            var target = DescribeProcess(targetProcessId);
            var current = DescribeCurrentProcess();
            string control = GetAddressControlEvidence(addressEdit);

            // Desktop/token queries are intentionally not enabled in this
            // diagnostic build: they require additional process-token/window-
            // station rights and could themselves change the evidence context.
            QuickJumpDiagnostics.Info(
                $"native-evidence targetThread={targetThreadId} target={target} current={current} " +
                "targetDesktop=unavailable(token-desktop-query-disabled) " +
                "integrity=unavailable(token-query-disabled) " + control);
        }
    }

    public static void LogImmediateLastError(string operation)
    {
        // This method must be called immediately after the native operation;
        // no other P/Invoke is allowed between it and the observed call.
        var nativeError = PInvoke.GetLastError();
        int marshaledError = Marshal.GetLastPInvokeError();
        QuickJumpDiagnostics.Info($"native-evidence {operation} getLastError={nativeError} marshalLastPInvokeError={marshaledError}");
    }

    private static string DescribeCurrentProcess()
    {
        using var process = Process.GetCurrentProcess();
        return $"pid={process.Id};name={process.ProcessName};session={process.SessionId}";
    }

    private static string DescribeProcess(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return $"pid={process.Id};name={process.ProcessName};session={process.SessionId}";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"pid={processId};unavailable={exception.GetType().Name}";
        }
    }

    private static string GetAddressControlEvidence(HWND editHandle)
    {
        bool enabled = PInvoke.IsWindowEnabled(editHandle);
        int style = PInvoke.GetWindowLong(editHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        bool readOnly = (style & EditStyleReadOnly) != 0;
        bool visible = (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
        return $"addressEnabled={enabled} addressVisible={visible} addressReadOnly={readOnly} addressStyle=0x{style:X8}";
    }
}
