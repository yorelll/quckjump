using System;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class AliasGeneratorTests
{
    [Theory]
    [InlineData(@"C:\docs\My C# Notes.docx", "File", "My-C-Notes")]
    [InlineData(@"C:\docs\测试 文件夹", "Folder", "测试-文件夹")]
    [InlineData(@"C:\docs\  Work    Files  ", "Folder", "Work-Files")]
    [InlineData(@"C:\docs\-Already--hyphen-", "Folder", "Already-hyphen")]
    public void CreateDefault_SanitizesKnownNames(string path, string typeName, string expected)
    {
        var type = Enum.Parse<CustomEntryType>(typeName);
        Assert.Equal(expected, AliasGenerator.CreateDefault(path, type, []));
    }

    [Fact]
    public void CreateDefault_RemovesOnlyFinalFileExtension()
    {
        Assert.Equal("releasetar", AliasGenerator.CreateDefault(@"C:\docs\release.tar.gz", CustomEntryType.File, []));
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
