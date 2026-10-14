using BetterCM.Models;
namespace BetterCM.Services;

internal static class DeferredMenuChecks
{
    internal static async Task RunAsync(string fixture, Action<string, bool, string?> check)
    {
        var file = Path.Combine(fixture, "lazy-中文 & 文件.txt"); File.WriteAllText(file, "lazy fixture");
        nint desktop = NativeMethods.FindWindow("Progman", null);
        using var bridge = new ShellBridge();
        foreach (var pair in new[]
        {
            (Name: "文件", Context: new SelectedContext { CurrentDirectory = fixture, Items = [new(file, false)] }),
            (Name: "桌面", Context: new SelectedContext { CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), SourceWindow = desktop, ShellBackground = true })
        })
        {
            var full = await bridge.OpenAsync(pair.Context);
            var initial = await bridge.OpenAsync(pair.Context, deferSubmenus: true);
            check(pair.Name + "一级菜单按需读取但保留原始根项目及状态", full.Success && initial.Success && RootShape(full.Menu!).SequenceEqual(RootShape(initial.Menu!)), initial.Error);
            var group = initial.Menu!.FirstOrDefault(x => x.DeferredSubmenu != null);
            check(pair.Name + "原生子菜单初次呈现只保存会话令牌", group is { Children.Count: 0 }, null);
            if (group?.DeferredSubmenu is int token)
            {
                var child = await bridge.ExpandAsync(initial.Session!, token);
                var again = await bridge.ExpandAsync(initial.Session!, token);
                check(pair.Name + "单个子菜单可读取且重复读取不重复命令", child.Success && again.Success && child.Items!.Count == again.Items!.Count && child.Menu!.Count > 0, child.Error);
            }
            var completed = await bridge.ExpandAsync(initial.Session!, null);
            check(pair.Name + "搜索补全保留完整原生树和叶子状态", completed.Success && !NativeMenuTree.HasDeferred(completed.Menu!) && TreeShape(full.Menu!).SequenceEqual(TreeShape(completed.Menu!)), completed.Error);
            var invalid = await bridge.ExpandAsync(initial.Session!, int.MaxValue);
            check(pair.Name + "拒绝不属于当前会话的子菜单令牌", !invalid.Success, null);
            await bridge.OpenAsync(pair.Context);
            var stale = await bridge.ExpandAsync(initial.Session!, null);
            check(pair.Name + "拒绝旧菜单的延迟读取", !stale.Success, null);
        }
    }
    private static IEnumerable<string> RootShape(IReadOnlyList<ShellMenuNode> rows) => rows.Select(x => $"{x.Title}|{x.Enabled}|{x.IsSeparator}|{x.Checked}|{x.IsDefault}|{x.Shortcut}|{x.Mnemonic}|{x.Children != null}");
    private static IEnumerable<string> TreeShape(IReadOnlyList<ShellMenuNode> rows)
    {
        foreach (var row in rows)
        {
            yield return RootShape([row]).Single();
            if (row.Children != null) { yield return "{"; foreach (var child in TreeShape(row.Children)) yield return child; yield return "}"; }
        }
    }
}
