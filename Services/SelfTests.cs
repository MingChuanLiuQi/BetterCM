using System.Text.Json;
using System.Windows.Interop;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class SelfTests
{
    public static async Task<bool> RunAsync(string? output)
    {
        var reportPath = Path.GetFullPath(output ?? Path.Combine(ConfigStore.DataDirectory, "self-test.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var fixture = Path.Combine(Path.GetDirectoryName(reportPath)!, "fixtures");
        var results = new List<RegressionCheck>();
        void Check(string name, bool passed, string? detail = null) => results.Add(new(name, passed, detail));
        try
        {
            var selections = await RegressionFixtures.CreateAsync(fixture);
            var single = selections.Single; var multiple = selections.Multiple;
            var textPath = single.PrimaryPath;
            var virtualContext = ExplorerContextService.Build([], "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "此电脑", "测试", true);
            var navigationCom = new List<object>();
            object Own(object value) { navigationCom.Add(value); return value; }
            try
            {
                dynamic automation = Own(Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!);
                dynamic downloads = Own(automation.NameSpace("::{374DE290-123F-4565-9164-39C4925E467B}"));
                dynamic downloadsSelf = Own(downloads.Self);
                string name = downloadsSelf.Name, path = downloadsSelf.Path;
                string first = ExplorerContextService.ResolveNavigation((object)automation, ["快速访问结束 - " + name + " (已固定)", "桌面"], "", Own);
                string second = ExplorerContextService.ResolveNavigation((object)automation, ["快速访问开始 - " + name + " (已固定)", "桌面"], "", Own);
                Check("下载导航项使用实际 Shell 解析路径而非 shell 协议", first.Equals(path, StringComparison.OrdinalIgnoreCase) && Directory.Exists(first), first);
                Check("下载重新固定后的开始结束标记不改变实际目标", second.Equals(first, StringComparison.OrdinalIgnoreCase));
                Check("固定状态可从 Home 属性读取而不依赖 UIA 标记", ExplorerContextService.IsHomePinned((object)automation, path, Own));
                using var pinnedMenu = new ShellMenuSession([path], 0, pinnedNavigation: true);
                Check("固定导航项使用 Home 命名空间并保留取消固定动作", pinnedMenu.Items.Any(x => x.Verb.Contains("unpin", StringComparison.OrdinalIgnoreCase) || x.Title.Contains("取消固定", StringComparison.Ordinal) || x.Title.Contains("Unpin", StringComparison.OrdinalIgnoreCase)));
            }
            finally { for (int i = navigationCom.Count - 1; i >= 0; i--) if (System.Runtime.InteropServices.Marshal.IsComObject(navigationCom[i])) System.Runtime.InteropServices.Marshal.ReleaseComObject(navigationCom[i]); }
            using (var contextService = new ExplorerContextService())
            {
                var captured = await contextService.CaptureAsync(0, [textPath]);
                Check("真实上下文进程可往返物理目标", captured.PrimaryPath == textPath && captured.HasFileTarget);
            }
            await MenuUiChecks.RunAsync(Check);
            await SettingsUiChecks.RunAsync(Check);
            await ShellOperationChecks.RunAsync(fixture, Check);
            await ShellLaunchChecks.RunAsync(fixture, Check);
            await DeferredMenuChecks.RunAsync(fixture, Check);
            using var bridge = new ShellBridge();
            var shell = await bridge.OpenAsync(single);
            Check("独立进程可读取真实 Shell 菜单", shell.Success && shell.Items?.Count > 0, shell.Error);
            if (shell.Success)
            {
                Check("Shell 项目均为可定位的叶子动作", shell.Items!.All(x => x.Command >= 0 && x.Title.Length > 0 && x.Key.Length > 0));
                Check("Shell 菜单包含属性动作", shell.Items!.Any(x => x.Verb == "properties"));
                Check("Shell 协议保留原生树和分隔线", shell.Menu?.Count > 0 && shell.Menu.Any(x => x.IsSeparator));
                var next = await bridge.OpenAsync(multiple);
                Check("多选 Shell 菜单读取", next.Success && next.Items?.Count > 0, next.Error);
                var stale = await bridge.InvokeAsync(shell.Session!, shell.Items![0].Command);
                Check("拒绝旧菜单会话执行", !stale.Success);
            }
            var virtualShell = await bridge.OpenAsync(virtualContext);
            Check("此电脑背景可读取真实 Shell 菜单", virtualShell.Success && virtualShell.Items?.Count > 0, virtualShell.Error);
            using var testBridge = new ShellBridge(TimeSpan.FromSeconds(2), true);
            var cached = await testBridge.OpenAsync(single);
            await new ActionExecutor(testBridge).ExecuteAsync("properties", single, existingShell: cached);
            Check("属性复用菜单会话且仅读取一次", testBridge.OpenCount == 1);
            nint desktopWindow = NativeMethods.FindWindow("Progman", null);
            if (desktopWindow != 0)
            {
                var desktopContext = ExplorerContextService.Build([], Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "桌面", "桌面背景测试", true) with { SourceWindow = desktopWindow };
                var desktopMenu = await bridge.OpenAsync(desktopContext);
                Check("真实桌面视图包含刷新命令", desktopMenu.Success && desktopMenu.Items!.Any(x => x.Verb.Equals("refresh", StringComparison.OrdinalIgnoreCase) || MenuComposer.Caption(x.Title) == "刷新" || MenuComposer.Caption(x.Title).Equals("Refresh", StringComparison.OrdinalIgnoreCase)), desktopMenu.Error);
                Check("真实桌面视图包含查看与排序分组", desktopMenu.Success && desktopMenu.Items!.Select(x => x.Group).Distinct().Count() >= 2, desktopMenu.Error);
                var refreshed = await bridge.RefreshAsync(desktopContext);
                Check("刷新通过真实 ShellView 执行", refreshed.Success, refreshed.Error);
                var refreshItem = desktopMenu.Items?.FirstOrDefault(x => x.Enabled && (x.Verb.Equals("refresh", StringComparison.OrdinalIgnoreCase) || MenuComposer.Caption(x.Title) is "刷新" or "Refresh"));
                if (desktopMenu.Success && refreshItem != null)
                {
                    using var host = bridge.CurrentProcessId is int id ? System.Diagnostics.Process.GetProcessById(id) : null;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var invoked = await bridge.InvokeAsync(desktopMenu.Session!, refreshItem.Command);
                    Check("普通原生刷新 invoke 往返小于 1.5 秒", invoked.Success && clock.ElapsedMilliseconds < 1500, $"{clock.Elapsed.TotalMilliseconds:F2} ms");
                    bool exited = false;
                    if (host != null) { try { await host.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(2500)); exited = true; } catch (TimeoutException) { } }
                    Check("非新建背景命令无需三秒回调空等且宿主退出", exited, $"{clock.Elapsed.TotalMilliseconds:F2} ms");
                }
            }

        }
        catch (Exception ex) { Check("UI/Shell 检查异常", false, ex.ToString()); }
        bool passed = results.All(x => x.Passed);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { Version = AppInfo.Version, Build = AppInfo.Build, Environment = "InteractiveWindowsDesktop; Explorer; Clipboard; InstalledShellExtensions", Executable = Environment.ProcessPath, Time = DateTimeOffset.Now, Passed = passed, Failures = results.Count(x => !x.Passed), Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        return passed;
    }
}
