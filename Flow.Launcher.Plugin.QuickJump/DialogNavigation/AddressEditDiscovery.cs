using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>Classifies candidate controls without selecting arbitrary editable controls.</summary>
internal static class AddressEditDiscovery
{
    /// <summary>
    /// Address controls used by Windows common file dialogs include a ComboBoxEx
    /// parent and an Edit descendant. Other edit fields (notably filename fields)
    /// must not be treated as a navigation address bar.
    /// </summary>
    public static bool IsAddressEditPath(IReadOnlyList<string> classPath) =>
        classPath.Count >= 3 &&
        string.Equals(classPath[^1], "Edit", StringComparison.Ordinal) &&
        string.Equals(classPath[^2], "ComboBox", StringComparison.Ordinal) &&
        string.Equals(classPath[^3], "ComboBoxEx32", StringComparison.Ordinal);

    public static bool IsEligibleDialogClass(string? className) =>
        string.Equals(className, "#32770", StringComparison.Ordinal);
}
