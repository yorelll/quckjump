using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class AliasGeneratorTests
{
    [Theory]
    [InlineData(@"C:\docs\My C# Notes.docx", CustomEntryType.File, "MyC-Notes")]
    [InlineData(@"C:\docs\测试 文件夹", CustomEntryType.Folder, "测试-文件夹")]
    [InlineData(@"C:\docs\  Work    Files  ", CustomEntryType.Folder, "Work-Files")]
    [InlineData(@"C:\docs\-Already--hyphen-", CustomEntryType.Folder, "Already-hyphen")]
    public void CreateDefault_SanitizesKnownNames(string path, CustomEntryType type, string expected)
    {
        Assert.Equal(expected, AliasGenerator.CreateDefault(path, type, []));
    }

    [Fact]
    public void CreateDefault_RemovesOnlyFinalFileExtension()
    {
        Assert.Equal("release.tar", AliasGenerator.CreateDefault(@"C:\docs\release.tar.gz", CustomEntryType.File, []));
    }

    [Fact]
    public void CreateDefault_UsesFallbackWhenSanitizationLeavesNothing()
    {
        Assert.Equal("entry", AliasGenerator.CreateDefault(@"C:\docs\!!!.txt", CustomEntryType.File, []));
    }

    [Fact]
    public void CreateDefault_AddsCaseInsensitiveSuffixUntilUnique()
    {
        CustomEntry[] existing =
        [
            new() { Alias = "work" },
            new() { Alias = "WORK-1" },
        ];

        Assert.Equal("work-2", AliasGenerator.CreateDefault(@"C:\docs\work.txt", CustomEntryType.File, existing));
    }
}
