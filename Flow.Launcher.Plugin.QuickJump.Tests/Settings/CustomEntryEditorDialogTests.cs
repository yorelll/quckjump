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
            var dialog = CreateDialog(); var grid = dialog.LayoutGrid; var enabled = grid.Children.OfType<CheckBox>().Single(); var footer = grid.Children.OfType<StackPanel>().Single(panel => panel.Children.OfType<Button>().Count() == 2);
            return (grid.RowDefinitions.Count, Grid.GetRow(enabled), Grid.GetRow(dialog.ErrorText), Grid.GetRow(footer));
        });
        Assert.True(layout.Item1 >= 9); Assert.NotEqual(layout.Item2, layout.Item3); Assert.NotEqual(layout.Item2, layout.Item4); Assert.NotEqual(layout.Item3, layout.Item4);
    }

    [Fact]
    public void Dialog_UsesDynamicResourcesForPersistentTextAndNoEditableTypeSelector()
    {
        var details = RunOnSta(() =>
        {
            var dialog = CreateDialog();
            var buttons = dialog.LayoutGrid.Children.OfType<DockPanel>().SelectMany(panel => panel.Children.OfType<Button>()).ToArray();
            return (buttons.Select(button => DependencyPropertyHelper.GetValueSource(button, ContentControl.ContentProperty).IsExpression).ToArray(), dialog.LayoutGrid.Children.OfType<ComboBox>().Count(), DependencyPropertyHelper.GetValueSource(dialog.TitleProperty).IsExpression);
        });
        Assert.All(details.Item1, Assert.True); Assert.Equal(2, details.Item1.Length); Assert.Equal(0, details.Item2); Assert.True(details.Item3);
    }

    [Fact]
    public void Dialog_PathEditRedetectsFolderAndSavePersistsDetectedType()
    {
        var details = RunOnSta(() =>
        {
            var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings(); var repository = new CustomEntryRepository(settings, () => { }); var dialog = new CustomEntryEditorDialog(repository, initialPath: CreateFile());
            string folder = Path.Combine(_root, "folder"); Directory.CreateDirectory(folder); dialog.PathInput.Text = folder; dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return settings.CustomEntries.Single().Type;
        });
        Assert.Equal(CustomEntryType.Folder, details);
    }

    [Fact]
    public void Dialog_MissingPathShowsLocalizedErrorAndDoesNotSave()
    {
        var details = RunOnSta(() =>
        {
            var settings = new Flow.Launcher.Plugin.QuickJump.Settings.Settings(); var dialog = new CustomEntryEditorDialog(new CustomEntryRepository(settings, () => { }), initialPath: CreateFile());
            dialog.PathInput.Text = Path.Combine(_root, "missing"); dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return (dialog.ErrorText.Text, settings.CustomEntries.Count, dialog.DialogResult);
        });
        Assert.NotEmpty(details.Item1); Assert.Equal(0, details.Item2); Assert.Null(details.Item3);
    }

    [Fact]
    public void Dialog_InvalidAliasRevealsLocalizedErrorWithoutClosing()
    {
        var details = RunOnSta(() =>
        {
            var dialog = CreateDialog(); dialog.AliasInput.Text = "bad alias"; dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return (dialog.ErrorText.Text, dialog.ErrorText.Visibility, dialog.IsVisible, dialog.DialogResult);
        });
        Assert.NotEmpty(details.Item1); Assert.Equal(Visibility.Visible, details.Item2); Assert.False(details.Item3); Assert.Null(details.Item4);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
    private CustomEntryEditorDialog CreateDialog() => new(new CustomEntryRepository(new Flow.Launcher.Plugin.QuickJump.Settings.Settings(), () => { }), initialPath: CreateFile());
    private string CreateFile() { Directory.CreateDirectory(_root); string file = Path.Combine(_root, "entry.txt"); File.WriteAllText(file, "quickjump"); return file; }
    private static T RunOnSta<T>(Func<T> callback) { T? result = default; Exception? exception = null; var thread = new Thread(() => { try { result = callback(); } catch (Exception error) { exception = error; } }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (exception is not null) throw exception; return result!; }
}
