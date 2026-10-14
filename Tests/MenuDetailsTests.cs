using System.Windows.Media;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class MenuDetailsTests
{
    [Fact] public async Task SameTargetSharesPendingReadButCompletedResultsAreNotCached()
    {
        var probe = new OptionalProbe<string>(); var pending = new TaskCompletionSource<string?>(); int reads = 0;
        var first = probe.ReadAsync(() => { reads++; return pending.Task; }, TimeSpan.FromSeconds(1), identity: "file:A");
        var follower = probe.ReadAsync(() => { reads++; return Task.FromResult<string?>("duplicate"); }, TimeSpan.FromSeconds(1), identity: "file:a");
        pending.SetResult("shared"); Assert.Equal("shared", await first); Assert.Equal("shared", await follower); Assert.Equal(1, reads);
        Assert.Equal("fresh", await probe.ReadAsync(() => Task.FromResult<string?>("fresh"), TimeSpan.FromSeconds(1), identity: "file:A"));
    }
    [Fact] public async Task DifferentTargetsNeverShareMetadata()
    {
        var probe = new OptionalProbe<string>(); var pending = new TaskCompletionSource<string?>(); int reads = 0;
        var first = probe.ReadAsync(() => { reads++; return pending.Task; }, TimeSpan.FromSeconds(1), identity: "file:A");
        var second = probe.ReadAsync(() => { reads++; return Task.FromResult<string?>("B"); }, TimeSpan.FromSeconds(1), identity: "file:B");
        pending.SetResult("A"); Assert.Equal("A", await first); Assert.Equal("B", await second); Assert.Equal(2, reads);
    }
    [Fact] public async Task CancellingFollowerDoesNotCancelSharedNativeRead()
    {
        var probe = new OptionalProbe<string>(); var pending = new TaskCompletionSource<string?>();
        var first = probe.ReadAsync(() => pending.Task, TimeSpan.FromSeconds(1), identity: "A");
        using var cancellation = new CancellationTokenSource();
        var follower = probe.ReadAsync(() => Task.FromResult<string?>("wrong"), TimeSpan.FromSeconds(1), cancellation.Token, "A");
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => follower);
        pending.SetResult("correct"); Assert.Equal("correct", await first);
    }
    [Fact] public async Task SizeIsPublishedWhileIconStillPendingAndSurvivesIconTimeout()
    {
        var icon = new TaskCompletionSource<ImageSource?>(); var first = new TaskCompletionSource<long?>();
        var snapshots = new List<long?>();
        var loading = MenuDetails.LoadAsync(icon.Task, Task.FromResult<long?>(42), (_, size) => { snapshots.Add(size); first.TrySetResult(size); }, default, TimeSpan.FromMilliseconds(100));
        Assert.Equal(42, await first.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        await loading; Assert.Equal(42, snapshots.Last()); icon.SetResult(null);
    }
    [Fact] public async Task SlowSizeDoesNotDiscardAvailableIcon()
    {
        var image = new DrawingImage(); image.Freeze(); var size = new TaskCompletionSource<long?>(); ImageSource? result = null;
        await MenuDetails.LoadAsync(Task.FromResult<ImageSource?>(image), size.Task, (icon, _) => result = icon, default, TimeSpan.FromMilliseconds(20));
        Assert.Same(image, result); size.SetResult(null);
    }
    [Fact] public async Task CancellationNeverPublishesLateMetadata()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); int updates = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MenuDetails.LoadAsync(Task.FromResult<ImageSource?>(null), Task.FromResult<long?>(7), (_, _) => updates++, cancellation.Token));
        Assert.Equal(0, updates);
    }
    [Fact] public async Task BlockedProbeDoesNotAccumulateReadsAndRecoversAfterCompletion()
    {
        var probe = new OptionalProbe<string>(); var pending = new TaskCompletionSource<string?>(); int reads = 0;
        var reading = probe.TryStartNoWait(() => { reads++; return pending.Task; });
        for (int i = 0; i < 50; i++) Assert.Null(await probe.TryStartNoWait(() => { reads++; return Task.FromResult<string?>("wrong"); }));
        Assert.Equal(1, reads); pending.SetResult("first"); Assert.Equal("first", await reading);
        Assert.Equal("next", await probe.TryStartNoWait(() => Task.FromResult<string?>("next")));
    }
    [Fact] public async Task FastConsecutiveReadWaitsForInFlightProbeInsteadOfDroppingResult()
    {
        var probe = new OptionalProbe<string>(); var first = new TaskCompletionSource<string?>(); int reads = 0;
        var inflight = probe.ReadAsync(() => { reads++; return first.Task; }, TimeSpan.FromSeconds(1));
        var second = probe.ReadAsync(() => { reads++; return Task.FromResult<string?>("second"); }, TimeSpan.FromSeconds(1));
        await Task.Delay(25); first.SetResult("first");
        Assert.Equal("first", await inflight); Assert.Equal("second", await second); Assert.Equal(2, reads);
    }
    [Fact] public async Task StalledInFlightProbeTimesOutFollowerWithoutStartingSecondRead()
    {
        var probe = new OptionalProbe<string>(); var stalled = new TaskCompletionSource<string?>(); int reads = 0;
        var first = probe.ReadAsync(() => { reads++; return stalled.Task; }, TimeSpan.FromMilliseconds(40));
        var follower = probe.ReadAsync(() => { reads++; return Task.FromResult<string?>("unreached"); }, TimeSpan.FromMilliseconds(40));
        Assert.Null(await first); Assert.Null(await follower); Assert.Equal(1, reads);
        stalled.SetResult("done"); await Task.Delay(10);
        Assert.Equal("recovered", await probe.ReadAsync(() => { reads++; return Task.FromResult<string?>("recovered"); }, TimeSpan.FromSeconds(1)));
    }
}
