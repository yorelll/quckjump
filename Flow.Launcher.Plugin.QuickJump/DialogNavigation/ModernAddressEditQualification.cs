using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Pure structural proof required before using an Edit as a modern address bar.</summary>
internal sealed record ModernAddressEditEvidence(
    bool IsEdit,
    IReadOnlyList<string> AncestorClasses,
    bool IsOwnedByTargetDialog)
{
    public bool IsQualified() =>
        IsEdit &&
        IsOwnedByTargetDialog &&
        AncestorClasses.Count >= 4 &&
        string.Equals(AncestorClasses[0], "Edit", StringComparison.Ordinal) &&
        string.Equals(AncestorClasses[1], "ComboBox", StringComparison.Ordinal) &&
        string.Equals(AncestorClasses[2], "ComboBoxEx32", StringComparison.Ordinal) &&
        AncestorClasses.Skip(3).Any(value => string.Equals(value, "Address Band Root", StringComparison.Ordinal));
}
