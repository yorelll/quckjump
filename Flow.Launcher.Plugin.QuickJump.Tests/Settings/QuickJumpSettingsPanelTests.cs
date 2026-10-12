using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class QuickJumpSettingsPanelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.SettingsPanelTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Panel_StrategyPanelDocksTopAndEntriesGridIsFinalFillChild()
    {
        var layout = RunOnSta(() =>
        {
            var panel = CreatePanel();
            var root = panel.RootPanel;
            var strategy = root.Children.OfType<StackPanel>().Single(stack => stack.Children.OfType<TextBlock>().Any(text => text.Text == "QuickJump 会先向 Windows 原生文件对话框发送所选快捷键以聚焦地址栏，再插入路径。默认使用 Alt+D；如果应用或对话框忽略 Alt+D，请改用 Ctrl+L。"));
            return (DockPanel.GetDock(strategy), root.Children[^1], panel.EntriesGrid);
        });

        Assert.Equal(Dock.Top, layout.Item1);
        Assert.Same(layout.Item3, layout.Item2);
    }

    [Fact]
    public void Panel_UsesChineseColumnsAndChineseNavigationHelp()
    {
        var details = RunOnSta(() =>
        {
            var panel = CreatePanel();
            var grid = panel.EntriesGrid;
            var help = panel.RootPanel.Children
                .OfType<StackPanel>()
                .SelectMany(stack => stack.Children.OfType<TextBlock>())
                .Single(text => text.Text.Contains("原生文件对话框", StringComparison.Ordinal));
            return (grid.SelectionUnit, grid.SelectionMode, grid.IsReadOnly, grid.Columns.Select(column => column.Header.ToString()).ToArray(), panel.EntryRows.Single().Enabled, panel.EntryRows.Single().Type, help.Text);
        });

        Assert.Equal(DataGridSelectionUnit.FullRow, details.Item1);
        Assert.Equal(DataGridSelectionMode.Single, details.Item2);
        Assert.True(details.Item3);
        Assert.Equal(new[] { "别名", "路径", "类型", "启用" }, details.Item4);
        Assert.True(details.Item5);
        Assert.Equal("文件", details.Item6);
        Assert.Contains("Alt+D", details.Item7);
        Assert.Contains("Ctrl+L", details.Item7);
    }

    private QuickJumpSettingsPanel CreatePanel()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
        var path = CreateFile();
        settings.CustomEntries.Add(new CustomEntry { Alias = "report", Path = path, Type = CustomEntryType.File, Enabled = true });
        return new QuickJumpSettingsPanel(settings, new CustomEntryRepository(settings, () => { }), () => { });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateFile()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "report.txt");
        File.WriteAllText(path, "quickjump");
        return path;
    }

    private static T RunOnSta<T>(Func<T> callback)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = callback();
            }
            catch (Exception error)
            {
                exception = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw exception;
        }
        return result!;
    }
}
