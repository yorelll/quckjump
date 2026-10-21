using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>
/// Shows one reusable, top-level modeless custom-entry editor. The editor is
/// deliberately never owned by Flow's main window: Flow hides that window after
/// a context action, which would hide an owned editor with it.
/// </summary>
internal sealed class CustomEntryDialogPresenter
{
    private readonly Func<Dispatcher?> _dispatcher;
    private readonly Func<CustomEntryRepository, string, CustomEntryEditorDialog> _createDialog;
    private readonly Action<Action> _schedule;
    private CustomEntryEditorDialog? _activeDialog;

    public CustomEntryDialogPresenter(
        Func<Dispatcher?>? dispatcher = null,
        Func<CustomEntryRepository, string, CustomEntryEditorDialog>? createDialog = null,
        Action<Action>? schedule = null)
    {
        _dispatcher = dispatcher ?? (() => Application.Current?.Dispatcher);
        _createDialog = createDialog ?? ((repository, path) => new CustomEntryEditorDialog(repository, initialPath: path));
        _schedule = schedule ?? (action =>
        {
            var currentDispatcher = _dispatcher();
            if (currentDispatcher is not null && !currentDispatcher.HasShutdownStarted && !currentDispatcher.HasShutdownFinished)
            {
                currentDispatcher.BeginInvoke(DispatcherPriority.Normal, action);
            }
        });
    }

    public void ShowForPath(CustomEntryRepository repository, string path, Action? saved = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(path);
        var dispatcher = _dispatcher();
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        _schedule(() => ShowCore(repository, path, saved));
    }

    internal void ShowCore(CustomEntryRepository repository, string path, Action? saved)
    {
        try
        {
            var sessionAction = CustomEntryDialogSessionPlan.Decide(_activeDialog is { IsLoaded: true, IsVisible: true });
            if (sessionAction == CustomEntryDialogSessionAction.ReuseAndActivate)
            {
                BringToFront(_activeDialog!);
                return;
            }

            // Always present the context editor as an unowned top-level window.
            var plan = CustomEntryDialogPresentationPlan.Create(hasUsableUnrelatedOwner: false);
            var dialog = _createDialog(repository, path);
            dialog.IsModelessPresentation = true;
            _activeDialog = dialog;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - dialog.Width) / 2);
            dialog.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - dialog.Height) / 2);
            dialog.Topmost = plan.TemporarilyTopmost;

            dialog.Loaded += (_, _) => BringToFront(dialog);
            dialog.ContentRendered += (_, _) =>
            {
                // The editor must be visible immediately but must not remain
                // above every other application afterwards.
                if (plan.TemporarilyTopmost)
                {
                    dialog.Topmost = false;
                }
                BringToFront(dialog);
            };
            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(_activeDialog, dialog))
                {
                    _activeDialog = null;
                }
                if (CustomEntryDialogSessionPlan.InvokeSavedCallbackAfterClose(dialog.SavedSuccessfully))
                {
                    saved?.Invoke();
                }
            };

            // Modeless avoids disabling a Flow owner and keeps the plugin's UI
            // dispatcher responsive while the editor is open.
            dialog.Show();
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
