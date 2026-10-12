using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>在不依赖当前前台窗口的情况下定位 Windows 通用文件对话框。</summary>
internal static class DialogWindowLocator
{
    public static HWND CaptureVisibleFileDialog()
    {
        HWND captured = HWND.Null;
        PInvoke.EnumWindows(EnumProc, 0);
        return captured;

        BOOL EnumProc(HWND window, LPARAM _)
        {
            if (IsUsableFileDialog(window))
            {
                captured = window;
                return false;
            }
            return true;
        }
    }

    public static bool IsUsableFileDialog(HWND window) =>
        window != HWND.Null &&
        PInvoke.IsWindow(window) &&
        PInvoke.IsWindowVisible(window) &&
        AddressEditDiscovery.IsEligibleDialogClass(Utils.GetClassName(window));

    /// <summary>
    /// 在 Flow 隐藏后优先返回先前缓存的文件对话框；只有缓存目标不可用时才使用
    /// 当前前台窗口或重新枚举到的文件对话框。
    /// </summary>
    public static HWND WaitForTarget(HWND captured, DialogTargetRetryPlan? retryPlan = null)
    {
        var plan = retryPlan ?? DialogTargetRetryPlan.Default;
        for (int attempt = 0; plan.ShouldTry(attempt); attempt++)
        {
            var foreground = PInvoke.GetForegroundWindow();
            var source = DialogTargetSelection.Select(
                IsUsableFileDialog(captured),
                IsUsableFileDialog(foreground));
            if (source == DialogTargetSource.Captured)
            {
                return captured;
            }
            if (source == DialogTargetSource.Foreground)
            {
                return foreground;
            }

            var enumerated = CaptureVisibleFileDialog();
            if (enumerated != HWND.Null)
            {
                return enumerated;
            }

            if (attempt + 1 < plan.Attempts)
            {
                Thread.Sleep(plan.DelayMilliseconds);
            }
        }
        return HWND.Null;
    }
}
