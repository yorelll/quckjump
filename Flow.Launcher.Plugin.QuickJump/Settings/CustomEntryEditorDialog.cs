using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>用于新增或编辑自定义快速跳转条目的对话框。</summary>
internal sealed class CustomEntryEditorDialog : Window
{
    private readonly CustomEntryRepository _repository;
    private readonly string? _originalAlias;
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
        string currentPath = existing?.Path ?? initialPath ?? string.Empty;
        var initialType = existing?.Type ?? DetectExistingType(currentPath);
        string defaultAlias = existing?.Alias ?? CreateInitialAlias(currentPath, initialType);

        Title = existing is null ? "新增自定义跳转" : "编辑自定义跳转";
        Width = 600;
        Height = 390;
        MinWidth = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(20) };
        for (int index = 0; index < 9; index++)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        root.Children.Add(Label("别名", 0));
        _alias = Input(defaultAlias, 1);
        _alias.ToolTip = "只能包含字母、数字、连字符和下划线；按 Enter 保存。";
        _alias.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter)
            {
                Save();
            }
        };
        root.Children.Add(_alias);

        root.Children.Add(Label("路径", 2));
        var pathPanel = new DockPanel { LastChildFill = true };
        Grid.SetRow(pathPanel, 3);
        var chooseFolder = new Button { Content = "选择文件夹…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        chooseFolder.Click += (_, _) => ChooseFolder();
        DockPanel.SetDock(chooseFolder, Dock.Right);
        pathPanel.Children.Add(chooseFolder);
        var chooseFile = new Button { Content = "选择文件…", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        chooseFile.Click += (_, _) => ChooseFile();
        DockPanel.SetDock(chooseFile, Dock.Right);
        pathPanel.Children.Add(chooseFile);
        _path = new TextBox { Text = currentPath, Padding = new Thickness(6, 4, 6, 4) };
        _path.TextChanged += (_, _) => UpdateDetectedType();
        pathPanel.Children.Add(_path);
        root.Children.Add(pathPanel);

        root.Children.Add(Label("自动识别类型", 4));
        _detectedType = new TextBlock { Margin = new Thickness(0, 5, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(_detectedType, 5);
        root.Children.Add(_detectedType);

        _enabled = new CheckBox { Content = "启用", IsChecked = existing?.Enabled ?? true, Margin = new Thickness(0, 3, 0, 0) };
        Grid.SetRow(_enabled, 6);
        root.Children.Add(_enabled);

        _error = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.Firebrick,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(_error, 7);
        root.Children.Add(_error);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 90, Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        _save = new Button { Content = "保存", IsDefault = true, MinWidth = 90, Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };
        _save.Click += (_, _) => Save();
        footer.Children.Add(cancel);
        footer.Children.Add(_save);
        Grid.SetRow(footer, 8);
        root.Children.Add(footer);

        Content = root;
        UpdateDetectedType();
        Loaded += (_, _) =>
        {
            _alias.Focus();
            _alias.SelectAll();
        };
    }

    internal Grid LayoutGrid => (Grid)Content;

    internal TextBox AliasInput => _alias;

    internal TextBox PathInput => _path;

    internal TextBlock DetectedTypeText => _detectedType;

    internal TextBlock ErrorText => _error;

    internal Button SaveButton => _save;

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

    private void ChooseFile()
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, Filter = "所有文件|*.*", Title = "选择要快速跳转的文件" };
        if (dialog.ShowDialog(this) == true)
        {
            _path.Text = dialog.FileName;
        }
    }

    private void ChooseFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要快速跳转的文件夹",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _path.Text = dialog.SelectedPath;
        }
    }

    private void UpdateDetectedType()
    {
        _detectedType.Text = PathTypeDetector.TryDetect(_path.Text, out var type, out _)
            ? type == CustomEntryType.File ? "文件" : "文件夹"
            : "未找到有效的文件或文件夹";
    }

    private void Save()
    {
        if (!PathTypeDetector.TryDetect(_path.Text, out var type, out var typeError))
        {
            _error.Text = typeError ?? "无法识别路径类型。";
            return;
        }

        bool success = _originalAlias is null
            ? _repository.TryAdd(_alias.Text, _path.Text, type, _enabled.IsChecked == true, out var error)
            : _repository.TryUpdate(_originalAlias, _alias.Text, _path.Text, type, _enabled.IsChecked == true, out error);
        if (!success)
        {
            _error.Text = error ?? "无法保存自定义跳转。";
            return;
        }

        DialogResult = true;
    }

    private string CreateInitialAlias(string path, CustomEntryType type)
    {
        return string.IsNullOrWhiteSpace(path)
            ? "条目"
            : AliasGenerator.CreateDefault(path, type, _repository.GetAll());
    }

    private static CustomEntryType DetectExistingType(string path) =>
        Directory.Exists(path) ? CustomEntryType.Folder : CustomEntryType.File;
}
