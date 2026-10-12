using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class CustomEntryEditorDialogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.EditorDialogTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Dialog_UsesSeparateRowsForEnabledErrorAndFooter()
    {
        var layout = RunOnSta(() =>
        {
            var dialog = CreateDialog();
            var grid = dialog.LayoutGrid;
            var enabled = grid.Children.OfType<CheckBox>().Single(control => Equals(control.Content, "Enabled"));
            var footer = grid.Children.OfType<StackPanel>().Single(panel => panel.Children.OfType<Button>().Any(button => Equals(button.Content, "Save")));
            return (
                grid.RowDefinitions.Count,
                Grid.GetRow(enabled),
                Grid.GetRow(dialog.ErrorText),
                Grid.GetRow(footer),
                Grid.GetColumn(enabled),
                Grid.GetColumn(dialog.ErrorText),
                Grid.GetColumn(footer));
        });

        Assert.True(layout.Item1 >= 9);
        Assert.NotEqual(layout.Item2, layout.Item3);
        Assert.NotEqual(layout.Item2, layout.Item4);
        Assert.NotEqual(layout.Item3, layout.Item4);
        Assert.Equal(0, layout.Item5);
        Assert.Equal(0, layout.Item6);
        Assert.Equal(0, layout.Item7);
    }

    [Fact]
    public void Dialog_InvalidAliasRevealsVisibleErrorWithoutClosing()
    {
        var details = RunOnSta(() =>
        {
            var dialog = CreateDialog();
            dialog.AliasInput.Text = "bad alias";
            dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return (dialog.ErrorText.Text, dialog.ErrorText.Visibility, dialog.IsVisible, dialog.DialogResult);
        });

        Assert.Contains("spaces", details.Item1, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Visibility.Visible, details.Item2);
        Assert.False(details.Item3);
        Assert.Null(details.Item4);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private CustomEntryEditorDialog CreateDialog()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "entry.txt");
        File.WriteAllText(file, "quickjump");
        var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings();
        return new CustomEntryEditorDialog(new CustomEntryRepository(settings, () => { }), initialPath: file);
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
