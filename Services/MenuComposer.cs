using System.Text.RegularExpressions;
using BetterCM.Models;
namespace BetterCM.Services;

// Presentation is separate from command identity. Grouping never changes a Shell session or command.
public static partial class MenuComposer
{
    private sealed class Group(string title, string id)
    {
        public string Title { get; } = title;
        public string Id { get; } = id;
        public List<object> Children { get; } = [];
        public bool IsShell { get; set; }
        public int Count => Children.Sum(x => x is Group g ? g.Count : 1);
    }
    [GeneratedRegex(@"[（(]&?[A-Za-z0-9][）)]$", RegexOptions.CultureInvariant)]
    private static partial Regex MnemonicSuffix();
    public static string Caption(string title) => MnemonicSuffix().Replace(title.Trim(), "").TrimEnd();
    public static bool DuplicatesBuiltin(MenuAction shell, IEnumerable<MenuAction> builtins)
    {
        var title = Caption(shell.Title);
        // Match only known equivalents outside extension groups; two different extension commands remain distinct.
        if (shell.Group.Length != 0) return false;
        string? id = shell.Verb.ToLowerInvariant() switch { "refresh" => "refresh", "open" => "open", "cut" => "cut", "copy" => "copy", "properties" => "properties", _ => null };
        // Explorer's actual view can omit a canonical verb for its native refresh item.
        if (shell.Verb.Length == 0 && (title == "刷新" || title.Equals("Refresh", StringComparison.OrdinalIgnoreCase))) id = "refresh";
        if (title.Equals("在终端中打开", StringComparison.OrdinalIgnoreCase) || title.Equals("Open in Terminal", StringComparison.OrdinalIgnoreCase)) id = "terminal";
        if (title.Equals("Open with Code", StringComparison.OrdinalIgnoreCase) || title == "通过 Code 打开") id = "code";
        return id != null && builtins.Any(x => x.Id == id && x.Enabled);
    }
    public static List<MenuEntry> Compose(IReadOnlyList<MenuAction> primary, IReadOnlyList<MenuAction> more,
        IReadOnlyList<MenuAction> shell, IReadOnlySet<string> pins, bool expanded, string filter, bool allItems = false)
    {
        var native = shell.DistinctBy(x => x.Id).ToList();
        var pinned = native.Where(x => x.ShellKey != null && pins.Contains(x.ShellKey)).ToList();
        var extra = native.Except(pinned).ToList();
        var rows = new List<MenuEntry>();
        MenuEntry Leaf(MenuAction a, int depth = 0, bool search = false) => new()
        {
            Id = a.Id,
            Title = search && a.Group.Length > 0 ? a.Group + " · " + Caption(a.Title) : Caption(a.Title),
            Glyph = a.Glyph,
            Hint = a.Shortcut,
            Detail = a.Detail,
            Action = a,
            Depth = depth,
            IsPinned = a.ShellKey != null && pins.Contains(a.ShellKey),
            Origin = a.ShellKey == null ? "" : a.Group.Length == 0 ? "Shell" : a.Group.Split(" · ")[0]
        };
        // Disabling More exposes its root entries. Related commands still share a submenu.
        expanded |= allItems;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            rows.AddRange(primary.Concat(pinned).Concat(more).Concat(extra)
                .Where(a => (a.Title + " " + a.Group + " " + a.Detail).Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(a => Leaf(a, search: true)));
            return rows;
        }
        var primaryGroups = new Dictionary<string, MenuEntry>(StringComparer.Ordinal);
        foreach (var action in primary.Concat(pinned))
        {
            if (action.ShellKey != null && !ActionCatalog.ById.ContainsKey(action.Id) || action.Group.Length == 0) { rows.Add(Leaf(action)); continue; }
            if (!primaryGroups.TryGetValue(action.Group, out var group))
            {
                group = new() { Id = "custom-group:" + action.Group, Title = action.Group, Glyph = "\uE8B7", IsGroup = true, Origin = "自定义" };
                primaryGroups.Add(action.Group, group); rows.Add(group);
            }
            group.Children = [.. group.Children, Leaf(action)];
        }
        if (!expanded) return rows;
        var roots = new Group("", "");
        void Add(MenuAction action, IEnumerable<string> path)
        {
            var group = roots;
            foreach (var part in path)
            {
                var id = group.Id + "/" + part;
                var child = group.Children.OfType<Group>().FirstOrDefault(x => x.Id == id);
                if (child == null) { child = new(part, id); group.Children.Add(child); }
                child.IsShell |= action.ShellKey != null;
                group = child;
            }
            group.Children.Add(action);
        }
        foreach (var a in more) Add(a, a.Group.Length > 0 ? [a.Group] : a.Id is "slash" or "wsl" ? ["复制其他格式路径"] : []);
        foreach (var a in extra)
        {
            var path = a.Group.Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            var rootCaption = Caption(a.Title);
            if (path.Count == 0)
            {
                if (rootCaption.StartsWith("Open Git ", StringComparison.OrdinalIgnoreCase)) path.Add("Git");
                else if (rootCaption.StartsWith("Open ", StringComparison.OrdinalIgnoreCase) ||
                    rootCaption.EndsWith("打开", StringComparison.Ordinal) && (rootCaption.StartsWith("用", StringComparison.OrdinalIgnoreCase) || rootCaption.StartsWith("使用", StringComparison.OrdinalIgnoreCase) || rootCaption.StartsWith("通过", StringComparison.OrdinalIgnoreCase) || rootCaption.StartsWith("在 ", StringComparison.OrdinalIgnoreCase))) path.Add("打开方式");
            }
            var archive = path.FirstOrDefault(x => x.Contains("zip", StringComparison.OrdinalIgnoreCase) || x.Contains("7-", StringComparison.OrdinalIgnoreCase));
            if (archive != null)
            {
                // Some extensions expose hashes/mail at their root instead of submenus. Keep these related tasks together.
                var caption = Caption(a.Title);
                if (path.All(x => !x.Contains("校验", StringComparison.OrdinalIgnoreCase) && !x.Contains("Hash", StringComparison.OrdinalIgnoreCase)) &&
                    (caption.StartsWith("CRC", StringComparison.OrdinalIgnoreCase) || caption.StartsWith("SHA", StringComparison.OrdinalIgnoreCase) || caption.StartsWith("MD5", StringComparison.OrdinalIgnoreCase) || caption == "*")) path.Add("校验与哈希");
                else if (path.Count == 1 && (caption.Contains("邮寄", StringComparison.OrdinalIgnoreCase) || caption.Contains("邮件", StringComparison.OrdinalIgnoreCase) || caption.Contains("email", StringComparison.OrdinalIgnoreCase))) path.Add("压缩并发送");
            }
            Add(a, path);
        }
        if (rows.Count > 0 && roots.Children.Count > 0) rows.Add(new() { IsSeparator = true });
        List<MenuEntry> BuildChildren(Group group)
        {
            var children = new List<MenuEntry>();
            foreach (var child in group.Children)
            {
                if (child is MenuAction a) children.Add(Leaf(a));
                else if (child is Group g)
                {
                    var glyph = g.Title.Contains("校验", StringComparison.OrdinalIgnoreCase) ? "\uE9D9" : g.Title == "复制其他格式路径" ? "\uE71B" : g.Title == "打开方式" ? "\uE8A7" : "\uE8B7";
                    children.Add(new() { Id = g.Id, Title = Caption(g.Title), Glyph = glyph, Hint = g.Count.ToString(), Detail = "悬停或点击打开子菜单 · " + g.Count + " 个动作", IsGroup = true, Origin = g.IsShell ? "Shell" : "内置", Children = BuildChildren(g) });
                }
            }
            return children;
        }
        rows.AddRange(BuildChildren(roots));
        return rows;
    }
}
