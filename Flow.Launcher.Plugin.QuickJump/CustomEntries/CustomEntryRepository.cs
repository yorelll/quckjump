using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.QuickJump.Settings;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>Validates and mutates the custom entries owned by Flow's settings storage.</summary>
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

    public bool TryAdd(string alias, string path, CustomEntryType type, bool enabled, out string? error)
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

    public bool TryUpdate(string originalAlias, string alias, string path, CustomEntryType type, bool enabled, out string? error)
    {
        var existing = _settings.CustomEntries.FirstOrDefault(entry =>
            string.Equals(entry.Alias, originalAlias, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            error = $"Custom entry '{originalAlias}' no longer exists.";
            return false;
        }

        var proposed = new CustomEntry
        {
            Alias = alias.Trim(),
            Path = path.Trim(),
            Type = type,
            Enabled = enabled,
        };
        error = CustomEntryValidator.Validate(proposed, _settings.CustomEntries, originalAlias);
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

    public bool Remove(string alias)
    {
        var entry = _settings.CustomEntries.FirstOrDefault(item =>
            string.Equals(item.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        _settings.CustomEntries.Remove(entry);
        _save();
        return true;
    }

    public bool TrySetEnabled(string alias, bool enabled, out string? error)
    {
        var entry = _settings.CustomEntries.FirstOrDefault(item =>
            string.Equals(item.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            error = $"Custom entry '{alias}' no longer exists.";
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
}
