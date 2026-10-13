using Flow.Launcher.Plugin.QuickJump.Diagnostics;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.Diagnostics;

public sealed class GuiThreadOwnershipPlanTests
{
    [Fact]
    public void DistinctThreads_DeduplicatesTopAddressAndFocusedOwners()
    {
        var snapshot = Create(topThread: 10, addressThread: 10, focusedThread: 10);

        Assert.Equal(new uint[] { 10 }, GuiThreadOwnershipPlan.DistinctThreads(snapshot));
    }

    [Fact]
    public void DistinctThreads_PreservesDistinctNonZeroThreadsInStableOrder()
    {
        var snapshot = Create(topThread: 10, addressThread: 20, focusedThread: 10);

        Assert.Equal(new uint[] { 10, 20 }, GuiThreadOwnershipPlan.DistinctThreads(snapshot));
    }

    [Fact]
    public void Interpret_IdentifiesAddressThreadDifferentFromTopThread()
    {
        Assert.Equal("top-and-address-different-threads", GuiThreadOwnershipPlan.Interpret(Create(topThread: 10, addressThread: 20, focusedThread: 20)));
        Assert.Equal("top-and-address-same-thread", GuiThreadOwnershipPlan.Interpret(Create(topThread: 10, addressThread: 10, focusedThread: 10)));
    }

    [Fact]
    public void Interpret_RejectsUnqualifiedAddressEvidence()
    {
        Assert.Equal("address-unqualified", GuiThreadOwnershipPlan.Interpret(Create(topThread: 10, addressThread: 20, focusedThread: 20, qualified: false)));
    }

    private static GuiThreadOwnershipSnapshot Create(uint topThread, uint addressThread, uint focusedThread, bool qualified = true) =>
        new(
            TopDialogHandle: 1,
            TopDialogThread: topThread,
            TopDialogProcess: 100,
            AddressEditHandle: 2,
            AddressEditThread: addressThread,
            AddressEditProcess: 100,
            FocusedHandle: 3,
            FocusedThread: focusedThread,
            FocusedProcess: 100,
            AddressStructureQualified: qualified,
            AddressRootMatchesDialog: qualified);
}
