using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Patterns;
using FlaUI.UIA3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Collects only read-only integrity, desktop, and UIA capability evidence.</summary>
internal static class ReadOnlyEvidenceCollector
{
    public static unsafe void LogProcessIntegrity(uint targetProcessId)
    {
        var current = GetCurrentIntegrity();
        var target = GetProcessIntegrity(targetProcessId);
        QuickJumpDiagnostics.Info(
            $"readonly-evidence integrity current={current.Level};rid={FormatRid(current.Rid)};error={current.Error} " +
            $"target={target.Level};rid={FormatRid(target.Rid)};error={target.Error}");
    }

    public static unsafe void LogDesktopContext(uint targetThreadId)
    {
        string worker = GetDesktopName(PInvoke.GetThreadDesktop(PInvoke.GetCurrentThreadId()));
        string target = targetThreadId == 0 ? "unavailable(no-target-thread)" : GetDesktopName(PInvoke.GetThreadDesktop(targetThreadId));
        string input = GetInputDesktopName();
        QuickJumpDiagnostics.Info($"readonly-evidence desktop worker={worker} target={target} input={input}");
    }

    public static void LogAddressUiaCapabilities(HWND addressEdit, bool addressQualified)
    {
        if (!addressQualified || addressEdit == HWND.Null)
        {
            QuickJumpDiagnostics.Info("readonly-evidence uia addressQualified=false; snapshot=skipped");
            return;
        }

        try
        {
            using var automation = new UIA3Automation();
            var element = automation.FromHandle(addressEdit);
            bool value = element.Patterns.Value.IsSupported;
            bool text = element.Patterns.Text.IsSupported;
            bool legacy = element.Patterns.LegacyIAccessible.IsSupported;
            bool selection = element.Patterns.Selection.IsSupported;
            bool expandCollapse = element.Patterns.ExpandCollapse.IsSupported;
            QuickJumpDiagnostics.Info(
                $"readonly-evidence uia controlType={element.ControlType} automationId={element.AutomationId ?? "<empty>"} " +
                $"name={Sanitize(element.Name)} enabled={element.IsEnabled} " +
                $"patterns=value:{value};text:{text};legacy:{legacy};selection:{selection};expandCollapse:{expandCollapse}");
        }
        catch (Exception exception)
        {
            QuickJumpDiagnostics.Exception("readonly-evidence uia-snapshot", exception);
        }
    }

    private static unsafe (IntegrityLevel Level, uint? Rid, int Error) GetCurrentIntegrity()
    {
        using var process = Process.GetCurrentProcess();
        return GetIntegrity(process.Handle);
    }

    private static (IntegrityLevel Level, uint? Rid, int Error) GetProcessIntegrity(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return GetIntegrity(process.Handle);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (IntegrityLevel.Unavailable, null, -1);
        }
    }

    private static unsafe (IntegrityLevel Level, uint? Rid, int Error) GetIntegrity(nint processHandle)
    {
        if (!PInvoke.OpenProcessToken((HANDLE)processHandle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            return (IntegrityLevel.Unavailable, null, Marshal.GetLastWin32Error());
        }

        try
        {
            uint required = 0;
            _ = PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out required);
            if (required == 0)
            {
                return (IntegrityLevel.Unavailable, null, Marshal.GetLastWin32Error());
            }

            var buffer = Marshal.AllocHGlobal(checked((int)required));
            try
            {
                if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, (void*)buffer, required, out _))
                {
                    return (IntegrityLevel.Unavailable, null, Marshal.GetLastWin32Error());
                }

                // TOKEN_MANDATORY_LABEL begins with a SID_AND_ATTRIBUTES whose
                // first field is the SID pointer. The final SID subauthority is
                // the integrity RID.
                var sid = Marshal.ReadIntPtr(buffer);
                byte subAuthorityCount = Marshal.ReadByte(sid, 1);
                uint rid = unchecked((uint)Marshal.ReadInt32(sid, 8 + 4 * (subAuthorityCount - 1)));
                return (ReadOnlyEvidencePlan.ClassifyIntegrityRid(rid), rid, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            PInvoke.CloseHandle(token);
        }
    }

    private static unsafe string GetDesktopName(HDESK desktop)
    {
        if (desktop.IsNull)
        {
            return "unavailable";
        }

        uint required = 0;
        _ = PInvoke.GetUserObjectInformation(desktop, USER_OBJECT_INFORMATION_INDEX.UOI_NAME, null, 0, &required);
        if (required == 0)
        {
            return $"unavailable(error:{Marshal.GetLastWin32Error()})";
        }

        var buffer = new char[checked((int)(required / sizeof(char)))];
        fixed (char* chars = buffer)
        {
            if (!PInvoke.GetUserObjectInformation(desktop, USER_OBJECT_INFORMATION_INDEX.UOI_NAME, chars, required, &required))
            {
                return $"unavailable(error:{Marshal.GetLastWin32Error()})";
            }
        }
        return new string(buffer).TrimEnd('\0');
    }

    private static unsafe string GetInputDesktopName()
    {
        var desktop = PInvoke.OpenInputDesktop(0, false, (uint)DESKTOP_ACCESS_FLAGS.DESKTOP_READOBJECTS);
        if (desktop.IsNull)
        {
            return $"unavailable(error:{Marshal.GetLastWin32Error()})";
        }
        try
        {
            return GetDesktopName(desktop);
        }
        finally
        {
            PInvoke.CloseDesktop(desktop);
        }
    }

    private static string FormatRid(uint? rid) => rid is uint value ? $"0x{value:X}" : "unavailable";

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }
        return value.Length <= 80 ? value.Replace('\r', ' ').Replace('\n', ' ') : value[..80].Replace('\r', ' ').Replace('\n', ' ') + "…";
    }
}
