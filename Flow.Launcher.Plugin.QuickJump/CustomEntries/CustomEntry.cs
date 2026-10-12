namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>A user-managed path shortcut persisted in the plugin settings.</summary>
internal sealed class CustomEntry
{
    public string Alias { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public CustomEntryType Type { get; set; }

    public bool Enabled { get; set; } = true;
}
