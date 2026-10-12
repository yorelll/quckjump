using System;
using System.IO;
using System.Threading;
using System.Linq;
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
            var strategy = root.Children.OfType<Grid>().Single(grid => grid.Children.OfType<TextBlock>().Any(text => text.Text == "How to navigate to the path"));
            return (DockPanel.GetDock(strategy), root.Children[^1], panel.EntriesGrid);
        });

        Assert.Equal(Dock.Top, layout.Item1);
        Assert.Same(layout.Item3, layout.Item2);
    }

    [Fact]
    public void Panel_ExposesCustomEntryManagementColumns()
    {
        var details = RunOnSta(() =>
        {
            var panel = CreatePanel();
            var grid = panel.EntriesGrid;
            return (grid.SelectionUnit, grid.SelectionMode, grid.IsReadOnly, grid.Columns.Select(column => column.Header.ToString()).ToArray(), panel.EntryRows.Single().Enabled);
        });

        Assert.Equal(DataGridSelectionUnit.FullRow, details.Item1);
        Assert.Equal(DataGridSelectionMode.Single, details.Item2);
        Assert.True(details.Item3);
        Assert.Equal(new[] { "Alias", "Path", "Type", "Enabled" }, details.Item4);
        Assert.True(details.Item5);
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
