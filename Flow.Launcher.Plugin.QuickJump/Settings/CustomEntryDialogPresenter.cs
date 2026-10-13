using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Diagnostics;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Shows one reusable modeless custom-entry editor without blocking Flow's settings window.</summary>
internal sealed class CustomEntryDialogPresenter
{
    private readonly Func<Dispatcher?> _dispatcher;
    private readonly Func<Window?> _activeWindow;
    private readonly Func<CustomEntryRepository, string, CustomEntryEditorDialog> _createDialog;
    private readonly Action<Action> _schedule;
    private CustomEntryEditorDialog? _activeDialog;

    public CustomEntryDialogPresenter(
        Func<Dispatcher?>? dispatcher = null,
        Func<Window?>? activeWindow = null,
        Func<CustomEntryRepository, string, CustomEntryEditorDialog>? createDialog = null,
        Action<Action>? schedule = null)
    {
        _dispatcher = dispatcher ?? (() => Application.Current?.Dispatcher);
        _activeWindow = activeWindow ?? (() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive));
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
            QuickJumpDiagnostics.Warn("editor schedule aborted: dispatcher unavailable");
            return;
        }

        QuickJumpDiagnostics.Info($"editor schedule dispatcherThread={dispatcher.Thread.ManagedThreadId}");
        _schedule(() => ShowCore(repository, path, saved));
    }

    internal void ShowCore(CustomEntryRepository repository, string path, Action? saved)
    {
        try
        {
            var sessionAction = CustomEntryDialogSessionPlan.Decide(_activeDialog is { IsLoaded: true, IsVisible: true });
            if (sessionAction == CustomEntryDialogSessionAction.ReuseAndActivate)
            {
                QuickJumpDiagnostics.Info("editor reuse existing visible dialog");
                BringToFront(_activeDialog!);
                return;
            }

            var owner = _activeWindow();
            var plan = CustomEntryDialogPresentationPlan.Create(owner is { IsVisible: true, IsEnabled: true });
            QuickJumpDiagnostics.Info($"editor create ownerVisibleEnabled={plan.UseOwner} modeless={plan.IsModeless} center={plan.CenterOnPrimaryScreen} temporaryTopmost={plan.TemporarilyTopmost}");

            var dialog = _createDialog(repository, path);
            dialog.IsModelessPresentation = true;
            _activeDialog = dialog;
            if (plan.UseOwner)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                dialog.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - dialog.Width) / 2);
                dialog.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - dialog.Height) / 2);
                dialog.Topmost = plan.TemporarilyTopmost;
            }

            dialog.Loaded += (_, _) =>
            {
                QuickJumpDiagnostics.Info($"editor loaded ownerAssigned={dialog.Owner is not null} topmost={dialog.Topmost}");
                BringToFront(dialog);
            };
            dialog.ContentRendered += (_, _) =>
            {
                QuickJumpDiagnostics.Info("editor content-rendered");
                // The temporary topmost elevation is released only after WPF
                // rendered the editor. The window remains normally movable.
                if (plan.TemporarilyTopmost)
                {
                    dialog.Topmost = false;
                    QuickJumpDiagnostics.Info("editor temporary-topmost released");
                }
            };
            dialog.Activated += (_, _) => QuickJumpDiagnostics.Info("editor activated");
            dialog.Closed += (_, _) =>
            {
                QuickJumpDiagnostics.Info($"editor closed saved={dialog.SavedSuccessfully}");
                if (ReferenceEquals(_activeDialog, dialog))
                {
                    _activeDialog = null;
                }
                if (CustomEntryDialogSessionPlan.InvokeSavedCallbackAfterClose(dialog.SavedSuccessfully))
                {
                    saved?.Invoke();
                }
            };

            // Never use ShowDialog here: it disables an assigned owner and can
            // block the Flow settings dispatcher. Context-menu editing is a
            // guarded modeless window by design.
            dialog.Show();
            QuickJumpDiagnostics.Info("editor show returned modeless");
        }
        catch (Exception exception)
        {
            QuickJumpDiagnostics.Exception("editor presentation", exception);
            _activeDialog = null;
        }
    }

    private static void BringToFront(Window dialog)
    {
        dialog.Activate();
        dialog.Focus();
        // Reassert only long enough to request foreground activation. Do not
        // leave user-facing windows permanently Topmost.
        if (!dialog.IsActive)
        {
            dialog.Topmost = true;
            dialog.Activate();
            dialog.Topmost = false;
        }
    }
}
