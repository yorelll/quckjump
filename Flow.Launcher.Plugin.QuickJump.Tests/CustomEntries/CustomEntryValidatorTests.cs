using System;
using System.IO;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Localization;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.CustomEntries;

public sealed class CustomEntryValidatorTests
{
    [Theory]
    [InlineData("work")]
    [InlineData("测试-文件_1")]
    [InlineData("résumé")]
    [InlineData("Release_2026")]
    public void ValidateAlias_AcceptsLettersNumbersUnderscoresAndHyphens(string alias) => Assert.Null(CustomEntryValidator.ValidateAlias(alias));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("work files")]
    [InlineData("work/file")]
    [InlineData("C#")]
    [InlineData("hello!")]
    public void ValidateAlias_RejectsInvalidValuesWithLanguageNeutralCode(string alias)
    {
        var error = CustomEntryValidator.ValidateAlias(alias);
        Assert.NotNull(error);
        Assert.Contains(error!.Code, new[] { QuickJumpErrorCode.AliasRequired, QuickJumpErrorCode.AliasInvalid });
    }

    [Fact]
    public void Validate_RejectsCaseInsensitiveDuplicateAliasOfTheSameType()
    {
        var temporaryFile = Path.GetTempFileName();
        try
        {
            var entry = new CustomEntry { Alias = "WORK", Path = temporaryFile, Type = CustomEntryType.File };
            var existing = new[] { new CustomEntry { Alias = "work", Path = temporaryFile, Type = CustomEntryType.File } };
            Assert.Equal(QuickJumpErrorCode.AliasExists, CustomEntryValidator.Validate(entry, existing)!.Code);
        }
        finally { File.Delete(temporaryFile); }
    }

    [Fact]
    public void Validate_AllowsSameAliasForFileAndFolderEntries()
    {
        var file = Path.GetTempFileName();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var proposed = new CustomEntry { Alias = "output", Path = folder, Type = CustomEntryType.Folder };
            var existing = new[] { new CustomEntry { Alias = "output", Path = file, Type = CustomEntryType.File } };

            Assert.Null(CustomEntryValidator.Validate(proposed, existing));
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void Validate_EditingKeepsSameEntryFromBeingItsOwnDuplicate()
    {
        var file = Path.GetTempFileName();
        try
        {
            var proposed = new CustomEntry { Alias = "OUTPUT", Path = file, Type = CustomEntryType.File };
            var existing = new[] { new CustomEntry { Alias = "output", Path = file, Type = CustomEntryType.File } };

            Assert.Null(CustomEntryValidator.Validate(proposed, existing, originalAlias: "output", originalType: CustomEntryType.File));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ValidatePath_RejectsMissingPathForEitherDeclaredType()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal(QuickJumpErrorCode.PathMissing, CustomEntryValidator.ValidatePath(missing, CustomEntryType.File)!.Code);
        Assert.Equal(QuickJumpErrorCode.PathMissing, CustomEntryValidator.ValidatePath(missing, CustomEntryType.Folder)!.Code);
    }

    [Fact]
    public void ValidatePath_RejectsFilePassedAsFolder()
    {
        var file = Path.GetTempFileName();
        try { Assert.Equal(QuickJumpErrorCode.PathTypeChanged, CustomEntryValidator.ValidatePath(file, CustomEntryType.Folder)!.Code); }
        finally { File.Delete(file); }
    }
}
