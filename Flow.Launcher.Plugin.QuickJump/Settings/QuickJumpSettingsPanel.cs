using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Flow settings UI for navigation strategy and persistent custom entries.</summary>
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
            Text = "QuickJump settings",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        root.Children.Add(CreateStrategyPanel());

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
            Text = "Custom entries",
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
        toolbar.Children.Add(CreateButton("Delete", (_, _) => DeleteSelected()));
        toolbar.Children.Add(CreateButton("Edit", (_, _) => EditSelected()));
        toolbar.Children.Add(CreateButton("Add", (_, _) => AddEntry()));
        DockPanel.SetDock(toolbar, Dock.Bottom);
        root.Children.Add(toolbar);

        _grid = CreateGrid();
        root.Children.Add(_grid);
        Content = root;
        ReloadEntries();
    }

    internal DataGrid EntriesGrid => _grid;

    internal IReadOnlyList<CustomEntryRow> EntryRows => _rows;

    private Control CreateStrategyPanel()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var text = new TextBlock
        {
            Text = "How to navigate to the path",
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var comboBox = new ComboBox
        {
            ItemsSource = new[] { "Alt-D", "Ctrl-L" },
            SelectedItem = _settings.Strategy == Strategy.CtrlL ? "Ctrl-L" : "Alt-D",
            IsEditable = false,
            MinWidth = 100,
        };
        comboBox.DropDownClosed += (_, _) =>
        {
            _settings.Strategy = comboBox.Text == "Ctrl-L" ? Strategy.CtrlL : Strategy.AltD;
            _save();
        };
        Grid.SetColumn(comboBox, 1);
        grid.Children.Add(comboBox);
        return grid;
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
        grid.Columns.Add(TextColumn("Alias", nameof(CustomEntryRow.Alias), 1));
        grid.Columns.Add(TextColumn("Path", nameof(CustomEntryRow.Path), 3));
        grid.Columns.Add(TextColumn("Type", nameof(CustomEntryRow.Type), 1));
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
        var column = new DataGridTemplateColumn { Header = "Enabled", Width = DataGridLength.Auto };
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
            ShowError(error ?? "Unable to update entry state.");
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
            ShowError("Select a custom entry to edit.");
            return;
        }

        var entry = _repository.GetAll().FirstOrDefault(item =>
            string.Equals(item.Alias, row.Alias, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            ShowError("The selected custom entry no longer exists.");
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
            ShowError("Select a custom entry to delete.");
            return;
        }

        if (MessageBox.Show($"Delete custom entry '{row.Alias}'? The original path will not be deleted.", "QuickJump", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_repository.Remove(row.Alias))
        {
            ShowError("The selected custom entry no longer exists.");
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
            _rows.Add(new CustomEntryRow(entry.Alias, entry.Path, entry.Type.ToString(), entry.Enabled));
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

/// <summary>Display-only row for a custom-entry settings grid.</summary>
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
