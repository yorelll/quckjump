using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Security;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Integrity RID read with the minimum privilege required for a UIPI comparison.</summary>
internal sealed record ProcessIntegrityResult(uint? Rid, int Error);

/// <summary>Reads process integrity using PROCESS_QUERY_INFORMATION-equivalent token access.</summary>
internal static class ProcessIntegrityReader
{
    public static ProcessIntegrityResult GetCurrent()
    {
        using var process = Process.GetCurrentProcess();
        return Read(process.SafeHandle);
    }

    public static ProcessIntegrityResult Get(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return Read(process.SafeHandle);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            int error = exception is System.ComponentModel.Win32Exception win32 ? win32.NativeErrorCode : 0;
            return new ProcessIntegrityResult(null, error);
        }
    }

    private static unsafe ProcessIntegrityResult Read(SafeProcessHandle processHandle)
    {
        if (!PInvoke.OpenProcessToken(processHandle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            return new ProcessIntegrityResult(null, Marshal.GetLastWin32Error());
        }

        try
        {
            uint required = 0;
            _ = PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out required);
            if (required == 0)
            {
                return new ProcessIntegrityResult(null, Marshal.GetLastWin32Error());
            }

            var buffer = Marshal.AllocHGlobal(checked((int)required));
            try
            {
                if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, (void*)buffer, required, out _))
                {
                    return new ProcessIntegrityResult(null, Marshal.GetLastWin32Error());
                }

                // TOKEN_MANDATORY_LABEL starts with SID_AND_ATTRIBUTES. The
                // final SID subauthority is the integrity RID.
                var sid = Marshal.ReadIntPtr(buffer);
                byte subAuthorityCount = Marshal.ReadByte(sid, 1);
                uint rid = unchecked((uint)Marshal.ReadInt32(sid, 8 + 4 * (subAuthorityCount - 1)));
                return new ProcessIntegrityResult(rid, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            token.Dispose();
        }
    }
}

/// <summary>User Interface Privilege Isolation guard for cross-process keyboard input.</summary>
internal enum UipiDecision
{
    Proceed,
    BlockElevatedTarget,
}

internal static class UipiPlan
{
    public const int ErrorAccessDenied = 5;

    /// <summary>
    /// A target whose integrity is higher than Flow's cannot receive injected
    /// keyboard input. Access denied while reading the target token is treated
    /// as likely elevated, because lower/equal tokens are normally readable.
    /// Unavailable data in otherwise normal cases must not block the jump.
    /// </summary>
    public static UipiDecision Decide(ProcessIntegrityResult flow, ProcessIntegrityResult target)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Rid is null && target.Error == ErrorAccessDenied)
        {
            return UipiDecision.BlockElevatedTarget;
        }

        return flow.Rid is uint flowRid && target.Rid is uint targetRid && targetRid > flowRid
            ? UipiDecision.BlockElevatedTarget
            : UipiDecision.Proceed;
    }
}
