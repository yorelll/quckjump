using System;
using FlaUI.UIA3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Outcome of reading a control's text, distinguishing empty text from an unreadable control.</summary>
internal sealed record ControlTextRead(bool Succeeded, string? Text);

/// <summary>
/// Reads control text across process boundaries. GetWindowText cannot read a
/// control owned by another process (it only returns top-level captions), so
/// this uses a bounded WM_GETTEXT and falls back to UIA3 Value/Name.
/// </summary>
internal static class CrossProcessTextReader
{
    private const int MaximumTextLength = 1024;
    private const uint TimeoutMilliseconds = 250;

    public static unsafe ControlTextRead Read(HWND handle)
    {
        if (handle == HWND.Null || !PInvoke.IsWindow(handle))
        {
            return new ControlTextRead(false, null);
        }

        if (TryReadWithMessage(handle, out string? messageText))
        {
            return new ControlTextRead(true, messageText);
        }

        return TryReadWithUia(handle);
    }

    private static unsafe bool TryReadWithMessage(HWND handle, out string? text)
    {
        text = null;
        var buffer = new char[MaximumTextLength];
        fixed (char* pointer = buffer)
        {
            nint sent = PInvoke.SendMessageTimeout(
                handle,
                PInvoke.WM_GETTEXT,
                (WPARAM)MaximumTextLength,
                (LPARAM)pointer,
                SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK,
                TimeoutMilliseconds,
                out _).Value;
            if (sent == 0)
            {
                return false;
            }
        }

        int length = Array.IndexOf(buffer, '\0');
        text = new string(buffer, 0, length < 0 ? buffer.Length : length);
        return true;
    }

    private static ControlTextRead TryReadWithUia(HWND handle)
    {
        try
        {
            using var automation = new UIA3Automation();
            var element = automation.FromHandle(handle);
            var value = element.Patterns.Value.PatternOrDefault?.Value?.Value;
            if (value is not null)
            {
                return new ControlTextRead(true, value);
            }

            return new ControlTextRead(true, element.Name ?? string.Empty);
        }
        catch (Exception)
        {
            return new ControlTextRead(false, null);
        }
    }
}
