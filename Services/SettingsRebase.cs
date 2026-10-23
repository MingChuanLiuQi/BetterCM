using System.Text.Json.Nodes;
using BetterCM.Config;
namespace BetterCM.Services;

// Internal tray/pin changes advance the saved baseline while keeping pending
// settings edits. A locally edited value wins; pin/hide sets merge per key.
internal static class SettingsRebase
{
    internal static AppConfig Merge(AppConfig baseline, AppConfig draft, AppConfig saved)
    {
        var before = JsonNode.Parse(ConfigJson.Serialize(baseline))!.AsObject();
        var pending = JsonNode.Parse(ConfigJson.Serialize(draft))!.AsObject();
        var result = JsonNode.Parse(ConfigJson.Serialize(saved))!.AsObject();
        foreach (var pair in pending)
        {
            if (pair.Key is nameof(AppConfig.PinnedShellActions) or nameof(AppConfig.HiddenShellActions)) continue;
            if (!JsonNode.DeepEquals(before[pair.Key], pair.Value)) result[pair.Key] = pair.Value?.DeepClone();
        }
        var merged = ConfigJson.Deserialize(result.ToJsonString())!;
        static HashSet<string> MergeSet(HashSet<string> original, HashSet<string> edited, HashSet<string> current)
        {
            var values = new HashSet<string>(current, current.Comparer);
            values.ExceptWith(original.Except(edited));
            values.UnionWith(edited.Except(original));
            return values;
        }
        merged.PinnedShellActions = MergeSet(baseline.PinnedShellActions, draft.PinnedShellActions, saved.PinnedShellActions);
        merged.HiddenShellActions = MergeSet(baseline.HiddenShellActions, draft.HiddenShellActions, saved.HiddenShellActions);
        return merged;
    }
}
