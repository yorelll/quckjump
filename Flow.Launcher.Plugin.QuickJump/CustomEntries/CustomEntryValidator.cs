using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Validates aliases and verifies that custom-entry paths match their declared type.</summary>
internal static partial class CustomEntryValidator
{
    private const int AliasMaximumLength = 120;

    /// <summary>
    /// Accepts Unicode letters/numbers plus hyphen and underscore. This includes
    /// Chinese, English, and other alphabetic scripts without treating whitespace
    /// or punctuation as valid alias characters.
    /// </summary>
    [GeneratedRegex("^[\\p{L}\\p{N}_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();

    public static string? ValidateAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return "Alias is required.";
        }

        if (alias.Length > AliasMaximumLength)
        {
            return $"Alias must be {AliasMaximumLength} characters or fewer.";
        }

        return AliasPattern().IsMatch(alias)
            ? null
            : "Alias may contain only letters, numbers, hyphens, and underscores; spaces and special characters are not allowed.";
    }

    public static string? ValidatePath(string? path, CustomEntryType type)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Path is required.";
        }

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "Path is not a valid Windows path.";
        }

        return type switch
        {
            CustomEntryType.File when !File.Exists(path) => "The selected file does not exist.",
            CustomEntryType.Folder when !Directory.Exists(path) => "The selected folder does not exist.",
            _ => null,
        };
    }

    public static string? Validate(CustomEntry entry, IEnumerable<CustomEntry> existingEntries, string? originalAlias = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(existingEntries);

        var aliasError = ValidateAlias(entry.Alias);
        if (aliasError is not null)
        {
            return aliasError;
        }

        var pathError = ValidatePath(entry.Path, entry.Type);
        if (pathError is not null)
        {
            return pathError;
        }

        var duplicate = existingEntries.Any(existing =>
            !string.Equals(existing.Alias, originalAlias, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Alias, entry.Alias, StringComparison.OrdinalIgnoreCase));
        return duplicate ? $"An entry with alias '{entry.Alias}' already exists." : null;
    }
}
