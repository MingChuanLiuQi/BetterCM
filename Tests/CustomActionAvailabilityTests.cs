using BetterCM.Config;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class CustomActionAvailabilityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-availability-" + Guid.NewGuid().ToString("N"));
    public CustomActionAvailabilityTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact] public void MissingProgramIsDisabledWithoutDestroyingConfiguration()
    {
        var action = new CustomActionItem { Title = "removable tool", Executable = Path.Combine(_root, "missing.exe"), Arguments = "\"{path}\"" };
        var store = new ConfigStore(Path.Combine(_root, "config.json"));
        Assert.True(store.Save(new() { CustomActions = [action] }), store.LastError);
        Assert.True(store.Load(), store.LastError);
        var stored = Assert.Single(store.Current.CustomActions);
        var row = CustomActionPresentation.Create(stored, () => Task.CompletedTask);
        Assert.False(row.Enabled); Assert.Null(row.Execute); Assert.Contains("程序不可用", row.Detail);
        Assert.True(stored.Enabled); Assert.Equal(action.Executable, stored.Executable);
    }
    [Fact] public void AvailabilityIsRecheckedWhenMediaReturnsOrProgramDisappears()
    {
        string executable = Path.Combine(_root, "tool.exe");
        var action = new CustomActionItem { Title = "tool", Executable = executable };
        Assert.False(CustomActionPresentation.Create(action, () => Task.CompletedTask).Enabled);
        File.WriteAllText(executable, "fixture");
        Func<Task> execute = () => Task.CompletedTask;
        var available = CustomActionPresentation.Create(action, execute);
        Assert.True(available.Enabled); Assert.Same(execute, available.Execute); Assert.Equal(executable, available.Detail);
        File.Delete(executable);
        Assert.False(CustomActionPresentation.Create(action, execute).Enabled);
        Assert.True(action.Enabled);
    }
    [Theory]
    [InlineData("tool.exe")]
    [InlineData("tool.cmd")]
    public void UnresolvedProgramNamesCannotCreateEnabledRows(string executable)
    {
        Assert.False(CustomActionPresentation.Create(new() { Executable = executable }, () => Task.CompletedTask).Enabled);
    }
}
