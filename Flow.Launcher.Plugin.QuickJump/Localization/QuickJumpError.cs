namespace Flow.Launcher.Plugin.QuickJump.Localization;

/// <summary>Language-neutral validation failures resolved to current Flow resources at the UI boundary.</summary>
internal enum QuickJumpErrorCode
{
    AliasRequired,
    AliasLength,
    AliasInvalid,
    PathRequired,
    PathInvalid,
    PathMissing,
    PathTypeChanged,
    AliasExists,
    EntryMissing,
    DetectPath,
}

/// <summary>A localized-message key plus optional format arguments.</summary>
internal sealed record QuickJumpError(QuickJumpErrorCode Code, object? Argument = null);
