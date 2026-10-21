using System;
using System.IO;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure, bounded verification rules for navigating a file dialog to a file's parent folder.</summary>
internal static class FileJumpNavigationPlan
{
    public const int DefaultVerificationAttempts = 12;
    public const int DefaultVerificationDelayMilliseconds = 100;

    /// <summary>Returns the parent folder of a file path, or null for a root/missing parent.</summary>
    public static string? GetParentFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(filePath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static string? GetFileName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string name = Path.GetFileName(filePath);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Navigation is only accepted when the observed address text or window
    /// title proves that the dialog moved to the expected parent folder.
    /// </summary>
    public static bool IsNavigationVerified(string? expectedFolder, string? addressText, string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(expectedFolder))
        {
            return false;
        }

        string folder = expectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderName = Path.GetFileName(folder);
        string normalizedAddress = Normalize(addressText);
        string normalizedTitle = Normalize(windowTitle);

        return normalizedAddress.Contains(folder, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(folderName) && normalizedTitle.Contains(folderName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Bounded retry policy shared by navigation verification and filename lookup.</summary>
    public static bool ShouldVerify(int attempt) => attempt >= 0 && attempt < DefaultVerificationAttempts;

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('/', '\\').Replace("\"", string.Empty).Trim();
}
