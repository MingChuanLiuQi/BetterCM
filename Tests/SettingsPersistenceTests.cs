using BetterCM.Config;
using BetterCM.Services;
using Microsoft.Win32;
using Xunit;
namespace BetterCM.Tests;
public class SettingsPersistenceTests
{
    [Fact] public void SnapshotIsStableAcrossClonePersistenceAndSetInsertionOrder()
    {
        var config = new AppConfig { PinnedShellActions = ["z", "a"], HiddenShellActions = ["b", "a"], Material = "acrylic" };
        var equivalent = config.Clone(); equivalent.PinnedShellActions = ["a", "z"]; equivalent.HiddenShellActions = ["a", "b"];
        Assert.Equal(ConfigJson.Snapshot(config), ConfigJson.Snapshot(equivalent));
        Assert.Equal(ConfigJson.Snapshot(config), ConfigJson.Snapshot(ConfigJson.Deserialize(ConfigJson.Serialize(config))!));
        equivalent.Material = "mica"; Assert.NotEqual(ConfigJson.Snapshot(config), ConfigJson.Snapshot(equivalent));
        Assert.Equal("dark", ConfigJson.Deserialize("{\"theme\":\"dark\"}")!.Theme);
    }
    [Fact] public void StartupFailureRestoresExactConfigTextAndOriginalStartupValue()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-settings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "config.json"); const string original = "{\"GlobalHotkey\":\"Alt+X\",\"Theme\":\"dark\"}";
            File.WriteAllText(path, original); var store = new ConfigStore(path); Assert.True(store.Load());
            object? startup = "original executable --background";
            var next = store.Current.Clone(); next.Theme = "light";
            var error = SettingsPersistence.Save(store, next, true, () => new(startup, RegistryValueKind.String), _ => { startup = "partial write"; throw new IOException("startup denied"); }, snapshot => startup = snapshot.Value);
            Assert.Contains("已还原", error); Assert.Equal(original, File.ReadAllText(path)); Assert.Equal("dark", store.Current.Theme); Assert.Equal("original executable --background", startup);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void ConfigSaveFailureDoesNotTouchStartup()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-settings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "config.json");
            var store = new ConfigStore(path); Assert.True(store.Save(new() { Theme = "dark" }));
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            bool touched = false;
            var error = SettingsPersistence.Save(store, new() { Theme = "light" }, true, () => new(null, RegistryValueKind.String), _ => touched = true, _ => touched = true);
            Assert.NotNull(error); Assert.False(touched);
            Assert.Equal("dark", ConfigJson.Deserialize(File.ReadAllText(path))!.Theme);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void FailedRollbackIsExplicitAndOtherStateStillRestores()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-settings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var store = new ConfigStore(Path.Combine(root, "config.json")); Assert.True(store.Save(new() { Theme = "dark" }));
            var error = SettingsPersistence.Save(store, new() { Theme = "light" }, true, () => new(null, RegistryValueKind.String), _ => throw new IOException("denied"), _ => throw new IOException("rollback denied"));
            Assert.Contains("启动项还原失败", error); Assert.Equal("dark", store.Current.Theme); Assert.True(store.Load()); Assert.Equal("dark", store.Current.Theme);
        }
        finally { Directory.Delete(root, true); }
    }
}
