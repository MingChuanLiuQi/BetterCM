using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class CustomActionAvailabilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-availability-" + Guid.NewGuid().ToString("N"));
    public CustomActionAvailabilityTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    // Verify presentation using real fixture files without making correctness depend on thread-pool latency.
    // ProgramAvailabilityIsolationTests exercises the bounded asynchronous probe implementation separately.
    private static Task<MenuAction> PresentFileAsync(CustomActionItem action, Func<Task> execute)
        => CustomActionPresentation.CreateAsync(action, execute, (path, _) => Task.FromResult<bool?>(File.Exists(path)));

    [Fact]
    public async Task MissingProgramIsDisabledWithoutDestroyingConfiguration()
    {
        var action = new CustomActionItem { Title = "removable tool", Executable = Path.Combine(_root, "missing.exe"), Arguments = "\"{path}\"" };
        var store = new ConfigStore(Path.Combine(_root, "config.json"));
        Assert.True(store.Save(new() { CustomActions = [action] }), store.LastError);
        Assert.True(store.Load(), store.LastError);
        var stored = Assert.Single(store.Current.CustomActions);
        var row = await PresentFileAsync(stored, () => Task.CompletedTask);
        Assert.False(row.Enabled); Assert.Null(row.Execute); Assert.Contains("程序不可用", row.Detail);
        Assert.True(stored.Enabled); Assert.Equal(action.Executable, stored.Executable);
    }
    [Fact]
    public async Task AvailabilityIsRecheckedWhenMediaReturnsOrProgramDisappears()
    {
        string executable = Path.Combine(_root, "tool.exe");
        var action = new CustomActionItem { Title = "tool", Executable = executable };
        Assert.False((await PresentFileAsync(action, () => Task.CompletedTask)).Enabled);
        File.WriteAllText(executable, "fixture");
        Func<Task> execute = () => Task.CompletedTask;
        var available = await PresentFileAsync(action, execute);
        Assert.True(available.Enabled); Assert.Same(execute, available.Execute); Assert.Equal(executable, available.Detail);
        File.Delete(executable);
        Assert.False((await PresentFileAsync(action, execute)).Enabled);
        Assert.True(action.Enabled);
    }
    [Fact]
    public async Task TimedOutProbeDisablesActionWithoutChangingItsConfiguration()
    {
        var action = new CustomActionItem { Title = "busy tool", Executable = Path.Combine(_root, "tool.exe") };
        var row = await CustomActionPresentation.CreateAsync(action, () => Task.CompletedTask, (_, _) => Task.FromResult<bool?>(null));
        Assert.False(row.Enabled); Assert.Null(row.Execute);
        Assert.Contains("超时或繁忙", row.Detail); Assert.Contains(action.Executable, row.Detail);
        Assert.True(action.Enabled); Assert.Equal("busy tool", action.Title);
    }
    [Theory]
    [InlineData("tool.exe")]
    [InlineData("tool.cmd")]
    public async Task UnresolvedProgramNamesCannotCreateEnabledRows(string executable)
    {
        Assert.False((await CustomActionPresentation.CreateAsync(new() { Executable = executable }, () => Task.CompletedTask)).Enabled);
    }
}
