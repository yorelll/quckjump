using System.IO;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Determines the persisted custom-entry type from the current filesystem path.</summary>
internal static class PathTypeDetector
{
    public static bool TryDetect(string? path, out CustomEntryType type, out QuickJumpError? error)
    {
        type = CustomEntryType.File;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = new QuickJumpError(QuickJumpErrorCode.PathRequired);
            return false;
        }

        if (Directory.Exists(path))
        {
            type = CustomEntryType.Folder;
            error = null;
            return true;
        }

        if (File.Exists(path))
        {
            type = CustomEntryType.File;
            error = null;
            return true;
        }

        error = new QuickJumpError(QuickJumpErrorCode.PathMissing);
        return false;
    }
}
