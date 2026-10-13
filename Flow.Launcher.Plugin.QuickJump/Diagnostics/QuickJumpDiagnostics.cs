using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>
/// Temporary targeted diagnostics for the manual v1.0.1-diagnostic.1 test
/// package. Removing this one class and its call sites removes all extra trace
/// output; it must not be included in the eventual public release.
/// </summary>
internal static class QuickJumpDiagnostics
{
    public const bool Enabled = true;
    public const string Tag = "QuickJump.Diagnostics";
    public const string Prefix = "[QuickJumpDiag]";

    private static IPublicAPI? _api;

    public static void Initialize(PluginInitContext context, string version)
    {
        _api = context.API;
        Info($"init version={version} enabled={Enabled}");
    }

    public static void Info(string message)
    {
        if (Enabled)
        {
            _api?.LogInfo(Tag, $"{Prefix} {message}");
        }
    }

    public static void Warn(string message)
    {
        if (Enabled)
        {
            _api?.LogWarn(Tag, $"{Prefix} {message}");
        }
    }

    public static void Exception(string operation, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Warn($"{operation} exception={exception.GetType().Name}: {exception.Message}");
    }
}
