using System.Collections.Generic;

namespace Flow.Launcher.Plugin.QuickJump.FileManager;

internal interface IFileManager
{
    IEnumerable<Entry> GetEntries();
}
