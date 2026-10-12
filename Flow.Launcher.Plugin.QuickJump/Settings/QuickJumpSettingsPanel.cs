using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>QuickJump 的 Flow 设置界面，管理导航方式和持久化自定义条目。</summary>
internal sealed class QuickJumpSettingsPanel : UserControl
{
    private readonly Settings _settings;
    private readonly CustomEntryRepository _repository;
    private readonly Action _save;
    private readonly ObservableCollection<CustomEntryRow> _rows = [];
    private readonly DataGrid _grid;
    private readonly TextBlock _error;

    public QuickJumpSettingsPanel(Settings settings, CustomEntryRepository repository, Action save)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _save = save ?? throw new ArgumentNullException(nameof(save));

        var root = new DockPanel { Margin = new Thickness(16) };
        var title = new TextBlock
        {
            Text = "QuickJump 设置",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var strategyPanel = CreateStrategyPanel();
        DockPanel.SetDock(strategyPanel, Dock.Top);
        root.Children.Add(strategyPanel);

        _error = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.Firebrick,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8),
        };
        DockPanel.SetDock(_error, Dock.Top);
        root.Children.Add(_error);

        var section = new TextBlock
        {
            Text = "自定义跳转",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6),
        };
        DockPanel.SetDock(section, Dock.Top);
        root.Children.Add(section);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        toolbar.Children.Add(CreateButton("删除", (_, _) => DeleteSelected()));
        toolbar.Children.Add(CreateButton("编辑", (_, _) => EditSelected()));
        toolbar.Children.Add(CreateButton("新增", (_, _) => AddEntry()));
        DockPanel.SetDock(toolbar, Dock.Bottom);
        root.Children.Add(toolbar);

        _grid = CreateGrid();
        root.Children.Add(_grid);
        Content = root;
        ReloadEntries();
    }

    internal DataGrid EntriesGrid => _grid;

    internal IReadOnlyList<CustomEntryRow> EntryRows => _rows;

    internal DockPanel RootPanel => (DockPanel)Content;

    private UIElement CreateStrategyPanel()
    {
        var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var text = new TextBlock
        {
            Text = "地址栏聚焦快捷键",
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var comboBox = new ComboBox
        {
            ItemsSource = new[] { "Alt+D（默认）", "Ctrl+L（备用）" },
            SelectedItem = _settings.Strategy == Strategy.CtrlL ? "Ctrl+L（备用）" : "Alt+D（默认）",
            IsEditable = false,
            MinWidth = 130,
        };
        comboBox.DropDownClosed += (_, _) =>
        {
            _settings.Strategy = comboBox.Text.StartsWith("Ctrl+L", StringComparison.Ordinal) ? Strategy.CtrlL : Strategy.AltD;
            _save();
        };
        Grid.SetColumn(comboBox, 1);
        grid.Children.Add(comboBox);
        outer.Children.Add(grid);
        outer.Children.Add(new TextBlock
        {
            Text = "QuickJump 会先向 Windows 原生文件对话框发送所选快捷键以聚焦地址栏，再插入路径。默认使用 Alt+D；如果应用或对话框忽略 Alt+D，请改用 Ctrl+L。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = System.Windows.Media.Brushes.DimGray,
        });
        return outer;
    }

    private DataGrid CreateGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = true,
            ItemsSource = _rows,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            MinHeight = 220,
        };
        grid.Columns.Add(TextColumn("别名", nameof(CustomEntryRow.Alias), 1));
        grid.Columns.Add(TextColumn("路径", nameof(CustomEntryRow.Path), 3));
        grid.Columns.Add(TextColumn("类型", nameof(CustomEntryRow.Type), 1));
        grid.Columns.Add(EnabledColumn());
        return grid;
    }

    private static DataGridTextColumn TextColumn(string header, string property, double width) =>
        new()
        {
            Header = header,
            Binding = new Binding(property),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
        };

    private DataGridTemplateColumn EnabledColumn()
    {
        var column = new DataGridTemplateColumn { Header = "启用", Width = DataGridLength.Auto };
        var template = new DataTemplate();
        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(CustomEntryRow.Enabled))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        checkBox.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(EnabledChanged));
        template.VisualTree = checkBox;
        column.CellTemplate = template;
        return column;
    }

    private void EnabledChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not CheckBox { DataContext: CustomEntryRow row })
        {
            return;
        }

        if (!_repository.TrySetEnabled(row.Alias, row.Enabled, out var error))
        {
            ShowError(error ?? "无法更新条目状态。");
            ReloadEntries();
            return;
        }
        ClearError();
    }

    private void AddEntry()
    {
        var dialog = new CustomEntryEditorDialog(_repository) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            ReloadEntries();
        }
    }

    private void EditSelected()
    {
        if (_grid.SelectedItem is not CustomEntryRow row)
        {
            ShowError("请先选择要编辑的自定义跳转。");
            return;
        }

        var entry = _repository.GetAll().FirstOrDefault(item =>
            string.Equals(item.Alias, row.Alias, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            ShowError("所选自定义跳转已不存在。");
            ReloadEntries();
            return;
        }

        var dialog = new CustomEntryEditorDialog(_repository, entry) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            ReloadEntries();
        }
    }

    private void DeleteSelected()
    {
        if (_grid.SelectedItem is not CustomEntryRow row)
        {
            ShowError("请先选择要删除的自定义跳转。");
            return;
        }

        if (MessageBox.Show($"确定删除自定义跳转“{row.Alias}”吗？原始文件或文件夹不会被删除。", "QuickJump", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_repository.Remove(row.Alias))
        {
            ShowError("所选自定义跳转已不存在。");
            return;
        }

        ClearError();
        ReloadEntries();
    }

    private void ReloadEntries()
    {
        _rows.Clear();
        foreach (var entry in _repository.GetAll().OrderBy(entry => entry.Alias, StringComparer.CurrentCultureIgnoreCase))
        {
            _rows.Add(new CustomEntryRow(entry.Alias, entry.Path, entry.Type == CustomEntryType.File ? "文件" : "文件夹", entry.Enabled));
        }
    }

    private static Button CreateButton(string text, RoutedEventHandler handler)
    {
        var button = new Button { Content = text, Margin = new Thickness(4, 0, 0, 0), MinWidth = 82, Padding = new Thickness(8, 4, 8, 4) };
        button.Click += handler;
        return button;
    }

    private void ShowError(string error) => _error.Text = error;

    private void ClearError() => _error.Text = string.Empty;
}

/// <summary>自定义条目设置表格的显示行。</summary>
internal sealed class CustomEntryRow
{
    public CustomEntryRow(string alias, string path, string type, bool enabled)
    {
        Alias = alias;
        Path = path;
        Type = type;
        Enabled = enabled;
    }

    public string Alias { get; }

    public string Path { get; }

    public string Type { get; }

    public bool Enabled { get; set; }
}
