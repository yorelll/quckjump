using System;
using System.IO;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Localization;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class PathTypeDetectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.PathTypeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryDetect_ExistingDirectoryReturnsFolder()
    {
        string folder = Path.Combine(_root, "folder"); Directory.CreateDirectory(folder);
        var success = PathTypeDetector.TryDetect(folder, out var type, out var error);
        Assert.True(success); Assert.Equal(CustomEntryType.Folder, type); Assert.Null(error);
    }

    [Fact]
    public void TryDetect_ExistingFileReturnsFile()
    {
        Directory.CreateDirectory(_root); string file = Path.Combine(_root, "entry.txt"); File.WriteAllText(file, "quickjump");
        var success = PathTypeDetector.TryDetect(file, out var type, out var error);
        Assert.True(success); Assert.Equal(CustomEntryType.File, type); Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryDetect_EmptyPathReturnsLanguageNeutralRequiredPathCode(string path)
    {
        var success = PathTypeDetector.TryDetect(path, out _, out var error);
        Assert.False(success); Assert.Equal(QuickJumpErrorCode.PathRequired, error!.Code);
    }

    [Fact]
    public void TryDetect_MissingPathReturnsLanguageNeutralMissingPathCode()
    {
        var success = PathTypeDetector.TryDetect(Path.Combine(_root, "missing"), out _, out var error);
        Assert.False(success); Assert.Equal(QuickJumpErrorCode.PathMissing, error!.Code);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
