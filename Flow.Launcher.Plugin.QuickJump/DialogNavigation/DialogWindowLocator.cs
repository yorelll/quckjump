using System;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Locates and waits for a Windows common file dialog without injecting keyboard input too early.</summary>
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
        window != HWND.Null && PInvoke.IsWindow(window) && PInvoke.IsWindowVisible(window) &&
        AddressEditDiscovery.IsEligibleDialogClass(Utils.GetClassName(window));

    /// <summary>
    /// Wait until the target dialog—not Flow—is the foreground window. A merely
    /// visible cached HWND is deliberately insufficient because H.InputSimulator
    /// sends global keys. Once Flow has hidden and natural restoration has not
    /// occurred, a single controlled foreground activation is attempted and
    /// verified before input is allowed.
    /// </summary>
    public static HWND WaitForReadyTarget(
        HWND captured,
        DialogTargetRetryPlan? retryPlan = null,
        Func<HWND>? foregroundWindow = null,
        Func<HWND, bool>? isUsable = null,
        Func<HWND, bool>? activateTarget = null,
        Func<bool>? isFlowForeground = null,
        Func<HWND>? enumerateTarget = null,
        Action<int>? wait = null)
    {
        var plan = retryPlan ?? DialogTargetRetryPlan.Default;
        foregroundWindow ??= PInvoke.GetForegroundWindow;
        isUsable ??= IsUsableFileDialog;
        activateTarget ??= window => PInvoke.SetForegroundWindow(window);
        isFlowForeground ??= () => false;
        enumerateTarget ??= CaptureVisibleFileDialog;
        wait ??= milliseconds => Thread.Sleep(milliseconds);

        HWND target = captured;
        bool activationAttempted = false;
        for (int attempt = 0; plan.ShouldTry(attempt); attempt++)
        {
            if (!isUsable(target))
            {
                var foreground = foregroundWindow();
                var source = DialogTargetSelection.Select(isUsable(captured), isUsable(foreground));
                target = source switch
                {
                    DialogTargetSource.Captured => captured,
                    DialogTargetSource.Foreground => foreground,
                    _ => enumerateTarget(),
                };
            }

            bool targetIsUsable = isUsable(target);
            bool targetIsForeground = targetIsUsable && foregroundWindow() == target;
            var decision = DialogReadinessPlan.Decide(
                targetIsUsable,
                targetIsForeground,
                isFlowForeground(),
                mayActivateTarget: !activationAttempted,
                retriesExhausted: attempt + 1 >= plan.Attempts);
            if (decision == DialogReadinessDecision.Ready)
            {
                return target;
            }
            if (decision == DialogReadinessDecision.ActivateTarget)
            {
                activationAttempted = true;
                _ = activateTarget(target);
                // Verify foreground status on a future bounded retry; do not
                // inject keys merely because SetForegroundWindow returned true.
            }
            if (decision == DialogReadinessDecision.Fail)
            {
                break;
            }
            if (attempt + 1 < plan.Attempts)
            {
                wait(plan.DelayMilliseconds);
            }
        }
        return HWND.Null;
    }
}
