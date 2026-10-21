using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Windows.Win32.Foundation;
using Xunit;

namespace Flow.Launcher.Plugin.QuickJump.Tests.DialogNavigation;

public sealed class FallbackDialogSelectionTests
{
    private const uint FlowProcess = 100;

    [Fact]
    public void Select_ExcludesFlowOwnProcess()
    {
        StandardDialogCandidate[] candidates = [new(new HWND(1), FlowProcess)];

        Assert.Null(FallbackDialogSelection.Select(candidates, FlowProcess));
    }

    [Fact]
    public void Select_KeepsUniqueUnrelatedCandidate()
    {
        StandardDialogCandidate[] candidates = [new(new HWND(2), 200)];

        var selected = FallbackDialogSelection.Select(candidates, FlowProcess);

        Assert.NotNull(selected);
        Assert.Equal(200u, selected!.Value.ProcessId);
    }

    [Fact]
    public void Select_MultipleCandidatesFromDifferentProcessesIsAmbiguous()
    {
        StandardDialogCandidate[] candidates =
        [
            new(new HWND(2), 200),
            new(new HWND(3), 300),
        ];

        Assert.Null(FallbackDialogSelection.Select(candidates, FlowProcess));
        Assert.Null(FallbackDialogSelection.SelectProcessId(candidates, FlowProcess));
    }

    [Fact]
    public void Select_MultipleDialogsFromOneUnrelatedProcessRemainsAttributable()
    {
        StandardDialogCandidate[] candidates =
        [
            new(new HWND(2), 200),
            new(new HWND(3), 200),
        ];

        Assert.Equal(200u, FallbackDialogSelection.SelectProcessId(candidates, FlowProcess));
    }

    [Fact]
    public void Select_IgnoresFlowCandidatesWhenAnUnrelatedOneExists()
    {
        StandardDialogCandidate[] candidates =
        [
            new(new HWND(1), FlowProcess),
            new(new HWND(2), 200),
        ];

        Assert.Equal(200u, FallbackDialogSelection.SelectProcessId(candidates, FlowProcess));
    }

    [Fact]
    public void Select_NoCandidatesYieldsNoAttribution()
    {
        Assert.Null(FallbackDialogSelection.Select([], FlowProcess));
    }
}
