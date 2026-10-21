using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class PluginMessagePresenterTests
{
    [Fact]
    public void Show_WithoutDispatcherIsSafeAndNotSilentlyClaimedAsShown()
    {
        var presenter = new PluginMessagePresenter(dispatcher: () => null, schedule: _ => { });

        Assert.False(presenter.Show("message"));
    }

    [Fact]
    public void Show_EmptyMessageIsIgnored()
    {
        var presenter = new PluginMessagePresenter(dispatcher: () => null, schedule: _ => { });

        Assert.False(presenter.Show("  "));
    }

    [Fact]
    public void Show_SchedulesOnProvidedUiDispatcher()
    {
        var scheduled = RunOnSta(() =>
        {
            int count = 0;
            var presenter = new PluginMessagePresenter(
                dispatcher: () => Dispatcher.CurrentDispatcher,
                schedule: _ => count++);

            bool shown = presenter.Show("message");
            return (shown, count);
        });

        Assert.True(scheduled.shown);
        Assert.Equal(1, scheduled.count);
    }

    [Fact]
    public void Dialog_UsesLocalizedTitleAndOkButtonWithoutOwner()
    {
        var details = RunOnSta(() =>
        {
            var dialog = new PluginMessageDialog("target message");
            var ok = Assert.IsType<Button>(Assert.IsType<Grid>(dialog.Content).Children[2]);
            var titleIsDynamic = DependencyPropertyHelper.GetValueSource(dialog, Window.TitleProperty).IsExpression;
            var okIsDynamic = DependencyPropertyHelper.GetValueSource(ok, ContentControl.ContentProperty).IsExpression;
            return (dialog.Owner is null, titleIsDynamic, okIsDynamic, dialog.MessageText.Text, dialog.Topmost);
        });

        Assert.True(details.Item1);
        Assert.True(details.Item2);
        Assert.True(details.Item3);
        Assert.Equal("target message", details.Item4);
        Assert.False(details.Topmost);
    }

    [Fact]
    public void ShowCore_ShowsTopmostDialogAndReusesItForNewMessages()
    {
        var details = RunOnSta(() =>
        {
            var created = new System.Collections.Generic.List<PluginMessageDialog>();
            var presenter = CreatePresenter(created);

            presenter.ShowCore("a");
            PumpDispatcher();
            var first = Assert.Single(created);
            bool visible = first.IsVisible;
            bool topmost = first.Topmost;
            string firstMessage = first.MessageText.Text;

            presenter.ShowCore("b");
            bool reused = created.Count == 1;
            string updatedMessage = first.MessageText.Text;

            return (visible, topmost, firstMessage, reused, updatedMessage);
        });

        Assert.True(details.visible);
        Assert.True(details.topmost);
        Assert.Equal("a", details.firstMessage);
        Assert.True(details.reused);
        Assert.Equal("b", details.updatedMessage);
    }

    [Fact]
    public void ShowCore_ReleasesTopmostOnCloseAndCreatesNewDialogAfterwards()
    {
        var details = RunOnSta(() =>
        {
            var created = new System.Collections.Generic.List<PluginMessageDialog>();
            var presenter = CreatePresenter(created);

            presenter.ShowCore("a");
            PumpDispatcher();
            var first = Assert.Single(created);
            first.Close();
            PumpDispatcher();
            bool released = !first.Topmost;

            presenter.ShowCore("c");
            PumpDispatcher();
            bool createdNew = created.Count == 2 && !ReferenceEquals(created[0], created[1]);
            var second = created[1];
            bool secondTopmost = second.Topmost;
            bool secondVisible = second.IsVisible;
            second.Close();

            return (released, createdNew, secondTopmost, secondVisible);
        });

        Assert.True(details.released);
        Assert.True(details.createdNew);
        Assert.True(details.secondTopmost);
        Assert.True(details.secondVisible);
    }

    private static PluginMessagePresenter CreatePresenter(System.Collections.Generic.List<PluginMessageDialog> created) =>
        new(
            dispatcher: () => Dispatcher.CurrentDispatcher,
            createDialog: message =>
            {
                var dialog = new PluginMessageDialog(message);
                created.Add(dialog);
                return dialog;
            },
            schedule: action => action());

    /// <summary>Lets WPF process Loaded/Closed callbacks without blocking the test.</summary>
    private static void PumpDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static T RunOnSta<T>(Func<T> callback)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { result = callback(); }
            catch (Exception error) { exception = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) throw exception;
        return result!;
    }
}
