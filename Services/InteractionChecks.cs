using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

// An interactive regression fixture uses real mouse/WinEvent notifications. It never injects input.
// The menu deliberately does not activate, reproducing the failure that Deactivated alone cannot detect.
public static class InteractionChecks
{
    public static async Task RunAsync(string? output)
    {
        var report = Path.GetFullPath(output ?? Path.Combine(ConfigStore.DataDirectory, "input-checks.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        ThemeService.Apply("dark");
        using var input = new InputService();
        input.Apply("Ctrl+Alt+F23", false);
        var menu = new ContextMenuWindow { ShowActivated = false };
        var host = new Window { Title = AppInfo.DisplayName + " · 交互回归检查", Width = 560, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        ThemeService.Attach(host);
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock { Text = "菜单未获得焦点时，外部输入仍应使它关闭", FontSize = 17, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = "测试已关闭右键接管。右侧菜单保持未激活；请依次点击下面的按钮。", Margin = new Thickness(0, 10, 0, 12), TextWrapping = TextWrapping.Wrap });
        var outside = new Button { Content = "1. 准备外部左键检查", Margin = new Thickness(0, 0, 0, 10) };
        var foreground = new Button { Content = "2. 验证前台窗口切换", IsEnabled = false };
        stack.Children.Add(outside); stack.Children.Add(foreground);
        var status = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(status); host.Content = stack;
        var completion = new TaskCompletionSource();
        var results = new List<CheckResult>();
        int pointerRequests = 0, foregroundRequests = 0, generation = 0;
        bool prepared = false;
        Window? other = null;
        menu.ObservationChanged += input.WatchMenu;
        input.MenuDismissRequested += (g, reason) =>
        {
            if (g == generation) { if (reason == MenuDismissReason.Pointer) pointerRequests++; else foregroundRequests++; }
            menu.Dismiss(g, reason);
        };
        void ShowInactiveMenu()
        {
            NativeMethods.GetWindowRect(new WindowInteropHelper(host).Handle, out var rect);
            generation = menu.ShowAt(rect.Right + 12, rect.Top, new() { Source = "交互检查", CurrentDirectory = Path.GetDirectoryName(report)! },
                new() { IncludeShellActions = false, EnableAnimations = true, OpenAnimationMs = 100, CloseAnimationMs = 70 }, [],
                [new() { Id = "fixture", Title = "这是未激活的测试菜单", Enabled = false }], [], false);
        }
        async Task Finish()
        {
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Version = AppInfo.Version, Executable = Environment.ProcessPath, Passed = results.Count == 2 && results.All(x => x.Passed), Results = results }, new JsonSerializerOptions { WriteIndented = true }));
            menu.Close(); other?.Close(); host.Close(); completion.TrySetResult();
        }
        outside.Click += async (_, _) =>
        {
            if (!prepared)
            {
                pointerRequests = 0; foregroundRequests = 0; ShowInactiveMenu(); prepared = true;
                outside.Content = "1. 验证外部左键点击"; status.Text = "菜单已显示且未激活，请再次点击此按钮。"; return;
            }
            outside.IsEnabled = false;
            bool logicallyClosed = !menu.IsOpen;
            await Task.Delay(300);
            bool pass = logicallyClosed && !menu.IsVisible && pointerRequests > 0;
            results.Add(new("启用动画的未激活菜单在外部左键后关闭，且按钮收到点击", pass));
            status.Text = pass ? "外部点击通过。接下来验证窗口切换。" : "外部点击失败，请保留报告。";
            outside.IsEnabled = false; foreground.IsEnabled = true;
        };
        foreground.Click += async (_, _) =>
        {
            pointerRequests = 0; foregroundRequests = 0; ShowInactiveMenu(); await Task.Delay(30);
            other = new Window { Title = "BetterCM · 前台切换测试", Width = 360, Height = 120, Content = new TextBlock { Text = "正在检查前台切换通知…", Margin = new Thickness(20) } };
            ThemeService.Attach(other); other.Show(); other.Activate();
            await Task.Delay(250);
            results.Add(new("未激活菜单在另一窗口成为前台后关闭", !menu.IsVisible && foregroundRequests > 0));
            await Finish();
        };
        host.Closed += (_, _) => completion.TrySetResult();
        host.Show(); host.Activate();
        await host.Dispatcher.InvokeAsync(ShowInactiveMenu, DispatcherPriority.ApplicationIdle);
        await completion.Task;
        menu.Close(); other?.Close();
    }
    private sealed record CheckResult(string Name, bool Passed);
}
