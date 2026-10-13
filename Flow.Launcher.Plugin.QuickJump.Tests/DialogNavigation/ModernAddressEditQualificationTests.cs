using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class ModernAddressEditQualificationTests
{
    [Fact]
    public void FocusedDifferentButQualifiedModernEdit_IsAccepted()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: true,
            AncestorClasses: ["Edit", "ComboBox", "ComboBoxEx32", "Address Band Root", "ReBarWindow32"],
            IsOwnedByTargetDialog: true);

        Assert.True(focused.IsQualified());
    }

    [Fact]
    public void NoCandidates_IsNotQualified()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: false,
            AncestorClasses: [],
            IsOwnedByTargetDialog: false);

        Assert.False(focused.IsQualified());
    }

    [Fact]
    public void FocusedUnorderedAddressClasses_AreRejected()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: true,
            AncestorClasses: ["Edit", "Pane", "ComboBox", "ComboBoxEx32", "Address Band Root"],
            IsOwnedByTargetDialog: true);

        Assert.False(focused.IsQualified());
    }

    [Fact]
    public void FocusedFilenameEditWithoutAddressBand_IsRejected()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: true,
            AncestorClasses: ["Edit", "ComboBox", "ComboBoxEx32", "#32770"],
            IsOwnedByTargetDialog: true);

        Assert.False(focused.IsQualified());
    }

    [Fact]
    public void FocusedUnrelatedEdit_IsRejectedEvenWhenItsClassPathLooksValid()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: true,
            AncestorClasses: ["Edit", "ComboBox", "ComboBoxEx32", "Address Band Root"],
            IsOwnedByTargetDialog: false);

        Assert.False(focused.IsQualified());
    }

    [Fact]
    public void FocusedNonEdit_IsRejected()
    {
        var focused = new ModernAddressEditEvidence(
            IsEdit: false,
            AncestorClasses: ["Button", "#32770"],
            IsOwnedByTargetDialog: true);

        Assert.False(focused.IsQualified());
    }
}
