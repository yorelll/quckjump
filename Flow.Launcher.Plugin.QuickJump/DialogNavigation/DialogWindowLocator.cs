using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Locates and waits for a structurally verified Windows common file dialog without injecting keyboard input too early.</summary>
internal static class DialogWindowLocator
{
    private const int IdOk = 1;

    public static HWND CaptureVisibleFileDialog()
    {
        var candidates = GetEligibleVisibleFileDialogs();
        return candidates.Count == 1 ? candidates[0] : HWND.Null;
    }

    /// <summary>
    /// Fallback capture used when structural qualification fails. It never
    /// trusts the current foreground window, which may already be Flow itself.
    /// Instead it enumerates visible standard dialogs, excludes Flow's process,
    /// and attributes one only when a single unrelated process owns them all.
    /// </summary>
    public static unsafe FallbackDialogCapture CaptureFallbackStandardDialog()
    {
        uint currentProcessId = (uint)Environment.ProcessId;
        var candidates = GetVisibleStandardDialogCandidates();
        var selected = FallbackDialogSelection.Select(candidates, currentProcessId);
        return selected is StandardDialogCandidate candidate
            ? new FallbackDialogCapture(candidate.Handle, candidate.ProcessId)
            : default;
    }

    private static List<StandardDialogCandidate> GetVisibleStandardDialogCandidates()
    {
        var candidates = new List<StandardDialogCandidate>();
        PInvoke.EnumWindows(EnumProc, 0);
        return candidates;

        unsafe BOOL EnumProc(HWND window, LPARAM _)
        {
            if (!PInvoke.IsWindowVisible(window) ||
                !string.Equals(Utils.GetClassName(window), "#32770", StringComparison.Ordinal))
            {
                return true;
            }

            uint processId;
            _ = PInvoke.GetWindowThreadProcessId(window, &processId);
            candidates.Add(new StandardDialogCandidate(window, processId));
            return true;
        }
    }

    /// <summary>
    /// A generic <c>#32770</c> class is not sufficient: message boxes,
    /// properties windows, and unrelated application dialogs use it too. The
    /// candidate must expose a constrained modern address-bar path or a
    /// verified legacy file-name structure before it may be activated.
    /// </summary>
    public static bool IsUsableFileDialog(HWND window) =>
        GetFileDialogKind(window) != FileDialogKind.None;

    public static FileDialogKind GetFileDialogKind(HWND window) =>
        window != HWND.Null &&
        PInvoke.IsWindow(window) &&
        PInvoke.IsWindowVisible(window)
            ? InspectStructure(window).Classify()
            : FileDialogKind.None;

    public static FileDialogPurpose GetFileDialogPurpose(HWND window) =>
        window != HWND.Null &&
        PInvoke.IsWindow(window) &&
        PInvoke.IsWindowVisible(window)
            ? InspectStructure(window).ClassifyPurpose()
            : FileDialogPurpose.Unknown;

    /// <summary>Returns the qualified filename combo edit, never the address-band edit.</summary>
    public static HWND FindFileNameEdit(HWND dialogHandle)
    {
        HWND found = HWND.Null;
        PInvoke.EnumChildWindows(dialogHandle, EnumProc, 0);
        return found;

        BOOL EnumProc(HWND window, LPARAM _)
        {
            if (found != HWND.Null ||
                !string.Equals(Utils.GetClassName(window), "ComboBoxEx32", StringComparison.Ordinal) ||
                !HasQualifiedComboBoxEditPath(window) ||
                HasAddressBandAncestor(window))
            {
                return true;
            }

            found = FindEditUnderComboEx(window);
            return found == HWND.Null;
        }
    }

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

    private static List<HWND> GetEligibleVisibleFileDialogs()
    {
        var candidates = new List<HWND>();
        PInvoke.EnumWindows(EnumProc, 0);
        return candidates;

        BOOL EnumProc(HWND window, LPARAM _)
        {
            if (IsUsableFileDialog(window))
            {
                candidates.Add(window);
            }
            return true;
        }
    }

    private static FileDialogStructure InspectStructure(HWND dialogHandle)
    {
        string? windowClass = Utils.GetClassName(dialogHandle);
        if (!AddressEditDiscovery.IsEligibleDialogClass(windowClass))
        {
            return new FileDialogStructure(windowClass, 0, 0, false);
        }

        int modernPaths = 0;
        int fileNamePaths = 0;
        PInvoke.EnumChildWindows(dialogHandle, EnumProc, 0);

        bool hasStandardConfirmButton = PInvoke.GetDlgItem(dialogHandle, IdOk) != HWND.Null;
        return new FileDialogStructure(windowClass, modernPaths, fileNamePaths, hasStandardConfirmButton);

        BOOL EnumProc(HWND window, LPARAM _)
        {
            if (!string.Equals(Utils.GetClassName(window), "ComboBoxEx32", StringComparison.Ordinal))
            {
                return true;
            }

            if (!HasQualifiedComboBoxEditPath(window))
            {
                return true;
            }

            if (HasAddressBandAncestor(window))
            {
                modernPaths++;
            }
            else
            {
                fileNamePaths++;
            }
            return true;
        }
    }

    private static bool HasQualifiedComboBoxEditPath(HWND comboExHandle) =>
        FindEditUnderComboEx(comboExHandle) != HWND.Null &&
        AddressEditDiscovery.IsAddressEditPath(["ComboBoxEx32", "ComboBox", "Edit"]);

    private static HWND FindEditUnderComboEx(HWND comboExHandle)
    {
        var comboHandle = PInvoke.FindWindowEx(comboExHandle, HWND.Null, "ComboBox", null);
        return comboHandle == HWND.Null
            ? HWND.Null
            : PInvoke.FindWindowEx(comboHandle, HWND.Null, "Edit", null);
    }

    private static bool HasAddressBandAncestor(HWND control)
    {
        var current = PInvoke.GetParent(control);
        for (int depth = 0; depth < 12 && current != HWND.Null; depth++)
        {
            if (string.Equals(Utils.GetClassName(current), "Address Band Root", StringComparison.Ordinal))
            {
                return true;
            }
            current = PInvoke.GetParent(current);
        }
        return false;
    }
}
