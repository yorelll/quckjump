using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class DialogNavigationTests
{
    [Theory]
    [InlineData(true, true, "Captured")]
    [InlineData(true, false, "Captured")]
    [InlineData(false, true, "Foreground")]
    [InlineData(false, false, "None")]
    public void TargetSelection_PrefersCapturedDialogBeforeForeground(bool capturedUsable, bool foregroundUsable, string expectedName)
    {
        var expected = Enum.Parse<DialogTargetSource>(expectedName);
        Assert.Equal(expected, DialogTargetSelection.Select(capturedUsable, foregroundUsable));
    }

    [Fact]
    public void RetryPlan_IsBounded()
    {
        var plan = new DialogTargetRetryPlan(Attempts: 3, DelayMilliseconds: 25);

        Assert.True(plan.ShouldTry(0));
        Assert.True(plan.ShouldTry(2));
        Assert.False(plan.ShouldTry(3));
        Assert.False(plan.ShouldTry(-1));
    }

    [Fact]
    public void AddressClassification_AcceptsOnlyComboBoxExComboBoxEditPath()
    {
        Assert.True(AddressEditDiscovery.IsAddressEditPath(new[] { "ComboBoxEx32", "ComboBox", "Edit" }));
        Assert.False(AddressEditDiscovery.IsAddressEditPath(new[] { "Edit" }));
        Assert.False(AddressEditDiscovery.IsAddressEditPath(new[] { "ComboBoxEx32", "Edit" }));
        Assert.False(AddressEditDiscovery.IsAddressEditPath(new[] { "ComboBoxEx32", "ComboBox", "RichEdit" }));
    }

    [Theory]
    [InlineData("#32770", true)]
    [InlineData("Chrome_WidgetWin_1", false)]
    [InlineData(null, false)]
    public void AddressClassification_RestrictsNativeDialogClass(string? className, bool expected)
    {
        Assert.Equal(expected, AddressEditDiscovery.IsEligibleDialogClass(className));
    }
}
