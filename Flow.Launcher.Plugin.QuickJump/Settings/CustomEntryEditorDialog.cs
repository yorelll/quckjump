using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Small reusable dialog for adding or editing a custom QuickJump path.</summary>
internal sealed class CustomEntryEditorDialog : Window
{
    private readonly CustomEntryRepository _repository;
    private readonly string? _originalAlias;
    private readonly TextBox _alias;
    private readonly TextBox _path;
    private readonly ComboBox _type;
    private readonly CheckBox _enabled;
    private readonly TextBlock _error;

    public CustomEntryEditorDialog(CustomEntryRepository repository, CustomEntry? existing = null, string? initialPath = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _originalAlias = existing?.Alias;
        var inferredType = existing?.Type ?? DetectType(initialPath);
        string defaultAlias = existing?.Alias ?? CreateInitialAlias(initialPath, inferredType);

        Title = existing is null ? "Add custom QuickJump entry" : "Edit custom QuickJump entry";
        Width = 560;
        Height = 315;
        MinWidth = 500;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(20) };
        for (int index = 0; index < 7; index++)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        root.Children.Add(Label("Alias", 0));
        _alias = Input(defaultAlias, 1);
        _alias.ToolTip = "Letters, numbers, hyphens, and underscores only. Press Enter to save.";
        _alias.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter)
            {
                Save();
            }
        };
        root.Children.Add(_alias);

        root.Children.Add(Label("Path", 2));
        var pathPanel = new DockPanel { LastChildFill = true };
        Grid.SetRow(pathPanel, 3);
        var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        browse.Click += (_, _) => Browse();
        DockPanel.SetDock(browse, Dock.Right);
        pathPanel.Children.Add(browse);
        _path = new TextBox { Text = existing?.Path ?? initialPath ?? string.Empty, Padding = new Thickness(6, 4, 6, 4) };
        pathPanel.Children.Add(_path);
        root.Children.Add(pathPanel);

        root.Children.Add(Label("Type", 4));
        _type = new ComboBox
        {
            ItemsSource = new[] { CustomEntryType.File, CustomEntryType.Folder },
            SelectedItem = inferredType,
            IsEditable = false,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(_type, 5);
        root.Children.Add(_type);

        _enabled = new CheckBox { Content = "Enabled", IsChecked = existing?.Enabled ?? true, Margin = new Thickness(0, 3, 0, 0) };
        Grid.SetRow(_enabled, 6);
        root.Children.Add(_enabled);

        _error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(_error, 6);
        Grid.SetColumn(_error, 1);
        root.Children.Add(_error);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 30, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 90, Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        save.Click += (_, _) => Save();
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        Grid.SetRow(footer, 6);
        Grid.SetColumn(footer, 1);
        root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) =>
        {
            _alias.Focus();
            _alias.SelectAll();
        };
    }

    private static TextBlock Label(string text, int row)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 3) };
        Grid.SetRow(label, row);
        return label;
    }

    private static TextBox Input(string text, int row)
    {
        var input = new TextBox { Text = text, Padding = new Thickness(6, 4, 6, 4) };
        Grid.SetRow(input, row);
        return input;
    }

    private void Browse()
    {
        if ((_type.SelectedItem as CustomEntryType?) == CustomEntryType.Folder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Choose a folder for the QuickJump entry",
                UseDescriptionForTitle = true,
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _path.Text = dialog.SelectedPath;
            }
            return;
        }

        var fileDialog = new OpenFileDialog { CheckFileExists = true, Filter = "All files|*.*" };
        if (fileDialog.ShowDialog(this) == true)
        {
            _path.Text = fileDialog.FileName;
        }
    }

    private void Save()
    {
        var type = _type.SelectedItem is CustomEntryType selectedType ? selectedType : CustomEntryType.File;
        bool success = _originalAlias is null
            ? _repository.TryAdd(_alias.Text, _path.Text, type, _enabled.IsChecked == true, out var error)
            : _repository.TryUpdate(_originalAlias, _alias.Text, _path.Text, type, _enabled.IsChecked == true, out error);
        if (!success)
        {
            _error.Text = error ?? "Unable to save custom entry.";
            return;
        }

        DialogResult = true;
    }

    private string CreateInitialAlias(string? path, CustomEntryType type)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "entry";
        }

        return AliasGenerator.CreateDefault(path, type, _repository.GetAll());
    }

    private static CustomEntryType DetectType(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
        {
            return CustomEntryType.Folder;
        }
        return CustomEntryType.File;
    }
}
