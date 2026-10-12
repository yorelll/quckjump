using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Validates aliases and ensures custom-entry paths agree with their detected type.</summary>
internal static partial class CustomEntryValidator
{
    private const int AliasMaximumLength = 120;

    /// <summary>Accepts Unicode letters/numbers plus hyphen and underscore.</summary>
    [GeneratedRegex("^[\\p{L}\\p{N}_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();

    public static QuickJumpError? ValidateAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return new QuickJumpError(QuickJumpErrorCode.AliasRequired);
        }

        if (alias.Length > AliasMaximumLength)
        {
            return new QuickJumpError(QuickJumpErrorCode.AliasLength, AliasMaximumLength);
        }

        return AliasPattern().IsMatch(alias)
            ? null
            : new QuickJumpError(QuickJumpErrorCode.AliasInvalid);
    }

    public static QuickJumpError? ValidatePath(string? path, CustomEntryType type)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new QuickJumpError(QuickJumpErrorCode.PathRequired);
        }

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new QuickJumpError(QuickJumpErrorCode.PathInvalid);
        }

        if (!PathTypeDetector.TryDetect(path, out var detectedType, out var error))
        {
            return error;
        }

        return detectedType == type ? null : new QuickJumpError(QuickJumpErrorCode.PathTypeChanged);
    }

    public static QuickJumpError? Validate(CustomEntry entry, IEnumerable<CustomEntry> existingEntries, string? originalAlias = null)
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
        return duplicate ? new QuickJumpError(QuickJumpErrorCode.AliasExists, entry.Alias) : null;
    }
}
