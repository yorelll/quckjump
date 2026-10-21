using System;
using System.IO;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Verification state of navigating a file dialog to a target folder.</summary>
internal enum NavigationVerification
{
    Confirmed,
    NotNavigated,
    Unverifiable,
}

/// <summary>Pure, bounded verification rules for navigating a file dialog to a file's parent folder.</summary>
internal static class FileJumpNavigationPlan
{
    public const int DefaultVerificationAttempts = 12;
    public const int DefaultVerificationDelayMilliseconds = 100;

    /// <summary>Fallback after bounded attempts cannot prove or disprove navigation.</summary>
    public const NavigationFallback DefaultUnverifiableFallback = NavigationFallback.FillFileName;

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
    /// Classifies the evidence. Unreadable address text is not proof of failure;
    /// in that case only the window title is auxiliary evidence.
    /// </summary>
    public static NavigationVerification Verify(string? expectedFolder, ControlTextRead addressText, string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(expectedFolder))
        {
            return NavigationVerification.NotNavigated;
        }

        if (!addressText.Succeeded)
        {
            return NavigationVerification.Unverifiable;
        }

        if (Matches(expectedFolder, addressText.Text))
        {
            return NavigationVerification.Confirmed;
        }

        // The address text was readable and did not match. A matching title is
        // still auxiliary evidence, otherwise navigation has not happened yet.
        return Matches(expectedFolder, windowTitle)
            ? NavigationVerification.Confirmed
            : NavigationVerification.NotNavigated;
    }

    private static bool Matches(string expectedFolder, string? observed)
    {
        if (string.IsNullOrWhiteSpace(observed))
        {
            return false;
        }

        string folder = expectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderName = Path.GetFileName(folder);
        string normalized = observed.Replace('/', '\\').Replace("\"", string.Empty).Trim();

        return normalized.Contains(folder, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(folderName) && normalized.Contains(folderName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Bounded retry policy shared by navigation verification and filename lookup.</summary>
    public static bool ShouldVerify(int attempt) => attempt >= 0 && attempt < DefaultVerificationAttempts;
}

/// <summary>Action chosen when navigation cannot be verified within the bound.</summary>
internal enum NavigationFallback
{
    FillFileName,
    Abort,
}
