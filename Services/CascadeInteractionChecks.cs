using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using BetterCM.Models;
using BetterCM.Views;

namespace BetterCM.Services;

// This fixture observes real input on the production menu windows; actions only record results.
public static class CascadeInteractionChecks
{
    public static async Task RunAsync(string output)
    {
        var report = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        ThemeService.Apply("dark");
        using var input = new InputService(); input.Apply("Ctrl+Alt+F23", false);
        var menu = new ContextMenuWindow { ShowInTaskbar = true };
        var host = new Window { Title = AppInfo.DisplayName + " · 侧边交互回归", Width = 560, Height = 300, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        ThemeService.Attach(host);
        var panel = new StackPanel { Margin = new Thickness(24) };
        var start = new Button { Content = "1. 准备多级侧边菜单" };
        var preparation = new Button { Content = "2. 准备取消延迟菜单检查", IsEnabled = false, Margin = new Thickness(0, 14, 0, 0) };
        var status = new TextBlock { Text = "先打开“测试工具”，再打开“深层分组”，点击“记录测试动作”。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
        panel.Children.Add(start); panel.Children.Add(preparation); panel.Children.Add(status); host.Content = panel;
        var results = new List<RegressionCheck>();
        var completion = new TaskCompletionSource();
        int pointerRequests = 0, invoked = 0, preparedGeneration = -100;
        bool preparing = false, cancelled = false;
        menu.ObservationChanged += input.WatchMenu;
        input.MenuDismissRequested += (generation, reason) =>
        {
            if (generation == preparedGeneration && preparing) { cancelled = true; preparing = false; input.WatchPreparation(null); }
            else { if (reason == MenuDismissReason.Pointer) pointerRequests++; menu.Dismiss(generation, reason); }
        };
        start.Click += (_, _) =>
        {
            start.IsEnabled = false; pointerRequests = 0;
            NativeMethods.GetWindowRect(new WindowInteropHelper(host).Handle, out var bounds);
            List<MenuAction> actions = [new() { Id = "record", Title = "记录测试动作", ShellKey = "fixture", Group = "测试工具 · 深层分组", Execute = () =>
            {
                invoked++;
                results.Add(new("多级侧边菜单内真实点击不会触发外部关闭", pointerRequests == 0));
                results.Add(new("侧边叶子执行一次并立即关闭整个菜单链", invoked == 1 && !menu.IsOpen && menu.OpenSubmenu == null));
                preparation.IsEnabled = true; status.Text = "侧边动作已记录。点击第 2 个按钮，然后再次点击它取消准备中的菜单。";
                return Task.CompletedTask;
            } }];
            menu.ShowAt(bounds.Right + 12, bounds.Top, new() { Source = "输入回归", CurrentDirectory = Path.GetDirectoryName(report)! }, new() { ShowMoreItems = false, CompactMenu = true }, [], [], [], shell: actions);
        };
        preparation.Click += async (_, _) =>
        {
            if (!preparing && !cancelled)
            {
                preparing = true;
                input.WatchPreparation(new(new WindowInteropHelper(host).Handle, default, preparedGeneration, (uint)Environment.TickCount));
                preparation.Content = "2. 取消准备中的菜单"; status.Text = "再次点击此按钮，模拟 Shell 尚未返回时的新输入。"; return;
            }
            results.Add(new("Shell 准备期间新输入取消旧请求且原按钮收到点击", cancelled && !preparing));
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Version = AppInfo.Version, Executable = Environment.ProcessPath, Passed = results.Count == 3 && results.All(x => x.Passed), Results = results }, new JsonSerializerOptions { WriteIndented = true }));
            menu.Close(); host.Close(); completion.TrySetResult();
        };
        host.Closed += (_, _) => completion.TrySetResult();
        host.Show(); host.Activate(); await completion.Task;
        input.WatchPreparation(null); menu.Close();
    }
}
