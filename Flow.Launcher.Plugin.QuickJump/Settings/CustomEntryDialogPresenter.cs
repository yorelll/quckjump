using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>在 WPF UI 线程中安全显示右键新增自定义跳转窗口。</summary>
internal sealed class CustomEntryDialogPresenter
{
    private readonly Func<Dispatcher?> _dispatcher;
    private readonly Func<Window?> _activeWindow;
    private readonly Func<CustomEntryRepository, string, CustomEntryEditorDialog> _createDialog;
    private readonly Action<Action> _schedule;

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
            return;
        }

        _schedule(() => ShowCore(repository, path, saved));
    }

    internal void ShowCore(CustomEntryRepository repository, string path, Action? saved)
    {
        var owner = _activeWindow();
        var plan = CustomEntryDialogPresentationPlan.Create(owner is { IsVisible: true, IsEnabled: true });
        var dialog = _createDialog(repository, path);
        if (plan.UseOwner)
        {
            dialog.Owner = owner;
        }
        else
        {
            // Do not rely on a hidden Flow window as an owner. Place the
            // top-level dialog explicitly at the primary-screen center.
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - dialog.Width) / 2);
            dialog.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - dialog.Height) / 2);
            dialog.Topmost = plan.TemporarilyTopmost;
        }

        try
        {
            if (dialog.ShowDialog() == true)
            {
                saved?.Invoke();
            }
        }
        finally
        {
            // Never leave a modeless/top-level dialog permanently above the
            // user's applications after the modal lifetime has ended.
            dialog.Topmost = false;
        }
    }
}
