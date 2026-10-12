using System;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.FileManager;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class QuickEntrySearchServiceTests
{
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
            fourth => { Assert.Equal("work", fourth.Title); Assert.True(fourth.IsLiteral); });
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

    [Fact]
    public void Search_SearchOnlyExcludesCustomButRetainsNativeAndLiteral()
    {
        var results = _service.Search(
            QuickJumpQuery.Parse("/s hello"),
            [new CustomEntry { Alias = "hello", Path = @"C:\custom", Type = CustomEntryType.File, Enabled = true }],
            [new Entry("hello native", @"C:\native")],
            FuzzyContains);

        Assert.Collection(
            results,
            first => { Assert.Equal("hello native", first.Title); Assert.False(first.IsCustom); },
            second => { Assert.Equal("hello", second.Title); Assert.True(second.IsLiteral); });
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

    private static bool FuzzyContains(string search, string value) =>
        value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
