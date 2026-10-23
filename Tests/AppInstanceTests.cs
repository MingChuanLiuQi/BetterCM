using BetterCM.Services;
using Xunit;

namespace BetterCM.Tests;

public sealed class AppInstanceTests
{
    [Theory]
    [InlineData("--shell-host")]
    [InlineData("--context-host")]
    [InlineData("--shell-test-host")]
    [InlineData("--context-test-host")]
    public void HelpersAreIdentifiedEvenWhenArgumentsAreMalformed(string argument) => Assert.True(AppInstance.IsHelper([argument]));
    [Fact]
    public async Task MissingInstanceStopsWithoutWaitingOrTouchingOtherInstances()
        => Assert.True(await AppInstance.StopAsync("BetterCM.Test." + Guid.NewGuid().ToString("N"), TimeSpan.FromSeconds(1)));
    [Fact]
    public async Task StopWaitsForActualInstanceCleanupAndReturnsFailureIfStillPresent()
    {
        string name = "BetterCM.Test." + Guid.NewGuid().ToString("N");
        using var instance = new Mutex(false, @"Local\" + name + ".App");
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name + ".Exit");
        Assert.False(await AppInstance.StopAsync(name, TimeSpan.FromMilliseconds(100)));
        Assert.True(signal.WaitOne(0));
        var stopping = AppInstance.StopAsync(name, TimeSpan.FromSeconds(2));
        await Task.Delay(100); Assert.False(stopping.IsCompleted);
        instance.Dispose(); Assert.True(await stopping);
    }
}
