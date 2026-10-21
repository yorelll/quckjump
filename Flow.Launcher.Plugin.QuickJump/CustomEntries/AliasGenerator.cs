using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Creates editable, safe default aliases for paths added from search results.</summary>
internal static class AliasGenerator
{
    public static string CreateDefault(string path, CustomEntryType type, IEnumerable<CustomEntry> existingEntries)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(existingEntries);

        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (type == CustomEntryType.File)
        {
            name = Path.GetFileNameWithoutExtension(name);
        }

        var builder = new StringBuilder(name.Length);
        bool pendingHyphen = false;
        foreach (char character in name)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0)
                {
                    pendingHyphen = true;
                }
                continue;
            }

            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                if (pendingHyphen && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
                pendingHyphen = false;

                if (character == '-' && (builder.Length == 0 || builder[^1] == '-'))
                {
                    continue;
                }

                builder.Append(character);
            }
        }

        string candidate = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = QuickJumpText.AliasFallback();
        }

        // Alias uniqueness is per type: an existing file alias does not force a
        // suffix on a new folder alias (and vice versa).
        var aliases = existingEntries
            .Where(entry => entry.Type == type)
            .Select(entry => entry.Alias)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!aliases.Contains(candidate))
        {
            return candidate;
        }

        for (int suffix = 1; ; suffix++)
        {
            string unique = $"{candidate}-{suffix}";
            if (!aliases.Contains(unique))
            {
                return unique;
            }
        }
    }
}
