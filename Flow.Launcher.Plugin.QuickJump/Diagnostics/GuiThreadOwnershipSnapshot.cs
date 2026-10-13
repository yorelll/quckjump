using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickJump.Diagnostics;

/// <summary>Immutable, content-free window/thread ownership evidence for a native dialog focus attempt.</summary>
internal sealed record GuiThreadOwnershipSnapshot(
    nint TopDialogHandle,
    uint TopDialogThread,
    uint TopDialogProcess,
    nint AddressEditHandle,
    uint AddressEditThread,
    uint AddressEditProcess,
    nint FocusedHandle,
    uint FocusedThread,
    uint FocusedProcess,
    bool AddressStructureQualified,
    bool AddressRootMatchesDialog);

/// <summary>Pure formatting and deduplication rules for GUI-thread evidence.</summary>
internal static class GuiThreadOwnershipPlan
{
    public static IReadOnlyList<uint> DistinctThreads(GuiThreadOwnershipSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new[] { snapshot.TopDialogThread, snapshot.AddressEditThread, snapshot.FocusedThread }
            .Where(thread => thread != 0)
            .Distinct()
            .ToArray();
    }

    public static string Interpret(GuiThreadOwnershipSnapshot snapshot) =>
        !snapshot.AddressStructureQualified || !snapshot.AddressRootMatchesDialog
            ? "address-unqualified"
            : snapshot.TopDialogThread == snapshot.AddressEditThread
                ? "top-and-address-same-thread"
                : "top-and-address-different-threads";
}
