using System;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class CustomEntryRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.RepositoryTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddUpdateToggleAndRemove_MutateSettingsAndSaveEachSuccessfulChange()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
        int saves = 0;
        var repository = new CustomEntryRepository(settings, () => saves++);
        var file = CreateFile("report.txt");
        var folder = CreateDirectory("folder");

        Assert.True(repository.TryAdd("report", file, CustomEntryType.File, true, out var addError), addError is null ? null : addError.Code.ToString());
        Assert.Single(repository.GetAll());
        Assert.Equal(1, saves);

        Assert.True(repository.TryUpdate("report", "documents", folder, CustomEntryType.Folder, false, out var updateError), updateError is null ? null : updateError.Code.ToString());
        Assert.Equal("documents", repository.GetAll().Single().Alias);
        Assert.Equal(CustomEntryType.Folder, repository.GetAll().Single().Type);
        Assert.False(repository.GetAll().Single().Enabled);
        Assert.Equal(2, saves);

        Assert.True(repository.TrySetEnabled("documents", true, out var toggleError), toggleError is null ? null : toggleError.Code.ToString());
        Assert.True(repository.GetAll().Single().Enabled);
        Assert.Equal(3, saves);

        Assert.True(repository.Remove("DOCUMENTS"));
        Assert.Empty(repository.GetAll());
        Assert.Equal(4, saves);
    }

    [Fact]
    public void FailedAdd_DoesNotMutateOrSave()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
        int saves = 0;
        var repository = new CustomEntryRepository(settings, () => saves++);

        Assert.False(repository.TryAdd("not valid", @"C:\missing", CustomEntryType.File, true, out var error));

        Assert.NotNull(error);
        Assert.Empty(repository.GetAll());
        Assert.Equal(0, saves);
    }

    [Fact]
    public void Constructor_NormalizesNullEntriesFromLegacyStorage()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings { CustomEntries = null! };
        var repository = new CustomEntryRepository(settings, () => { });

        Assert.Empty(repository.GetAll());
        Assert.NotNull(settings.CustomEntries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "quickjump");
        return path;
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
