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
        Assert.Equal(Enum.Parse<DialogTargetSource>(expectedName), DialogTargetSelection.Select(capturedUsable, foregroundUsable));
    }

    [Fact]
    public void Readiness_CapturedVisibleButNonForegroundWhileFlowForegroundWaits()
    {
        var decision = DialogReadinessPlan.Decide(
            targetIsUsable: true,
            targetIsForeground: false,
            flowIsForeground: true,
            mayActivateTarget: true,
            retriesExhausted: false);

        Assert.Equal(DialogReadinessDecision.Wait, decision);
    }

    [Fact]
    public void Readiness_VisibleTargetAfterFlowHidesRequestsControlledActivationThenVerification()
    {
        var activate = DialogReadinessPlan.Decide(
            targetIsUsable: true,
            targetIsForeground: false,
            flowIsForeground: false,
            mayActivateTarget: true,
            retriesExhausted: false);
        var ready = DialogReadinessPlan.Decide(
            targetIsUsable: true,
            targetIsForeground: true,
            flowIsForeground: false,
            mayActivateTarget: false,
            retriesExhausted: false);

        Assert.Equal(DialogReadinessDecision.ActivateTarget, activate);
        Assert.Equal(DialogReadinessDecision.Ready, ready);
    }

    [Fact]
    public void Readiness_NonForegroundTargetAtRetryLimitFailsWithoutKeyboardInjection()
    {
        var decision = DialogReadinessPlan.Decide(
            targetIsUsable: true,
            targetIsForeground: false,
            flowIsForeground: false,
            mayActivateTarget: false,
            retriesExhausted: true);

        Assert.Equal(DialogReadinessDecision.Fail, decision);
    }

    [Fact]
    public void RetryPlan_IsBounded()
    {
        var plan = new DialogTargetRetryPlan(Attempts: 3, DelayMilliseconds: 25);
        Assert.True(plan.ShouldTry(0)); Assert.True(plan.ShouldTry(2)); Assert.False(plan.ShouldTry(3)); Assert.False(plan.ShouldTry(-1));
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
    public void AddressClassification_RestrictsNativeDialogClass(string? className, bool expected) => Assert.Equal(expected, AddressEditDiscovery.IsEligibleDialogClass(className));
}
