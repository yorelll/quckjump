using System;
using System.IO;
using System.Globalization;
using System.Threading;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Localization;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class CultureRefreshTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.CultureRefreshTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RefreshAll_UpdatesExistingSettingsRowAndOpenEditorDetectedType()
    {
        var values = RunOnSta(() =>
        {
            string file = CreateFile();
            var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
            settings.CustomEntries.Add(new CustomEntry { Alias = "report", Path = file, Type = CustomEntryType.File, Enabled = true });
            var repository = new CustomEntryRepository(settings, () => { });
            var panel = new QuickJumpSettingsPanel(settings, repository, () => { });
            var dialog = new CustomEntryEditorDialog(repository, initialPath: file);

            QuickJumpText.TypeResolverForTests = type => type == CustomEntryType.File ? "文件（测试）" : "文件夹（测试）";
            try
            {
                var shell = new global::Flow.Launcher.Plugin.QuickJump.QuickJump();
                shell.OnCultureInfoChanged(CultureInfo.GetCultureInfo("zh-CN"));
                return (panel.EntryRows[0].Type, dialog.DetectedTypeText.Text);
            }
            finally
            {
                QuickJumpText.TypeResolverForTests = null;
            }
        });

        Assert.Equal("文件（测试）", values.Item1);
        Assert.Equal("文件（测试）", values.Item2);
    }

    public void Dispose()
    {
        QuickJumpText.TypeResolverForTests = null;
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string CreateFile()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "entry.txt");
        File.WriteAllText(path, "quickjump");
        return path;
    }

    private static T RunOnSta<T>(Func<T> callback)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() => { try { result = callback(); } catch (Exception error) { exception = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) throw exception;
        return result!;
    }
}
