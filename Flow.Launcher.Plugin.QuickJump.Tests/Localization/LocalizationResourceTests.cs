using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.Json;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.QuickJump;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Localization;

public sealed class LocalizationResourceTests
{
    [Fact]
    public void LanguageDictionaries_HaveIdenticalKeysAndRequiredChineseDescription()
    {
        var english = Load("en.xaml");
        var chinese = Load("zh-cn.xaml");

        Assert.Equal(english.Keys.OrderBy(key => key), chinese.Keys.OrderBy(key => key));
        Assert.Equal("Quickly jump to files and folders in open or save dialogs.", english["quickjump_plugin_description"]);
        Assert.Equal("快速跳转到打开或保存对话框中的文件和文件夹。", chinese["quickjump_plugin_description"]);
    }

    [Fact]
    public void Plugin_ImplementsFlowInternationalizationContract()
    {
        Assert.True(typeof(IPluginI18n).IsAssignableFrom(typeof(QuickJump)));
    }

    [Fact]
    public void PluginManifest_UsesEnglishFallbackDescription()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "plugin.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal("1.0.1-diagnostic.1", manifest.RootElement.GetProperty("Version").GetString());
        Assert.Equal("Quickly jump to files and folders in open or save dialogs.", manifest.RootElement.GetProperty("Description").GetString());
    }

    [Theory]
    [InlineData("quickjump_context_jump")]
    [InlineData("quickjump_editor_choose_file")]
    [InlineData("quickjump_editor_choose_folder")]
    [InlineData("quickjump_settings_address_help")]
    [InlineData("quickjump_error_path_missing")]
    public void LanguageDictionaries_ContainAllCriticalUiKeys(string key)
    {
        Assert.True(Load("en.xaml").ContainsKey(key));
        Assert.True(Load("zh-cn.xaml").ContainsKey(key));
    }

    private static IReadOnlyDictionary<string, string> Load(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Languages", fileName);
        var document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants()
            .Where(element => element.Name.LocalName == "String")
            .ToDictionary(element => element.Attribute(x + "Key")!.Value, element => element.Value, StringComparer.Ordinal);
    }
}
