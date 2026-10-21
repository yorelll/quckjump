using System;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.FileManager;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class QuickEntrySearchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.SearchTests", Guid.NewGuid().ToString("N"));
    private readonly QuickEntrySearchService _service = new();

    [Fact]
    public void Search_NormalQueryOrdersExactCustomThenFuzzyCustomThenNativeThenLiteral()
    {
        CustomEntry[] custom =
        [
            new() { Alias = "workbench", Path = @"C:\custom\workbench", Type = CustomEntryType.Folder, Enabled = true },
            new() { Alias = "work", Path = @"C:\custom\work", Type = CustomEntryType.Folder, Enabled = true },
            new() { Alias = "disabled-work", Path = @"C:\custom\disabled", Type = CustomEntryType.Folder, Enabled = false },
        ];
        Entry[] native = [new("Work Native", @"C:\native\work")];

        var results = _service.Search(
            QuickJumpQuery.Parse("work"),
            custom,
            native,
            FuzzyContains);

        Assert.Collection(
            results,
            first => { Assert.Equal("work", first.Title); Assert.True(first.IsCustom); },
            second => { Assert.Equal("workbench", second.Title); Assert.True(second.IsCustom); },
            third => { Assert.Equal("Work Native", third.Title); Assert.False(third.IsCustom); Assert.False(third.IsLiteral); },
            fourth => { Assert.Equal("work", fourth.Title); Assert.True(fourth.IsLiteral); Assert.Equal(CustomEntryType.Folder, fourth.Type); });
    }

    [Fact]
    public void Search_PlainEmptyQueryPreservesLegacyNativeOnlyBehavior()
    {
        var results = _service.Search(
            QuickJumpQuery.Parse(""),
            [new CustomEntry { Alias = "custom", Path = @"C:\custom", Type = CustomEntryType.Folder, Enabled = true }],
            [new Entry("Native", @"C:\native")],
            FuzzyContains);

        var result = Assert.Single(results);
        Assert.Equal("Native", result.Title);
        Assert.False(result.IsCustom);
        Assert.False(result.IsLiteral);
    }

    [Fact]
    public void Search_FileFilterOnlyReturnsAllEnabledCustomFiles()
    {
        CustomEntry[] entries =
        [
            new() { Alias = "file", Path = @"C:\file.txt", Type = CustomEntryType.File, Enabled = true },
            new() { Alias = "folder", Path = @"C:\folder", Type = CustomEntryType.Folder, Enabled = true },
            new() { Alias = "off", Path = @"C:\off.txt", Type = CustomEntryType.File, Enabled = false },
        ];

        var results = _service.Search(QuickJumpQuery.Parse("/f"), entries, [new Entry("Native", @"C:\native")], FuzzyContains);

        var result = Assert.Single(results);
        Assert.Equal("file", result.Title);
        Assert.Equal(CustomEntryType.File, result.Type);
        Assert.True(result.IsCustom);
    }

    [Theory]
    [InlineData("/s hello")]
    [InlineData("/s /f hello")]
    public void Search_SearchOnlyReturnsNativeResultsOnly(string query)
    {
        var results = _service.Search(
            QuickJumpQuery.Parse(query),
            [new CustomEntry { Alias = "hello", Path = @"C:\custom", Type = CustomEntryType.File, Enabled = true }],
            [new Entry("hello native", @"C:\native")],
            FuzzyContains);

        var result = Assert.Single(results);
        Assert.Equal("hello native", result.Title);
        Assert.False(result.IsCustom);
        Assert.False(result.IsLiteral);
    }

    [Fact]
    public void Search_ExistingLiteralDirectoryDispatchesAsFolder()
    {
        string directory = CreateDirectory("folder");

        var result = FindLiteral(directory);

        Assert.Equal(CustomEntryType.Folder, result.Type);
    }

    [Fact]
    public void Search_ExistingLiteralFileDispatchesAsFile()
    {
        string file = CreateFile("report.txt");

        var result = FindLiteral(file);

        Assert.Equal(CustomEntryType.File, result.Type);
    }

    [Fact]
    public void Search_NonexistentLiteralPathDefaultsToFolderForLegacyNavigation()
    {
        string nonexistent = Path.Combine(_root, "does-not-exist");

        var result = FindLiteral(nonexistent);

        Assert.Equal(CustomEntryType.Folder, result.Type);
    }

    [Fact]
    public void Search_CustomTypeFilterWithTermExcludesLiteralAndNative()
    {
        var results = _service.Search(
            QuickJumpQuery.Parse("report /fd"),
            [
                new CustomEntry { Alias = "report-folder", Path = @"C:\folder", Type = CustomEntryType.Folder, Enabled = true },
                new CustomEntry { Alias = "report-file", Path = @"C:\file", Type = CustomEntryType.File, Enabled = true },
            ],
            [new Entry("report native", @"C:\native")],
            FuzzyContains);

        var result = Assert.Single(results);
        Assert.Equal("report-folder", result.Title);
        Assert.Equal(CustomEntryType.Folder, result.Type);
    }

    [Fact]
    public void Search_SameAliasFileAndFolderBothAppearWithTheirOwnIconsAndTypes()
    {
        CustomEntry[] entries =
        [
            new() { Alias = "output", Path = @"C:\out.txt", Type = CustomEntryType.File, Enabled = true },
            new() { Alias = "output", Path = @"C:\out", Type = CustomEntryType.Folder, Enabled = true },
        ];

        var results = _service.Search(QuickJumpQuery.Parse("output"), entries, [], FuzzyContains)
            .Where(result => result.IsCustom)
            .ToArray();

        Assert.Equal(2, results.Length);
        Assert.Contains(results, result => result.Type == CustomEntryType.File);
        Assert.Contains(results, result => result.Type == CustomEntryType.Folder);
        Assert.All(results, result => Assert.Equal("output", result.Title));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private QuickEntryResult FindLiteral(string path)
    {
        var results = _service.Search(QuickJumpQuery.Parse(path), [], [], FuzzyContains);
        return Assert.Single(results);
    }

    private string CreateDirectory(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "quickjump");
        return path;
    }

    private static bool FuzzyContains(string search, string value) =>
        value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
