using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;

namespace BetterCM.Services;

internal static class BackdropUiChecks
{
    internal static async Task RunAsync(Action<string, bool, string?> check)
    {
        var window = new ContextMenuWindow { ShowActivated = false };
        List<MenuAction> actions = [new() { Id = "native:1", Title = "材质测试动作", ShellKey = "fixture" }];
        ShellMenuNode[] tree = [new("材质二级", Children: [new("材质三级", Children: [new("材质测试动作", 1)])])];
        try
        {
            foreach (bool compact in new[] { false, true })
            foreach (bool light in new[] { false, true })
            foreach (string material in new[] { "acrylic", "mica", "solid" })
            {
                ThemeService.ApplyResolved(light, material, false, true);
                window.ShowAt(24, 40, new(), new() { ClassicMenu = true, CompactMenu = compact, EnableAnimations = false }, [], [], [], false, actions, nativeMenu: tree);
                window.OpenGroup(window.VisibleRows[0]);
                var side = window.OpenSubmenu!; side.OpenGroup(side.VisibleRows[0]);
                var levels = new[] { window, side, side.OpenSubmenu! };
                var layoutWait = System.Diagnostics.Stopwatch.StartNew();
                while (levels.Any(x => !x.ContentCoversWindow) && layoutWait.ElapsedMilliseconds < 1000) await Task.Delay(16);
                string label = $"{material}/{(light ? "浅色" : "深色")}/{(compact ? "紧凑" : "标准")}";
                bool active = material != "solid" && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
                check("三级菜单与复用窗口材质一致：" + label,
                    levels.All(x => x.HasTransparentContentWindow && x.ContentCoversWindow &&
                        ThemeService.HasStableMenuBackdrop(x) == active &&
                        (!active || ThemeService.MenuMaterial(x) == material) &&
                        ((SolidColorBrush)x.FindResource("MenuSurfaceBg")).Color.A == (active ? light ? 12 : 8 : 255)),
                    string.Join("; ", levels.Select(x => $"{ThemeService.MenuMaterial(x)}; active={ThemeService.HasStableMenuBackdrop(x)}; cover={x.ContentCoversWindow}; alpha={((SolidColorBrush)x.FindResource("MenuSurfaceBg")).Color.A}; layout={System.Text.Json.JsonSerializer.Serialize(x.LayoutState)}")));
                // DWMWA_USE_HOSTBACKDROPBRUSH is set-only on this Windows build:
                // DwmGetWindowAttribute returns E_INVALIDARG, not the flag's value.
                check("每级 HWND 成功启用或释放亚克力背景画刷：" + label,
                    levels.All(x => ThemeService.MenuHostBackdropEnabled(x) == (active && material == "acrylic")), null);
                foreach (bool highContrast in new[] { false, true })
                {
                    ThemeService.ApplyResolved(light, "acrylic", highContrast, false);
                    check("三级菜单关闭透明/高对比度一致回退：" + label + "/" + highContrast,
                        levels.All(x => !ThemeService.HasStableMenuBackdrop(x) && ((SolidColorBrush)x.FindResource("MenuSurfaceBg")).Color.A == 255), null);
                }
                // Switch back while all cached cascade HWNDs still exist.
                ThemeService.ApplyResolved(light, material, false, true);
                window.CloseMenu("材质回归", true);
            }
        }
        finally { window.Close(); ThemeService.Apply("system"); }
    }
}
