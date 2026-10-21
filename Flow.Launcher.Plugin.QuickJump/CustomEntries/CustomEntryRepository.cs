using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.QuickJump.Localization;
using Flow.Launcher.Plugin.QuickJump.Settings;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Validates and mutates custom entries owned by Flow settings storage.</summary>
internal sealed class CustomEntryRepository
{
    private readonly Settings.Settings _settings;
    private readonly Action _save;

    public CustomEntryRepository(Settings.Settings settings, Action save)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _settings.CustomEntries ??= [];
    }

    public IReadOnlyList<CustomEntry> GetAll() => _settings.CustomEntries;

    public bool TryAdd(string alias, string path, CustomEntryType type, bool enabled, out QuickJumpError? error)
    {
        var entry = new CustomEntry
        {
            Alias = alias.Trim(),
            Path = path.Trim(),
            Type = type,
            Enabled = enabled,
        };
        error = CustomEntryValidator.Validate(entry, _settings.CustomEntries);
        if (error is not null)
        {
            return false;
        }

        _settings.CustomEntries.Add(entry);
        _save();
        return true;
    }

    /// <summary>Update keyed by the original alias and type so file/folder aliases may coexist.</summary>
    public bool TryUpdate(
        string originalAlias,
        CustomEntryType originalType,
        string alias,
        string path,
        CustomEntryType type,
        bool enabled,
        out QuickJumpError? error)
    {
        var existing = Find(originalAlias, originalType);
        if (existing is null)
        {
            error = new QuickJumpError(QuickJumpErrorCode.EntryMissing, originalAlias);
            return false;
        }

        var proposed = new CustomEntry
        {
            Alias = alias.Trim(),
            Path = path.Trim(),
            Type = type,
            Enabled = enabled,
        };
        error = CustomEntryValidator.Validate(proposed, _settings.CustomEntries, originalAlias, originalType);
        if (error is not null)
        {
            return false;
        }

        existing.Alias = proposed.Alias;
        existing.Path = proposed.Path;
        existing.Type = proposed.Type;
        existing.Enabled = proposed.Enabled;
        _save();
        return true;
    }

    public bool Remove(string alias, CustomEntryType type)
    {
        var entry = Find(alias, type);
        if (entry is null)
        {
            return false;
        }

        _settings.CustomEntries.Remove(entry);
        _save();
        return true;
    }

    public bool TrySetEnabled(string alias, CustomEntryType type, bool enabled, out QuickJumpError? error)
    {
        var entry = Find(alias, type);
        if (entry is null)
        {
            error = new QuickJumpError(QuickJumpErrorCode.EntryMissing, alias);
            return false;
        }

        if (entry.Enabled != enabled)
        {
            entry.Enabled = enabled;
            _save();
        }

        error = null;
        return true;
    }

    public CustomEntry? Find(string alias, CustomEntryType type) =>
        _settings.CustomEntries.FirstOrDefault(entry =>
            entry.Type == type &&
            string.Equals(entry.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
