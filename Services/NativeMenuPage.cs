using BetterCM.Models;
namespace BetterCM.Services;

public sealed record NativeMenuPage(IReadOnlyList<ShellMenuNode> Menu, IReadOnlyList<MenuAction> Actions);
public static class NativeMenuTree
{
    public static bool HasDeferred(IReadOnlyList<ShellMenuNode> nodes) => nodes.Any(x => x.DeferredSubmenu != null || x.Children != null && HasDeferred(x.Children));
    public static IReadOnlyList<ShellMenuNode> Replace(IReadOnlyList<ShellMenuNode> nodes, int token, IReadOnlyList<ShellMenuNode> rows)
        => nodes.Select(x => x.DeferredSubmenu == token ? x with { Children = rows, DeferredSubmenu = null }
            : x.Children == null ? x : x with { Children = Replace(x.Children, token, rows) }).ToArray();
}
