using System;
using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Collects bounded thread ownership and GUI-focus evidence without changing focus behavior.</summary>
internal static class GuiThreadOwnershipEvidence
{
    public static unsafe void Log(HWND topDialog, HWND qualifiedAddressEdit, HWND actualFocused, bool addressStructureQualified, bool addressRootMatchesDialog)
    {
        var top = GetThreadProcess(topDialog);
        var address = GetThreadProcess(qualifiedAddressEdit);
        var focused = GetThreadProcess(actualFocused);
        var snapshot = new GuiThreadOwnershipSnapshot(
            TopDialogHandle: topDialog.Value,
            TopDialogThread: top.Thread,
            TopDialogProcess: top.Process,
            AddressEditHandle: qualifiedAddressEdit.Value,
            AddressEditThread: address.Thread,
            AddressEditProcess: address.Process,
            FocusedHandle: actualFocused.Value,
            FocusedThread: focused.Thread,
            FocusedProcess: focused.Process,
            AddressStructureQualified: addressStructureQualified,
            AddressRootMatchesDialog: addressRootMatchesDialog);

        QuickJumpDiagnostics.Info(
            $"thread-evidence interpretation={GuiThreadOwnershipPlan.Interpret(snapshot)} " +
            $"top=handle:{topDialog};thread:{top.Thread};process:{top.Process} " +
            $"address=handle:{qualifiedAddressEdit};thread:{address.Thread};process:{address.Process} " +
            $"focused=handle:{actualFocused};thread:{focused.Thread};process:{focused.Process}");

        foreach (var thread in GuiThreadOwnershipPlan.DistinctThreads(snapshot))
        {
            QuickJumpDiagnostics.Info($"thread-evidence guiThread={thread} hwndFocus={GetGuiFocus(thread)}");
        }
    }

    private static unsafe (uint Thread, uint Process) GetThreadProcess(HWND handle)
    {
        if (handle == HWND.Null)
        {
            return (0, 0);
        }
        uint process;
        uint thread = PInvoke.GetWindowThreadProcessId(handle, &process);
        return (thread, process);
    }

    private static unsafe HWND GetGuiFocus(uint thread)
    {
        if (thread == 0)
        {
            return HWND.Null;
        }
        GUITHREADINFO info = new() { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(thread, ref info) ? info.hwndFocus : HWND.Null;
    }
}
