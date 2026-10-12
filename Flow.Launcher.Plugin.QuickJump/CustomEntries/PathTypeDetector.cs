using System.IO;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Determines the persisted custom-entry type from the current filesystem path.</summary>
internal static class PathTypeDetector
{
    public static bool TryDetect(string? path, out CustomEntryType type, out string? error)
    {
        type = CustomEntryType.File;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径不能为空。";
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

        error = "路径不存在或无法访问。请选择一个现有文件或文件夹。";
        return false;
    }
}
