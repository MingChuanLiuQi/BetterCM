using BetterCM.Config;
namespace BetterCM.Services;

internal static class SettingsPersistence
{
    internal static string? Save(ConfigStore store, AppConfig next, bool startup,
        Func<StartupService.Snapshot> capture, Action<bool> setStartup, Action<StartupService.Snapshot> restoreStartup)
    {
        ConfigStore.Validate(next);
        var previous = store.Current.Clone();
        string? originalText = File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null;
        var originalStartup = capture();
        if (!store.Save(next)) return store.LastError;
        try { setStartup(startup); return null; }
        catch (Exception ex)
        {
            var errors = new List<string>();
            try { restoreStartup(originalStartup); } catch (Exception rollback) { errors.Add("启动项还原失败：" + rollback.Message); }
            try { store.Restore(previous, originalText); } catch (Exception rollback) { errors.Add("配置还原失败：" + rollback.Message); }
            AppLog.Write("settings-startup", ex);
            return "启动项保存失败：" + ex.Message + (errors.Count == 0 ? "；已还原原配置和启动项。" : "；" + string.Join("；", errors));
        }
    }
}
