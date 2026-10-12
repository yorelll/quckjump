namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>A testable result model, before it is converted into a Flow Launcher result.</summary>
internal sealed record QuickEntryResult(
    string Title,
    string Path,
    CustomEntryType Type,
    bool IsCustom,
    bool IsLiteral);
