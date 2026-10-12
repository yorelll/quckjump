using System;
using System.IO;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class CustomEntryValidatorTests
{
    [Theory]
    [InlineData("work")]
    [InlineData("测试-文件_1")]
    [InlineData("résumé")]
    [InlineData("Release_2026")]
    public void ValidateAlias_AcceptsLettersNumbersUnderscoresAndHyphens(string alias)
    {
        Assert.Null(CustomEntryValidator.ValidateAlias(alias));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("work files")]
    [InlineData("work/file")]
    [InlineData("C#")]
    [InlineData("hello!")]
    public void ValidateAlias_RejectsEmptyWhitespaceAndSpecialCharacters(string alias)
    {
        Assert.NotNull(CustomEntryValidator.ValidateAlias(alias));
    }

    [Fact]
    public void Validate_RejectsCaseInsensitiveDuplicateAliasBeforeMutation()
    {
        var temporaryFile = Path.GetTempFileName();
        try
        {
            var entry = new CustomEntry { Alias = "WORK", Path = temporaryFile, Type = CustomEntryType.File };
            var existing = new[] { new CustomEntry { Alias = "work", Path = temporaryFile, Type = CustomEntryType.File } };

            Assert.Contains("already exists", CustomEntryValidator.Validate(entry, existing)!);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    [Fact]
    public void ValidatePath_RejectsMissingPathOfDeclaredType()
    {
        Assert.Equal("The selected file does not exist.", CustomEntryValidator.ValidatePath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), CustomEntryType.File));
        Assert.Equal("The selected folder does not exist.", CustomEntryValidator.ValidatePath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), CustomEntryType.Folder));
    }

    [Fact]
    public void ValidatePath_RejectsFilePassedAsFolder()
    {
        var file = Path.GetTempFileName();
        try
        {
            Assert.Equal("The selected folder does not exist.", CustomEntryValidator.ValidatePath(file, CustomEntryType.Folder));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
