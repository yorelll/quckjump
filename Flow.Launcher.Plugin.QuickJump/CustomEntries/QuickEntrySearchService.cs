using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.QuickJump.FileManager;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>
/// Combines custom entries, native file-manager folders, and the literal path in
/// the precise order expected by QuickJump. Matching is supplied by the host so
/// this service remains unit-testable without Flow Launcher.
/// </summary>
internal sealed class QuickEntrySearchService
{
    public IReadOnlyList<QuickEntryResult> Search(
        QuickJumpQuery query,
        IEnumerable<CustomEntry> customEntries,
        IEnumerable<Entry> nativeEntries,
        Func<string, string, bool> fuzzyMatches)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(customEntries);
        ArgumentNullException.ThrowIfNull(nativeEntries);
        ArgumentNullException.ThrowIfNull(fuzzyMatches);

        string term = query.SearchText;
        var results = new List<QuickEntryResult>();

        // Empty, unfiltered cd must remain exactly the legacy experience: only
        // currently open folders; custom shortcuts are intentionally absent.
        bool plainEmptySearch = string.IsNullOrWhiteSpace(term) && !query.HasRecognizedFilter;
        if (query.IncludesCustomEntries && !plainEmptySearch)
        {
            var matchingCustom = customEntries
                .Where(entry => entry.Enabled)
                .Where(entry => query.CustomTypeFilter is null || entry.Type == query.CustomTypeFilter)
                .Where(entry => string.IsNullOrWhiteSpace(term) || fuzzyMatches(term, entry.Alias))
                .OrderByDescending(entry => string.Equals(entry.Alias, term, StringComparison.OrdinalIgnoreCase))
                .ThenBy(entry => entry.Alias, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => new QuickEntryResult(entry.Alias, entry.Path, entry.Type, IsCustom: true, IsLiteral: false));
            results.AddRange(matchingCustom);
        }

        if (query.IncludesNativeEntries)
        {
            var matchingNative = nativeEntries.Where(entry =>
                string.IsNullOrWhiteSpace(term) ||
                fuzzyMatches(term, entry.Name) ||
                fuzzyMatches(term, entry.Path));
            results.AddRange(matchingNative.Select(entry =>
                new QuickEntryResult(entry.Name, entry.Path, CustomEntryType.Folder, IsCustom: false, IsLiteral: false)));
        }

        if (query.IncludesLiteralPath)
        {
            results.Add(new QuickEntryResult(term, term, CustomEntryType.File, IsCustom: false, IsLiteral: true));
        }

        return results;
    }
}
