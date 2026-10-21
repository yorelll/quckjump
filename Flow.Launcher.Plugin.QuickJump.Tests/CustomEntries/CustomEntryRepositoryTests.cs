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

        Assert.True(repository.TryUpdate("report", CustomEntryType.File, "documents", folder, CustomEntryType.Folder, false, out var updateError), updateError is null ? null : updateError.Code.ToString());
        Assert.Equal("documents", repository.GetAll().Single().Alias);
        Assert.Equal(CustomEntryType.Folder, repository.GetAll().Single().Type);
        Assert.False(repository.GetAll().Single().Enabled);
        Assert.Equal(2, saves);

        Assert.True(repository.TrySetEnabled("documents", CustomEntryType.Folder, true, out var toggleError), toggleError is null ? null : toggleError.Code.ToString());
        Assert.True(repository.GetAll().Single().Enabled);
        Assert.Equal(3, saves);

        Assert.True(repository.Remove("DOCUMENTS", CustomEntryType.Folder));
        Assert.Empty(repository.GetAll());
        Assert.Equal(4, saves);
    }

    [Fact]
    public void TypeSpecificMutations_OperateOnlyOnTheIntendedSameAliasEntry()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
        int saves = 0;
        var repository = new CustomEntryRepository(settings, () => saves++);
        var file = CreateFile("output.txt");
        var folder = CreateDirectory("output");

        Assert.True(repository.TryAdd("output", file, CustomEntryType.File, true, out _));
        Assert.True(repository.TryAdd("output", folder, CustomEntryType.Folder, true, out var duplicateError), duplicateError?.Code.ToString());
        Assert.Equal(2, repository.GetAll().Count);

        Assert.True(repository.TrySetEnabled("output", CustomEntryType.File, false, out _));
        Assert.False(repository.Find("output", CustomEntryType.File)!.Enabled);
        Assert.True(repository.Find("output", CustomEntryType.Folder)!.Enabled);

        Assert.True(repository.Remove("output", CustomEntryType.File));
        Assert.Single(repository.GetAll());
        Assert.Equal(CustomEntryType.Folder, repository.GetAll().Single().Type);
    }

    [Fact]
    public void Add_RejectsSameAliasAndTypeCaseInsensitively()
    {
        var repository = new CustomEntryRepository(new Flow.Launcher.Plugin.QuickJump.Settings.Settings(), () => { });
        var first = CreateFile("one.txt");
        var second = CreateFile("two.txt");

        Assert.True(repository.TryAdd("Shared", first, CustomEntryType.File, true, out _));
        Assert.False(repository.TryAdd("shared", second, CustomEntryType.File, true, out var error));
        Assert.Equal(Localization.QuickJumpErrorCode.AliasExists, error!.Code);
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
