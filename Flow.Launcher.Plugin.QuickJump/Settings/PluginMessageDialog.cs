using System;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.QuickJump.Localization;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>
/// Guaranteed-visible, user-dismissible error window. It is an unowned
/// top-level window: Flow may hide its search window and an elevated target
/// dialog may cover the desktop, so neither can be used as an owner.
/// </summary>
internal sealed class PluginMessageDialog : Window
{
    private readonly TextBlock _message;

    public PluginMessageDialog(string message)
    {
        SetResourceReference(TitleProperty, "quickjump_dialog_title");
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MinHeight = 170;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
        heading.SetResourceReference(TextBlock.TextProperty, "quickjump_dialog_title");
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        _message = new TextBlock { Text = message ?? string.Empty, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(_message, 1);
        root.Children.Add(_message);

        var ok = new Button { MinWidth = 96, Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), IsDefault = true, IsCancel = true };
        ok.SetResourceReference(ContentControl.ContentProperty, "quickjump_dialog_ok");
        ok.Click += (_, _) => Close();
        Grid.SetRow(ok, 2);
        root.Children.Add(ok);

        Content = root;
    }

    internal TextBlock MessageText => _message;

    internal static (double Left, double Top) GetCenteredPosition(double width, double height) =>
        (Math.Max(0, (SystemParameters.PrimaryScreenWidth - width) / 2),
         Math.Max(0, (SystemParameters.PrimaryScreenHeight - height) / 2));

    internal static (double Left, double Top) CenterOnPrimaryScreen(Window dialog) =>
        GetCenteredPosition(dialog.ActualWidth, dialog.ActualHeight);
}
