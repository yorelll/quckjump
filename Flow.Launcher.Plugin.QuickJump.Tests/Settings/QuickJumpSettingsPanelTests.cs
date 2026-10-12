using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            var panel = CreatePanel(); var root = panel.RootPanel;
            var strategy = root.Children.OfType<StackPanel>().Single(stack => stack.Children.OfType<Grid>().Any());
            return (DockPanel.GetDock(strategy), root.Children[^1], panel.EntriesGrid);
        });
        Assert.Equal(Dock.Top, layout.Item1); Assert.Same(layout.Item3, layout.Item2);
    }

    [Fact]
    public void Panel_UsesDynamicResourceColumnsAndNavigationHelp()
    {
        var details = RunOnSta(() =>
        {
            var panel = CreatePanel(); var grid = panel.EntriesGrid;
            var headers = grid.Columns.Select(column => Assert.IsType<TextBlock>(column.Header)).ToArray();
            var help = panel.RootPanel.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().Single();
            return (grid.SelectionUnit, grid.SelectionMode, grid.IsReadOnly, headers.Select(header => DependencyPropertyHelper.GetValueSource(header, TextBlock.TextProperty).IsExpression).ToArray(), panel.EntryRows.Single().Enabled, panel.EntryRows.Single().Type, DependencyPropertyHelper.GetValueSource(help, TextBlock.TextProperty).IsExpression);
        });
        Assert.Equal(DataGridSelectionUnit.FullRow, details.Item1); Assert.Equal(DataGridSelectionMode.Single, details.Item2); Assert.True(details.Item3);
        Assert.All(details.Item4, Assert.True); Assert.True(details.Item5); Assert.Equal("File", details.Item6); Assert.True(details.Item7);
    }

    private QuickJumpSettingsPanel CreatePanel()
    {
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings(); string path = CreateFile();
        settings.CustomEntries.Add(new CustomEntry { Alias = "report", Path = path, Type = CustomEntryType.File, Enabled = true });
        return new QuickJumpSettingsPanel(settings, new CustomEntryRepository(settings, () => { }), () => { });
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
    private string CreateFile() { Directory.CreateDirectory(_root); string path = Path.Combine(_root, "report.txt"); File.WriteAllText(path, "quickjump"); return path; }
    private static T RunOnSta<T>(Func<T> callback)
    {
        T? result = default; Exception? exception = null; var thread = new Thread(() => { try { result = callback(); } catch (Exception error) { exception = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (exception is not null) throw exception; return result!;
    }
}
