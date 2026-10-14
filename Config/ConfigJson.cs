using System.Text.Json;

namespace BetterCM.Config;

internal static class ConfigJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    internal static string Serialize(AppConfig config) => JsonSerializer.Serialize(config, Options);
    internal static AppConfig? Deserialize(string json) => JsonSerializer.Deserialize<AppConfig>(json, Options);
    // A snapshot compares effective configuration; set insertion order is irrelevant.
    // Keep action/list order intact because it controls menu ordering.
    internal static string Snapshot(AppConfig config)
    {
        var copy = Deserialize(Serialize(config))!;
        copy.PinnedShellActions = new(config.PinnedShellActions.Order(StringComparer.Ordinal));
        copy.HiddenShellActions = new(config.HiddenShellActions.Order(StringComparer.Ordinal));
        return Serialize(copy);
    }
}
