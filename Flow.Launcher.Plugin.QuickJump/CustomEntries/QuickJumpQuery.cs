using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Parsed QuickJump query filters and the remaining user search text.</summary>
internal sealed record QuickJumpQuery(
    string SearchText,
    CustomEntryType? CustomTypeFilter,
    bool SearchOnly,
    bool HasRecognizedFilter)
{
    public static QuickJumpQuery Parse(string? rawSearch)
    {
        var remainingTerms = new List<string>();
        CustomEntryType? type = null;
        bool searchOnly = false;
        bool recognizedFilter = false;

        foreach (string term in (rawSearch ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (term.Equals("/s", StringComparison.OrdinalIgnoreCase))
            {
                searchOnly = true;
                recognizedFilter = true;
                continue;
            }

            if (term.Equals("/f", StringComparison.OrdinalIgnoreCase))
            {
                type = CustomEntryType.File;
                recognizedFilter = true;
                continue;
            }

            if (term.Equals("/fd", StringComparison.OrdinalIgnoreCase))
            {
                type = CustomEntryType.Folder;
                recognizedFilter = true;
                continue;
            }

            remainingTerms.Add(term);
        }

        return new QuickJumpQuery(string.Join(" ", remainingTerms), type, searchOnly, recognizedFilter);
    }

    /// <summary>Whether native entries and the literal-path result should be omitted.</summary>
    public bool IsCustomOnly => !SearchOnly && CustomTypeFilter is not null;

    /// <summary>Whether custom entries should participate in a normal combined search.</summary>
    public bool IncludesCustomEntries => !SearchOnly;

    /// <summary>Whether native entries should participate in the results.</summary>
    public bool IncludesNativeEntries => !IsCustomOnly;

    /// <summary>Whether the implicit literal path result should be added.</summary>
    public bool IncludesLiteralPath => !SearchOnly && !IsCustomOnly && !string.IsNullOrWhiteSpace(SearchText);
}
