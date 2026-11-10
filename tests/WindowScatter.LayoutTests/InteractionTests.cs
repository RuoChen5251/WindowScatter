using WindowScatter;
using Xunit;

public class InteractionTests
{
    [Fact]
    public void HeldActivationKeyOnlyTogglesOnceUntilReleased()
    {
        var input = new ScatterInteraction();
        Assert.True(input.ActivationKeyDown(true));
        Assert.False(input.ActivationKeyDown(true));
        input.ActivationKeyUp();
        Assert.True(input.ActivationKeyDown(true));
    }

    [Fact]
    public void NonmatchingModifiersDoNotActivate()
    {
        var input = new ScatterInteraction();
        Assert.False(input.ActivationKeyDown(false));
        Assert.False(input.ActivationKeyDown(true));
        input.ActivationKeyUp();
        Assert.True(input.ActivationKeyDown(true));
    }

    [Fact]
    public void DismissDuringOpeningWaitsUntilReadyAndRunsOnlyOnce()
    {
        var input = new ScatterInteraction();
        input.RequestDismiss();
        Assert.False(input.TakeDismissRequest(isBusy: true));
        Assert.True(input.TakeDismissRequest(isBusy: false));
        Assert.False(input.TakeDismissRequest(isBusy: false));
    }

    [Fact]
    public void NewSessionDoesNotInheritPendingDismissal()
    {
        var input = new ScatterInteraction();
        input.RequestDismiss();
        input.ClearDismissRequest();
        Assert.False(input.TakeDismissRequest(isBusy: false));
    }
}
