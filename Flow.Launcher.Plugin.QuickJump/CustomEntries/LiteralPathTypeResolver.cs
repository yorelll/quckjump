using System.IO;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Classifies a literal query path without changing the legacy fallback behavior.</summary>
internal static class LiteralPathTypeResolver
{
    /// <summary>
    /// Existing directories navigate as folders, existing files are confirmed as
    /// files, and an unknown path retains QuickJump's historical folder-first
    /// navigation behavior.
    /// </summary>
    public static CustomEntryType Resolve(string path)
    {
        if (Directory.Exists(path))
        {
            return CustomEntryType.Folder;
        }

        return File.Exists(path) ? CustomEntryType.File : CustomEntryType.Folder;
    }
}
