using System;
using System.Windows;
using System.Windows.Threading;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Shows one reusable, always-visible top-level error dialog on the WPF dispatcher.</summary>
internal sealed class PluginMessagePresenter
{
    private readonly Func<Dispatcher?> _dispatcher;
    private readonly Func<string, PluginMessageDialog> _createDialog;
    private readonly Action<Action> _schedule;
    private PluginMessageDialog? _activeDialog;

    public PluginMessagePresenter(
        Func<Dispatcher?>? dispatcher = null,
        Func<string, PluginMessageDialog>? createDialog = null,
        Action<Action>? schedule = null)
    {
        _dispatcher = dispatcher ?? (() => Application.Current?.Dispatcher);
        _createDialog = createDialog ?? (message => new PluginMessageDialog(message));
        _schedule = schedule ?? (action =>
        {
            var current = _dispatcher();
            if (current is not null && !current.HasShutdownStarted && !current.HasShutdownFinished)
            {
                current.BeginInvoke(DispatcherPriority.Normal, action);
            }
        });
    }

    /// <summary>True only if a dispatcher is available to present the message.</summary>
    public bool Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var dispatcher = _dispatcher();
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return false;
        }

        _schedule(() => ShowCore(message));
        return true;
    }

    internal void ShowCore(string message)
    {
        try
        {
            if (_activeDialog is { IsLoaded: true, IsVisible: true })
            {
                _activeDialog.MessageText.Text = message;
                BringToFront(_activeDialog);
                return;
            }

            var dialog = _createDialog(message);
            _activeDialog = dialog;
            dialog.Loaded += (_, _) =>
            {
                var position = PluginMessageDialog.CenterOnPrimaryScreen(dialog);
                dialog.Left = position.Left;
                dialog.Top = position.Top;
                BringToFront(dialog);
            };
            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(_activeDialog, dialog))
                {
                    _activeDialog = null;
                }
                // Topmost is only needed while the message is visible.
                dialog.Topmost = false;
            };

            // Unowned and topmost while open: it must be readable even over an
            // elevated target window. Show() keeps Flow's dispatcher responsive.
            dialog.Topmost = true;
            dialog.Show();
            BringToFront(dialog);
        }
        catch (Exception)
        {
            _activeDialog = null;
        }
    }

    private static void BringToFront(Window dialog)
    {
        if (dialog.WindowState == WindowState.Minimized)
        {
            dialog.WindowState = WindowState.Normal;
        }
        dialog.Activate();
        dialog.Focus();
    }
}
