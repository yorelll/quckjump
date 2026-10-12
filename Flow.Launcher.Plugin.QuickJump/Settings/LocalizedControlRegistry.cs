using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace Flow.Launcher.Plugin.QuickJump.Settings;

/// <summary>Tracks live settings/editor controls weakly and refreshes materialized localized values.</summary>
internal static class LocalizedControlRegistry
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<ILocalizedControl>> Controls = [];

    public static void Register(ILocalizedControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (Gate)
        {
            Controls.RemoveAll(reference => !reference.TryGetTarget(out _));
            Controls.Add(new WeakReference<ILocalizedControl>(control));
        }
    }

    public static void RefreshAll()
    {
        ILocalizedControl[] controls;
        lock (Gate)
        {
            Controls.RemoveAll(reference => !reference.TryGetTarget(out _));
            controls = Controls.Select(reference => { reference.TryGetTarget(out var control); return control; }).Where(control => control is not null).Cast<ILocalizedControl>().ToArray();
        }

        foreach (var control in controls)
        {
            var dispatcher = control.Dispatcher;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                continue;
            }
            if (dispatcher.CheckAccess())
            {
                control.RefreshLocalizedValues();
            }
            else
            {
                dispatcher.BeginInvoke(DispatcherPriority.Normal, control.RefreshLocalizedValues);
            }
        }
    }
}

/// <summary>Implemented by controls containing localized strings materialized into plain CLR properties.</summary>
internal interface ILocalizedControl
{
    Dispatcher Dispatcher { get; }

    void RefreshLocalizedValues();
}
