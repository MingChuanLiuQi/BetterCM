using System.Text.Json.Nodes;
using BetterCM.Services;
namespace BetterCM.Config;

public static class ConfigMigrations
{
    // Schema 2 pins legacy executable names and disables names that cannot be resolved.
    // Migration is in memory; ConfigStore keeps the original disk snapshot until explicit save.
    public static string Migrate(string json, bool removeLegacyDefaults = false)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("配置必须是 JSON 对象");
        var schemaKeys = root.Select(x => x.Key).Where(x => x.Equals("SchemaVersion", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (schemaKeys.Length > 1) throw new InvalidDataException("配置包含重复的格式版本");
        int version = schemaKeys.Length == 0 ? 1 : root[schemaKeys[0]]?.GetValue<int>() ?? throw new InvalidDataException("配置格式版本不能为 null");
        if (version < 1 || version > AppConfig.CurrentSchemaVersion) throw new InvalidDataException("此版本不支持该配置格式，请使用兼容的 BetterCM 版本。");
        if (version == AppConfig.CurrentSchemaVersion && !removeLegacyDefaults) return json;
        // Deserialize only after checking the version; never discard unknown future data.
        var config = ConfigJson.Deserialize(json) ?? throw new InvalidDataException("配置为空");
        // Only the bundled first-run defaults are cleaned; user-defined actions are preserved.
        // Compare their original names before resolving executable paths.
        if (removeLegacyDefaults)
            config.CustomActions?.RemoveAll(x => x != null &&
                new[] { "code", "wt.exe", "notepad.exe" }.Contains(x.Executable, StringComparer.OrdinalIgnoreCase) &&
                x.Arguments is "\"{path}\"" or "-d \"{dir}\"");
        while (version < AppConfig.CurrentSchemaVersion)
        {
            switch (version)
            {
                case 1:
                    if (config.CustomActions != null)
                        foreach (var action in config.CustomActions.Where(x => x != null))
                        {
                            var resolved = string.Equals(action.Executable, "code", StringComparison.OrdinalIgnoreCase) ? ExecutableResolver.VSCode : ExecutableResolver.Resolve(action.Executable ?? "");
                            if (resolved != null) action.Executable = resolved;
                            else action.Enabled = false;
                        }
                    version = 2;
                    break;
                default: throw new InvalidDataException("缺少配置迁移步骤");
            }
        }
        config.SchemaVersion = version;
        return ConfigJson.Serialize(config);
    }
}
