using System.Collections.Generic;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Flow-managed JSON settings for QuickJump.</summary>
internal class Settings
{
    public Strategy Strategy { get; set; } = Strategy.AltD;

    public List<CustomEntry> CustomEntries { get; set; } = [];
}
