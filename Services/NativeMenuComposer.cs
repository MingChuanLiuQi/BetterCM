using BetterCM.Models;
namespace BetterCM.Services;

public static class NativeMenuComposer
{
    public static List<MenuEntry> Compose(IReadOnlyList<ShellMenuNode> menu, IReadOnlyList<MenuAction> commands, string filter = "")
        => ComposeIndexed(menu, commands.DistinctBy(x => x.Id).ToDictionary(x => x.Id), filter);
    internal static List<MenuEntry> ComposeIndexed(IReadOnlyList<ShellMenuNode> menu, IReadOnlyDictionary<string, MenuAction> byId, string filter = "")
    {
        var needle = filter.Trim();
        List<MenuEntry> Build(IReadOnlyList<ShellMenuNode> nodes, string path, string ancestor, bool parentEnabled)
        {
            var rows = new List<MenuEntry>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i]; string id = path + ":" + i;
                if (node.IsSeparator) { if (needle.Length == 0) rows.Add(new() { Id = id, IsSeparator = true, IsNativeLayout = true }); continue; }
                string fullTitle = ancestor.Length == 0 ? node.Title : ancestor + " · " + node.Title;
                bool enabled = parentEnabled && node.Enabled;
                var children = node.Children == null ? null : Build(node.Children, id, fullTitle, enabled);
                // Search returns directly executable leaves with their complete native path.
                // A matching parent naturally includes its leaves; no empty arrows or ID resets.
                if (needle.Length > 0 && children != null) { rows.AddRange(children); continue; }
                if (needle.Length > 0 && !fullTitle.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
                byId.TryGetValue("native:" + node.Command, out var action);
                if (action != null && action.Enabled != enabled)
                    action = new() { Id = action.Id, Title = action.Title, Glyph = action.Glyph, Shortcut = action.Shortcut, Detail = action.Detail, Enabled = enabled, ShellKey = action.ShellKey, Group = action.Group, Verb = action.Verb, Execute = action.Execute };
                rows.Add(new()
                {
                    Id = id, Title = needle.Length == 0 ? node.Title : fullTitle, Action = action, IsGroup = children != null, Children = children ?? [],
                    DeferredSubmenu = node.DeferredSubmenu,
                    GroupEnabled = enabled, Checked = node.Checked, IsDefault = node.IsDefault, IsNativeLayout = true,
                    Glyph = node.Checked ? "\uE73E" : children != null ? "\uE8B7" : action?.Glyph ?? "\uE8A7", Hint = node.Shortcut.Length > 0 ? node.Shortcut : action?.Shortcut ?? "",
                    Detail = fullTitle, Mnemonic = node.Mnemonic
                });
            }
            return rows;
        }
        return Build(menu, "native-row", "", true);
    }
}
