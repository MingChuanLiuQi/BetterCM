using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class ProgramAvailabilityIsolationTests
{
    [Fact]
    public async Task FourBlockedNetworkProbesDoNotConsumeLocalSlots()
    {
        using var release = new ManualResetEventSlim();
        using var started = new CountdownEvent(4);
        var availability = new ProgramAvailability(path =>
        {
            if (!path.StartsWith("remote", StringComparison.Ordinal)) return true;
            started.Signal(); release.Wait(); return true;
        }, path => path.StartsWith("remote", StringComparison.Ordinal));
        try
        {
            var remote = Enumerable.Range(0, 4).Select(i => availability.CheckAsync("remote" + i)).ToArray();
            Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
            Assert.True(await availability.CheckAsync("local").WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.All(await Task.WhenAll(remote), result => Assert.Null(result));
            Assert.True(await availability.CheckAsync("another-local"));
        }
        finally { release.Set(); }
    }
    [Fact]
    public async Task FailedRemoteProbeBacksOffAndRetriesAfterRecovery()
    {
        var clock = new ManualTime();
        int probes = 0;
        var availability = new ProgramAvailability(_ => Interlocked.Increment(ref probes) > 1, _ => true, clock);
        Assert.False(await availability.CheckAsync("remote"));
        Assert.Null(await availability.CheckAsync("REMOTE"));
        Assert.Equal(1, probes);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(await availability.CheckAsync("remote"));
        Assert.Equal(2, probes);
    }
    [Fact]
    public async Task CancellationDoesNotDiscardSharedNetworkProbe()
    {
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        int probes = 0;
        var availability = new ProgramAvailability(_ => { Interlocked.Increment(ref probes); started.Set(); release.Wait(); return true; }, _ => true);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var cancelled = availability.CheckAsync("remote", cancellation.Token);
            Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
            var shared = availability.CheckAsync("REMOTE");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            release.Set();
            Assert.True(await shared);
            Assert.Equal(1, probes);
        }
        finally { release.Set(); }
    }
    [Theory]
    [InlineData(@"\\server\share\tool.exe")]
    [InlineData(@"\\?\UNC\server\share\tool.exe")]
    public void NetworkNamespaceUsesRemotePool(string path) => Assert.True(ProgramAvailability.IsRemotePath(path));
    [Fact]
    public void FixedSystemDriveIncludingExtendedPathUsesLocalPool()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory)!;
        Assert.False(ProgramAvailability.IsRemotePath(Path.Combine(root, "tool.exe")));
        Assert.False(ProgramAvailability.IsRemotePath(@"\\?\" + Path.Combine(root, "tool.exe")));
    }
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan amount) => _now += amount;
    }
}
