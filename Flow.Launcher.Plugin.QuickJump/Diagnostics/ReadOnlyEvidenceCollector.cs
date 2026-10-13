using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Patterns;
using FlaUI.UIA3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
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
        // The CsWin32 version used by this project does not generate the
        // desktop-name query with our safe-handle signatures. Record the
        // desktop handles and explicit limitation without opening/switching a
        // desktop; this candidate remains strictly read-only.
        var worker = PInvoke.GetThreadDesktop(PInvoke.GetCurrentThreadId());
        var target = targetThreadId == 0 ? default : PInvoke.GetThreadDesktop(targetThreadId);
        QuickJumpDiagnostics.Info($"readonly-evidence desktop workerHandle={worker} targetHandle={target} input=unavailable(name-query-not-generated)");
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
        return GetIntegrity(process.SafeHandle);
    }

    private static (IntegrityLevel Level, uint? Rid, int Error) GetProcessIntegrity(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return GetIntegrity(process.SafeHandle);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (IntegrityLevel.Unavailable, null, -1);
        }
    }

    private static unsafe (IntegrityLevel Level, uint? Rid, int Error) GetIntegrity(Microsoft.Win32.SafeHandles.SafeProcessHandle processHandle)
    {
        var handle = new HANDLE((void*)processHandle.DangerousGetHandle());
        if (!PInvoke.OpenProcessToken(handle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
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
