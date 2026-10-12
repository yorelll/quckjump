using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.Settings;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Settings;

public sealed class CustomEntryDialogPresenterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickJump.DialogPresenterTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShowForPath_SchedulesOnProvidedUiDispatcherSeam()
    {
        var scheduled = RunOnSta(() =>
        {
            var repository = CreateRepository();
            int count = 0;
            var presenter = new CustomEntryDialogPresenter(
                dispatcher: () => Dispatcher.CurrentDispatcher,
                schedule: _ => count++);
            presenter.ShowForPath(repository, CreateFile());
            return count;
        });

        Assert.Equal(1, scheduled);
    }

    [Fact]
    public void ShowForPath_NoDispatcherDoesNotScheduleOrCreateWindow()
    {
        var repository = CreateRepository();
        int scheduled = 0;
        var presenter = new CustomEntryDialogPresenter(
            dispatcher: () => null,
            schedule: _ => scheduled++);

        presenter.ShowForPath(repository, CreateFile());

        Assert.Equal(0, scheduled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static T RunOnSta<T>(Func<T> callback)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = callback();
            }
            catch (Exception error)
            {
                exception = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw exception;
        }
        return result!;
    }

    private CustomEntryRepository CreateRepository() =>
        new(new Flow.Launcher.Plugin.QuickJump.Settings.Settings(), () => { });

    private string CreateFile()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "entry.txt");
        File.WriteAllText(path, "quickjump");
        return path;
    }
}
