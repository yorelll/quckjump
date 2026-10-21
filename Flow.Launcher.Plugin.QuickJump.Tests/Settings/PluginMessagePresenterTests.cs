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
    public void Dialog_TopmostIsReleasedOnClosed()
    {
        var released = RunOnSta(() =>
        {
            var dialog = new PluginMessageDialog("message") { Topmost = true };
            bool whileOpen = dialog.Topmost;
            dialog.Closed += (_, _) => dialog.Topmost = false;
            dialog.Show();
            dialog.Close();
            return (whileOpen, dialog.Topmost);
        });

        Assert.True(released.whileOpen);
        Assert.False(released.Topmost);
    }

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
