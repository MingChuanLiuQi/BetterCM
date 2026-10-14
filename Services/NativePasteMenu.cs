namespace BetterCM.Services;

internal static class NativePasteMenu
{
    internal static void Merge(List<ShellMenuNode> nodes, List<ShellItem> items, IReadOnlyList<ShellItem> capabilities, IReadOnlyList<ShellMenuNode> capabilityNodes, int commandBase)
    {
        foreach (var paste in capabilities.Where(x => x.Group.Length == 0 && x.Verb is "paste" or "pastelink"))
        {
            if (items.Any(x => x.Group.Length == 0 && x.Verb.Equals(paste.Verb, StringComparison.OrdinalIgnoreCase))) continue;
            var caption = MenuComposer.Caption(paste.Title);
            if (caption.Length == 0) continue; // Do not invent a label for an unrepresentable native command.
            int nodeIndex = nodes.FindIndex(x => !x.IsSeparator && x.Children == null && MenuComposer.Caption(x.Title).Equals(caption, StringComparison.OrdinalIgnoreCase));
            var original = nodeIndex >= 0 ? nodes[nodeIndex] : capabilityNodes.FirstOrDefault(x => x.Command == paste.Command) ?? new(paste.Title);
            int itemIndex = nodeIndex >= 0 ? items.FindIndex(x => x.Group.Length == 0 && x.Command == original.Command) : -1;
            if (itemIndex >= 0) items.RemoveAt(itemIndex);
            items.Insert(itemIndex >= 0 ? itemIndex : items.Count, paste with { Command = commandBase + paste.Command, Title = original.Title });
            var replacement = original with { Command = commandBase + paste.Command, Enabled = paste.Enabled };
            if (nodeIndex >= 0) nodes[nodeIndex] = replacement;
            else
            {
                // Prefer the existing edit area. Without one, stay before the final
                // separator/properties block rather than below native properties.
                int anchor = nodes.FindLastIndex(x => items.Any(item => item.Command == x.Command && item.Group.Length == 0 && item.Verb is "cut" or "copy"));
                int insertion = anchor >= 0 ? anchor + 1 : nodes.FindLastIndex(x => x.IsSeparator);
                if (insertion < 0) insertion = nodes.FindIndex(x => items.Any(item => item.Command == x.Command && item.Verb == "properties"));
                nodes.Insert(insertion < 0 ? nodes.Count : insertion, replacement);
            }
        }
    }
}
