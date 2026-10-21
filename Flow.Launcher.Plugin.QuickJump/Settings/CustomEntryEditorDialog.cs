using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Editable custom entry window whose persistent controls resolve Flow language resources dynamically.</summary>
internal sealed class CustomEntryEditorDialog : Window, ILocalizedControl
{
    private readonly CustomEntryRepository _repository;
    private readonly string? _originalAlias;
    private readonly CustomEntryType? _originalType;
    private readonly TextBox _alias;
    private readonly TextBox _path;
    private readonly TextBlock _detectedType;
    private readonly CheckBox _enabled;
    private readonly TextBlock _error;
    private readonly Button _save;

    public CustomEntryEditorDialog(CustomEntryRepository repository, CustomEntry? existing = null, string? initialPath = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _originalAlias = existing?.Alias;
        _originalType = existing?.Type;
        string currentPath = existing?.Path ?? initialPath ?? string.Empty;
        var initialType = existing?.Type ?? DetectExistingType(currentPath);
        string defaultAlias = existing?.Alias ?? CreateInitialAlias(currentPath, initialType);

        SetResourceReference(TitleProperty, existing is null ? "quickjump_editor_add_title" : "quickjump_editor_edit_title");
        Width = 600;
        Height = 390;
        MinWidth = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(20) };
        for (int index = 0; index < 9; index++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(Label("quickjump_editor_alias", 0));
        _alias = Input(defaultAlias, 1);
        _alias.SetResourceReference(ToolTipProperty, "quickjump_editor_alias_tooltip");
        _alias.KeyDown += (_, args) => { if (args.Key == System.Windows.Input.Key.Enter) Save(); };
        root.Children.Add(_alias);

        root.Children.Add(Label("quickjump_editor_path", 2));
        var pathPanel = new DockPanel { LastChildFill = true };
        Grid.SetRow(pathPanel, 3);
        var chooseFolder = LocalizedButton("quickjump_editor_choose_folder");
        chooseFolder.Click += (_, _) => ChooseFolder();
        DockPanel.SetDock(chooseFolder, Dock.Right);
        pathPanel.Children.Add(chooseFolder);
        var chooseFile = LocalizedButton("quickjump_editor_choose_file");
        chooseFile.Click += (_, _) => ChooseFile();
        DockPanel.SetDock(chooseFile, Dock.Right);
        pathPanel.Children.Add(chooseFile);
        _path = new TextBox { Text = currentPath, Padding = new Thickness(6, 4, 6, 4) };
        _path.TextChanged += (_, _) => UpdateDetectedType();
        pathPanel.Children.Add(_path);
        root.Children.Add(pathPanel);

        root.Children.Add(Label("quickjump_editor_detected_type", 4));
        _detectedType = new TextBlock { Margin = new Thickness(0, 5, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(_detectedType, 5);
        root.Children.Add(_detectedType);

        _enabled = new CheckBox { IsChecked = existing?.Enabled ?? true, Margin = new Thickness(0, 3, 0, 0) };
        _enabled.SetResourceReference(ContentControl.ContentProperty, "quickjump_editor_enabled");
        Grid.SetRow(_enabled, 6);
        root.Children.Add(_enabled);

        _error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(_error, 7);
        root.Children.Add(_error);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = LocalizedButton("quickjump_editor_cancel");
        cancel.IsCancel = true;
        _save = LocalizedButton("quickjump_editor_save");
        _save.IsDefault = true;
        _save.Click += (_, _) => Save();
        footer.Children.Add(cancel);
        footer.Children.Add(_save);
        Grid.SetRow(footer, 8);
        root.Children.Add(footer);
        Content = root;
        UpdateDetectedType();
        LocalizedControlRegistry.Register(this);
        Loaded += (_, _) => { _alias.Focus(); _alias.SelectAll(); };
    }

    internal Grid LayoutGrid => (Grid)Content;
    internal TextBox AliasInput => _alias;
    internal TextBox PathInput => _path;
    internal TextBlock DetectedTypeText => _detectedType;
    internal TextBlock ErrorText => _error;
    internal Button SaveButton => _save;

    internal bool SavedSuccessfully { get; private set; }

    /// <summary>Set by the context-menu presenter so saving closes a modeless editor.</summary>
    internal bool IsModelessPresentation { get; set; }

    public void RefreshLocalizedValues() => UpdateDetectedType();

    private static TextBlock Label(string resourceKey, int row)
    {
        var label = new TextBlock { Margin = new Thickness(0, 8, 0, 3) };
        label.SetResourceReference(TextBlock.TextProperty, resourceKey);
        Grid.SetRow(label, row);
        return label;
    }

    private static TextBox Input(string text, int row)
    {
        var input = new TextBox { Text = text, Padding = new Thickness(6, 4, 6, 4) };
        Grid.SetRow(input, row);
        return input;
    }

    private static Button LocalizedButton(string resourceKey)
    {
        var button = new Button { MinWidth = 90, Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        button.SetResourceReference(ContentControl.ContentProperty, resourceKey);
        return button;
    }

    private void ChooseFile()
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, Filter = QuickJumpText.AllFilesFilter(), Title = QuickJumpText.ChooseFileTitle() };
        if (dialog.ShowDialog(this) == true) _path.Text = dialog.FileName;
    }

    private void ChooseFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = QuickJumpText.ChooseFolderTitle(), UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) _path.Text = dialog.SelectedPath;
    }

    private void UpdateDetectedType()
    {
        _detectedType.Text = PathTypeDetector.TryDetect(_path.Text, out var type, out _)
            ? QuickJumpText.Type(type)
            : QuickJumpText.DetectedInvalid();
    }

    private void Save()
    {
        if (!PathTypeDetector.TryDetect(_path.Text, out var type, out var typeError))
        {
            _error.Text = QuickJumpText.Error(typeError);
            return;
        }
        bool success = _originalAlias is null || _originalType is null
            ? _repository.TryAdd(_alias.Text, _path.Text, type, _enabled.IsChecked == true, out var error)
            : _repository.TryUpdate(_originalAlias, _originalType.Value, _alias.Text, _path.Text, type, _enabled.IsChecked == true, out error);
        if (!success)
        {
            _error.Text = QuickJumpText.Error(error);
            return;
        }
        SavedSuccessfully = true;
        if (IsVisible)
        {
            if (IsModelessPresentation)
            {
                Close();
            }
            else
            {
                DialogResult = true;
            }
        }
    }

    private string CreateInitialAlias(string path, CustomEntryType type) => string.IsNullOrWhiteSpace(path) ? QuickJumpText.AliasFallback() : AliasGenerator.CreateDefault(path, type, _repository.GetAll());
    private static CustomEntryType DetectExistingType(string path) => Directory.Exists(path) ? CustomEntryType.Folder : CustomEntryType.File;
}
