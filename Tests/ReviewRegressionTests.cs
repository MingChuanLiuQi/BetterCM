using System.Text.Json;
using BetterCM.Config;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class ReviewRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-review-" + Guid.NewGuid().ToString("N"));
    public ReviewRegressionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomActionWorkingDirectoryRequiresExplicitOptIn(bool useTarget)
    {
        string program = Path.Combine(_root, "program"), target = Path.Combine(_root, "target");
        Directory.CreateDirectory(program); Directory.CreateDirectory(target);
        string executable = Path.Combine(program, "tool.exe"); File.WriteAllText(executable, "fixture");
        var action = new CustomActionItem { Executable = executable, UseTargetWorkingDirectory = useTarget };
        var info = ActionExecutor.CreateCustomStartInfo(action, [target], target);
        Assert.Equal(useTarget ? target : program, info.WorkingDirectory);
        Assert.Equal(executable, info.FileName);
        Assert.Equal(target, Assert.Single(info.ArgumentList));
        Assert.Equal(useTarget, new AppConfig { CustomActions = [action] }.Clone().CustomActions[0].UseTargetWorkingDirectory);
    }
    [Fact] public void ExistingSchemaTwoDefaultsToProgramDirectory()
    {
        string path = Path.Combine(_root, "config.json");
        File.WriteAllText(path, "{\"SchemaVersion\":2,\"CustomActions\":[{\"Title\":\"old\",\"Executable\":\"C:\\\\Tools\\\\tool.exe\"}]}");
        var store = new ConfigStore(path);
        Assert.True(store.Load(), store.LastError);
        Assert.False(Assert.Single(store.Current.CustomActions).UseTargetWorkingDirectory);
    }
    [Theory]
    [InlineData("code")]
    [InlineData("wt.exe")]
    [InlineData("NOTEPAD.EXE")]
    public void BundledLegacyDefaultsAreRemovedBeforeExecutableResolution(string command)
    {
        string json = JsonSerializer.Serialize(new { SchemaVersion = 1, CustomActions = new[] {
            new { Title = "old default", Executable = command, Arguments = "\"{path}\"" },
            new { Title = "keep customized parameters", Executable = command, Arguments = "--custom \"{path}\"" }
        } });
        using var migrated = JsonDocument.Parse(ConfigMigrations.Migrate(json, removeLegacyDefaults: true));
        var action = Assert.Single(migrated.RootElement.GetProperty("CustomActions").EnumerateArray());
        Assert.Equal("keep customized parameters", action.GetProperty("Title").GetString());
        using var userConfig = JsonDocument.Parse(ConfigMigrations.Migrate(json));
        Assert.Equal(2, userConfig.RootElement.GetProperty("CustomActions").GetArrayLength());
    }
    [Fact] public void RestoreBackupPersistsCurrentSchemaAndPreservesOriginalBackup()
    {
        string path = Path.Combine(_root, "config.json");
        const string legacy = "{\"SchemaVersion\":1,\"Theme\":\"dark\",\"CustomActions\":[]}";
        File.WriteAllText(path, "broken original"); File.WriteAllText(path + ".bak", legacy);
        var store = new ConfigStore(path);
        Assert.False(store.Load()); string invalid = store.InvalidCopyPath!;
        Assert.True(store.RestoreBackup(), store.LastError);
        Assert.Equal(AppConfig.CurrentSchemaVersion, store.Current.SchemaVersion);
        using var disk = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(AppConfig.CurrentSchemaVersion, disk.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("dark", disk.RootElement.GetProperty("Theme").GetString());
        Assert.Equal(legacy, File.ReadAllText(path + ".bak"));
        Assert.Equal("broken original", File.ReadAllText(invalid));
        Assert.True(store.Save(store.Current));
    }
}
