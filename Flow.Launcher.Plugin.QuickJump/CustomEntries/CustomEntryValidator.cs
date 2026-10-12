using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.QuickJump.CustomEntries;

/// <summary>验证别名并确保自定义条目路径与自动识别出的类型一致。</summary>
internal static partial class CustomEntryValidator
{
    private const int AliasMaximumLength = 120;

    /// <summary>
    /// 接受 Unicode 字母/数字、连字符和下划线，因而支持中文、英文及其他文字，
    /// 同时拒绝空格和其他标点符号。
    /// </summary>
    [GeneratedRegex("^[\\p{L}\\p{N}_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();

    public static string? ValidateAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return "别名不能为空。";
        }

        if (alias.Length > AliasMaximumLength)
        {
            return $"别名长度不能超过 {AliasMaximumLength} 个字符。";
        }

        return AliasPattern().IsMatch(alias)
            ? null
            : "别名只能包含字母、数字、连字符和下划线，不能包含空格或特殊字符。";
    }

    public static string? ValidatePath(string? path, CustomEntryType type)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "路径不能为空。";
        }

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "路径不是有效的 Windows 路径。";
        }

        if (!PathTypeDetector.TryDetect(path, out var detectedType, out var error))
        {
            return error;
        }

        return detectedType == type ? null : "路径类型已变化，请重新保存以自动识别类型。";
    }

    public static string? Validate(CustomEntry entry, IEnumerable<CustomEntry> existingEntries, string? originalAlias = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(existingEntries);

        var aliasError = ValidateAlias(entry.Alias);
        if (aliasError is not null)
        {
            return aliasError;
        }

        var pathError = ValidatePath(entry.Path, entry.Type);
        if (pathError is not null)
        {
            return pathError;
        }

        var duplicate = existingEntries.Any(existing =>
            !string.Equals(existing.Alias, originalAlias, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Alias, entry.Alias, StringComparison.OrdinalIgnoreCase));
        return duplicate ? $"别名“{entry.Alias}”已存在。" : null;
    }
}
