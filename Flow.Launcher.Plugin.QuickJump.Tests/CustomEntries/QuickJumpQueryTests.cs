using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class QuickJumpQueryTests
{
    [Theory]
    [InlineData("/f report", "report", CustomEntryType.File, false, true)]
    [InlineData("report /FD", "report", CustomEntryType.Folder, false, true)]
    [InlineData("/s report", "report", null, true, true)]
    [InlineData("report /s /f", "report", CustomEntryType.File, true, true)]
    [InlineData("/unknown report", "/unknown report", null, false, false)]
    public void Parse_RecognizesFiltersInAnyOrderAndPreservesUnknownSlashTerms(string raw, string text, CustomEntryType? type, bool searchOnly, bool hasFilter)
    {
        var parsed = QuickJumpQuery.Parse(raw);

        Assert.Equal(text, parsed.SearchText);
        Assert.Equal(type, parsed.CustomTypeFilter);
        Assert.Equal(searchOnly, parsed.SearchOnly);
        Assert.Equal(hasFilter, parsed.HasRecognizedFilter);
    }

    [Fact]
    public void Parse_SearchOnlyWinsOverAConflictingCustomFilter()
    {
        var parsed = QuickJumpQuery.Parse("/fd /s project");

        Assert.True(parsed.SearchOnly);
        Assert.False(parsed.IsCustomOnly);
        Assert.False(parsed.IncludesCustomEntries);
        Assert.True(parsed.IncludesNativeEntries);
        Assert.True(parsed.IncludesLiteralPath);
    }

    [Fact]
    public void Parse_FilterOnlyCustomQueryExcludesNativeAndLiteralEntries()
    {
        var parsed = QuickJumpQuery.Parse("/f");

        Assert.True(parsed.IsCustomOnly);
        Assert.True(parsed.IncludesCustomEntries);
        Assert.False(parsed.IncludesNativeEntries);
        Assert.False(parsed.IncludesLiteralPath);
    }

    [Fact]
    public void Parse_SearchOnlyWithNoTermHasNoLiteralPath()
    {
        var parsed = QuickJumpQuery.Parse("/s");

        Assert.True(parsed.IncludesNativeEntries);
        Assert.False(parsed.IncludesLiteralPath);
    }
}
