using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Flow settings UI using DynamicResource keys so labels follow Flow's current language.</summary>
internal sealed class QuickJumpSettingsPanel : UserControl, ILocalizedControl
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
        var title = LocalizedText("quickjump_settings_title", 18, FontWeights.SemiBold, new Thickness(0, 0, 0, 10));
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var strategy = CreateStrategyPanel(); DockPanel.SetDock(strategy, Dock.Top); root.Children.Add(strategy);
        _error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
        DockPanel.SetDock(_error, Dock.Top); root.Children.Add(_error);
        var section = LocalizedText("quickjump_settings_custom_entries", 16, FontWeights.SemiBold, new Thickness(0, 14, 0, 6));
        DockPanel.SetDock(section, Dock.Top); root.Children.Add(section);
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        toolbar.Children.Add(CreateButton("quickjump_button_delete", (_, _) => DeleteSelected()));
        toolbar.Children.Add(CreateButton("quickjump_button_edit", (_, _) => EditSelected()));
        toolbar.Children.Add(CreateButton("quickjump_button_add", (_, _) => AddEntry()));
        DockPanel.SetDock(toolbar, Dock.Bottom); root.Children.Add(toolbar);
        _grid = CreateGrid(); root.Children.Add(_grid); Content = root; ReloadEntries();
        LocalizedControlRegistry.Register(this);
    }

    internal DataGrid EntriesGrid => _grid;
    internal IReadOnlyList<CustomEntryRow> EntryRows => _rows;
    internal DockPanel RootPanel => (DockPanel)Content;

    public void RefreshLocalizedValues() => ReloadEntries();

    private UIElement CreateStrategyPanel()
    {
        var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var label = LocalizedText("quickjump_settings_address_shortcut", null, null, default); Grid.SetColumn(label, 0); grid.Children.Add(label);
        var combo = new ComboBox { IsEditable = false, MinWidth = 130 };
        combo.Items.Add(StrategyItem(Strategy.AltD, "quickjump_settings_alt_d"));
        combo.Items.Add(StrategyItem(Strategy.CtrlL, "quickjump_settings_ctrl_l"));
        combo.SelectedIndex = _settings.Strategy == Strategy.CtrlL ? 1 : 0;
        combo.DropDownClosed += (_, _) => { if (combo.SelectedItem is ComboBoxItem { Tag: Strategy value }) { _settings.Strategy = value; _save(); } };
        Grid.SetColumn(combo, 1); grid.Children.Add(combo); outer.Children.Add(grid);
        var help = LocalizedText("quickjump_settings_address_help", null, null, new Thickness(0, 6, 0, 0)); help.TextWrapping = TextWrapping.Wrap; help.Foreground = System.Windows.Media.Brushes.DimGray; outer.Children.Add(help);
        return outer;
    }

    private static ComboBoxItem StrategyItem(Strategy strategy, string resourceKey)
    {
        var item = new ComboBoxItem { Tag = strategy }; item.SetResourceReference(ContentControl.ContentProperty, resourceKey); return item;
    }

    private DataGrid CreateGrid()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, IsReadOnly = true, ItemsSource = _rows, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, GridLinesVisibility = DataGridGridLinesVisibility.None, MinHeight = 220 };
        grid.Columns.Add(TextColumn("quickjump_column_alias", nameof(CustomEntryRow.Alias), 1));
        grid.Columns.Add(TextColumn("quickjump_column_path", nameof(CustomEntryRow.Path), 3));
        grid.Columns.Add(TextColumn("quickjump_column_type", nameof(CustomEntryRow.Type), 1));
        grid.Columns.Add(EnabledColumn()); return grid;
    }

    private static DataGridTextColumn TextColumn(string resourceKey, string property, double width)
    {
        var header = new TextBlock(); header.SetResourceReference(TextBlock.TextProperty, resourceKey);
        return new DataGridTextColumn { Header = header, Binding = new Binding(property), Width = new DataGridLength(width, DataGridLengthUnitType.Star) };
    }

    private DataGridTemplateColumn EnabledColumn()
    {
        var header = new TextBlock(); header.SetResourceReference(TextBlock.TextProperty, "quickjump_column_enabled");
        var column = new DataGridTemplateColumn { Header = header, Width = DataGridLength.Auto }; var template = new DataTemplate(); var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(CustomEntryRow.Enabled)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); check.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(EnabledChanged)); template.VisualTree = check; column.CellTemplate = template; return column;
    }

    private void EnabledChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not CheckBox { DataContext: CustomEntryRow row }) return;
        if (!_repository.TrySetEnabled(row.Alias, row.Enabled, out var error)) { ShowError(error is null ? QuickJumpText.UpdateEnabledFailed() : QuickJumpText.Error(error)); ReloadEntries(); return; }
        ClearError();
    }
    private void AddEntry() { var dialog = new CustomEntryEditorDialog(_repository) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) ReloadEntries(); }
    private void EditSelected()
    {
        if (_grid.SelectedItem is not CustomEntryRow row) { ShowError(QuickJumpText.SelectEdit()); return; }
        var entry = _repository.GetAll().FirstOrDefault(item => string.Equals(item.Alias, row.Alias, StringComparison.OrdinalIgnoreCase));
        if (entry is null) { ShowError(QuickJumpText.EntryMissing()); ReloadEntries(); return; }
        var dialog = new CustomEntryEditorDialog(_repository, entry) { Owner = Window.GetWindow(this) }; if (dialog.ShowDialog() == true) ReloadEntries();
    }
    private void DeleteSelected()
    {
        if (_grid.SelectedItem is not CustomEntryRow row) { ShowError(QuickJumpText.SelectDelete()); return; }
        if (MessageBox.Show(QuickJumpText.DeleteConfirm(row.Alias), QuickJumpText.PluginName(), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (!_repository.Remove(row.Alias)) { ShowError(QuickJumpText.EntryMissing()); return; } ClearError(); ReloadEntries();
    }
    private void ReloadEntries() { _rows.Clear(); foreach (var entry in _repository.GetAll().OrderBy(entry => entry.Alias, StringComparer.CurrentCultureIgnoreCase)) _rows.Add(new CustomEntryRow(entry.Alias, entry.Path, QuickJumpText.Type(entry.Type), entry.Enabled)); }
    private static Button CreateButton(string key, RoutedEventHandler handler) { var button = new Button { Margin = new Thickness(4, 0, 0, 0), MinWidth = 82, Padding = new Thickness(8, 4, 8, 4) }; button.SetResourceReference(ContentControl.ContentProperty, key); button.Click += handler; return button; }
    private static TextBlock LocalizedText(string key, double? fontSize, FontWeight? weight, Thickness margin) { var text = new TextBlock { Margin = margin }; text.SetResourceReference(TextBlock.TextProperty, key); if (fontSize is not null) text.FontSize = fontSize.Value; if (weight is not null) text.FontWeight = weight.Value; return text; }
    private void ShowError(string error) => _error.Text = error;
    private void ClearError() => _error.Text = string.Empty;
}

internal sealed class CustomEntryRow
{
    public CustomEntryRow(string alias, string path, string type, bool enabled) { Alias = alias; Path = path; Type = type; Enabled = enabled; }
    public string Alias { get; } public string Path { get; } public string Type { get; } public bool Enabled { get; set; }
}
