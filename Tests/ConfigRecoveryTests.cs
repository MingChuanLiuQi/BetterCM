using BetterCM.Config;
using Xunit;

namespace BetterCM.Tests;

public sealed class ConfigRecoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "BetterCM-recovery-" + Guid.NewGuid().ToString("N"));
    private string PathToConfig => Path.Combine(_directory, "config.json");
    public ConfigRecoveryTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void CorruptInitialLoadCannotBeSavedOverAndOriginalIsPreservedOnce()
    {
        const string broken = "{\"Theme\":\"dark\",\"CustomActions\":[";
        File.WriteAllText(PathToConfig, broken);
        var store = new ConfigStore(PathToConfig);
        Assert.False(store.Load()); Assert.False(store.CanSave);
        Assert.False(store.Save(new())); Assert.Equal(broken, File.ReadAllText(PathToConfig));
        Assert.Equal(broken, File.ReadAllText(store.InvalidCopyPath!));
        Assert.False(store.Load()); Assert.Single(Directory.GetFiles(_directory, "*.invalid-*"));
        File.WriteAllText(PathToConfig, "{\"Theme\":\"light\"}");
        Assert.True(store.Load()); Assert.True(store.CanSave); Assert.Equal("light", store.Current.Theme);
    }
    [Fact]
    public void NormalSavesKeepLastGoodBackupAndExplicitRecoveryKeepsCorruptCopy()
    {
        var store = new ConfigStore(PathToConfig);
        Assert.True(store.Save(new() { Theme = "dark" })); string first = File.ReadAllText(PathToConfig);
        Assert.True(store.Save(new() { Theme = "light" }));
        Assert.Equal(first, File.ReadAllText(store.BackupPath));
        File.WriteAllText(PathToConfig, "broken"); Assert.False(store.Load());
        string invalid = store.InvalidCopyPath!;
        Assert.True(store.RestoreBackup()); Assert.True(store.CanSave); Assert.Equal("dark", store.Current.Theme);
        Assert.Equal(first, File.ReadAllText(PathToConfig)); Assert.Equal("broken", File.ReadAllText(invalid));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }
    [Fact]
    public void UnsupportedSchemaIsPreservedAndCannotBeDowngradedBySavingDefaults()
    {
        string future = "{\"SchemaVersion\":9,\"FutureSetting\":\"keep\"}";
        File.WriteAllText(PathToConfig, future); var store = new ConfigStore(PathToConfig);
        Assert.False(store.Load()); Assert.False(store.Save(new())); Assert.Equal(future, File.ReadAllText(PathToConfig));
    }
    [Fact]
    public void InvalidBackupCannotReplaceOriginalOrUnlockSaving()
    {
        File.WriteAllText(PathToConfig, "original-broken"); File.WriteAllText(PathToConfig + ".bak", "broken-backup");
        var store = new ConfigStore(PathToConfig); Assert.False(store.Load());
        Assert.False(store.RestoreBackup()); Assert.False(store.CanSave);
        Assert.Equal("original-broken", File.ReadAllText(PathToConfig));
    }
    [Fact]
    public void ExternalEditBetweenLoadAndSaveIsNotOverwrittenOrLeftAsTemporaryFile()
    {
        var store = new ConfigStore(PathToConfig); Assert.True(store.Save(new() { Theme = "dark" }));
        const string external = "{\"Theme\":\"light\"}";
        File.WriteAllText(PathToConfig, external);
        Assert.False(store.Save(new() { Theme = "system" }));
        Assert.Equal(external, File.ReadAllText(PathToConfig)); Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.True(store.Load()); Assert.Equal("light", store.Current.Theme);
    }
}
