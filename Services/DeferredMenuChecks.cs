using BetterCM.Models;
namespace BetterCM.Services;

internal static class DeferredMenuChecks
{
    internal static async Task RunAsync(string fixture, Action<CheckIdentity, bool, string?> check)
    {
        var file = Path.Combine(fixture, "lazy-中文 & 文件.txt"); File.WriteAllText(file, "lazy fixture");
        string shortcut = Path.Combine(fixture, "BetterCM-pin-policy.lnk");
        object? automation = null, link = null;
        try
        {
            automation = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)automation!).CreateShortcut(shortcut);
            ((dynamic)link).TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "winver.exe");
            ((dynamic)link).Save();
        }
        finally
        {
            if (link != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(link);
            if (automation != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(automation);
        }
        nint desktop = NativeMethods.FindWindow("Progman", null);
        using var bridge = new ShellBridge();
        foreach (var pair in new[]
        {
            (Id: "file", Name: "文件", Context: new SelectedContext { CurrentDirectory = fixture, Items = [new(file, false)] }),
            (Id: "shortcut", Name: "快捷方式", Context: new SelectedContext { CurrentDirectory = fixture, Items = [new(shortcut, false)] }),
            (Id: "desktop", Name: "桌面", Context: new SelectedContext { CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), SourceWindow = desktop, ShellBackground = true })
        })
        {
            var full = await bridge.OpenAsync(pair.Context);
            check(new CheckIdentity("DeferredMenu/C009" + "/" + pair.Id, pair.Name + "完整读取不导入开始与任务栏固定命令"), full.Success && full.Items!.All(x => !UnsupportedShellCommand.IsProtectedPin(x.Verb)), full.Error);
            if (pair.Name == "快捷方式")
                check(new CheckIdentity("DeferredMenu/C008", "移除受保护入口后快捷方式的打开和属性仍保留"), full.Items?.Any(x => x.Verb == "open") == true && full.Items.Any(x => x.Verb == "properties"), full.Error);
            var initial = await bridge.OpenAsync(pair.Context, deferSubmenus: true);
            check(new CheckIdentity("DeferredMenu/C007" + "/" + pair.Id, pair.Name + "一级菜单按需读取但保留原始根项目及状态"), full.Success && initial.Success && RootShape(full.Menu!).SequenceEqual(RootShape(initial.Menu!)), initial.Error);
            var group = initial.Menu!.FirstOrDefault(x => x.DeferredSubmenu != null);
            check(new CheckIdentity("DeferredMenu/C006" + "/" + pair.Id, pair.Name + "原生子菜单初次呈现只保存会话令牌"), group is { Children.Count: 0 }, null);
            if (group?.DeferredSubmenu is int token)
            {
                var child = await bridge.ExpandAsync(initial.Session!, token);
                var again = await bridge.ExpandAsync(initial.Session!, token);
                check(new CheckIdentity("DeferredMenu/C005" + "/" + pair.Id, pair.Name + "单个子菜单可读取且重复读取不重复命令"), child.Success && again.Success && child.Items!.Count == again.Items!.Count && child.Menu!.Count > 0, child.Error);
            }
            var completed = await bridge.ExpandAsync(initial.Session!, null);
            check(new CheckIdentity("DeferredMenu/C004" + "/" + pair.Id, pair.Name + "搜索补全保留完整原生树和叶子状态"), completed.Success && !NativeMenuTree.HasDeferred(completed.Menu!) && TreeShape(full.Menu!).SequenceEqual(TreeShape(completed.Menu!)), completed.Error);
            check(new CheckIdentity("DeferredMenu/C003" + "/" + pair.Id, pair.Name + "延迟读取与搜索补全无受保护入口且命令仍对应原始树"), completed.Success && completed.Items!.All(x => !UnsupportedShellCommand.IsProtectedPin(x.Verb)) &&
                CommandIds(completed.Menu!).Order().SequenceEqual(completed.Items!.Select(x => x.Command).Order()), completed.Error);
            var invalid = await bridge.ExpandAsync(initial.Session!, int.MaxValue);
            check(new CheckIdentity("DeferredMenu/C002" + "/" + pair.Id, pair.Name + "拒绝不属于当前会话的子菜单令牌"), !invalid.Success, null);
            await bridge.OpenAsync(pair.Context);
            var stale = await bridge.ExpandAsync(initial.Session!, null);
            check(new CheckIdentity("DeferredMenu/C001" + "/" + pair.Id, pair.Name + "拒绝旧菜单的延迟读取"), !stale.Success, null);
        }
    }
    private static IEnumerable<int> CommandIds(IReadOnlyList<ShellMenuNode> rows)
    {
        foreach (var row in rows)
        {
            if (row.Command >= 0) yield return row.Command;
            if (row.Children != null) foreach (int command in CommandIds(row.Children)) yield return command;
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
