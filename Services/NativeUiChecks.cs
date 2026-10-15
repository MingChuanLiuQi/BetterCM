using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class NativeUiChecks
{
    internal static async Task RunAsync(Action<string, bool, string?> check)
    {
        ThemeService.Apply("dark", "mica");
        var window = new ContextMenuWindow { ShowActivated = false };
        window.PreparePresentation();
        List<MenuAction> commands = [new() { Id = "native:1", Title = "原生打开", ShellKey = "open" }, new() { Id = "native:2", Title = "原生查看", ShellKey = "view" }];
        ShellMenuNode[] native = [new("原生打开", 1, IsDefault: true), new("", IsSeparator: true), new("原生分组", Children: [new("原生查看", 2, Checked: true)], Mnemonic: "W")];
        var config = new AppConfig { ClassicMenu = true, ShowMoreItems = true, OpenAnimationMs = 160, CompactMenu = false };
        try
        {
            ThemeService.ApplyResolved(false, "mica", false, true);
            var paletteNames = new[] { "WindowBg", "MenuSurfaceBg", "TextBrush", "HoverBrush", "BorderBrush", "GroupRowBg", "SourceBadgeBg", "ErrorBrush" };
            var baseline = paletteNames.ToDictionary(x => x, x => ((SolidColorBrush)Application.Current.Resources[x]).Color);
            ThemeService.ApplyResolved(false, "mica", true, true);
            check("高对比度采用系统色并关闭材质", ((SolidColorBrush)Application.Current.Resources["MenuSurfaceBg"]).Color == SystemColors.WindowColor && ThemeService.Diagnostics.Contains("纯色回退", StringComparison.Ordinal), null);
            ThemeService.ApplyResolved(false, "mica", false, true);
            check("退出高对比度恢复全部正常色板，无系统设置副作用", paletteNames.All(x => ((SolidColorBrush)Application.Current.Resources[x]).Color == baseline[x]), null);
            window.ShowAt(26, 42, new() { CurrentDirectory = AppContext.BaseDirectory }, config, [new() { Title = "不应注入" }], [new() { Title = "不应注入" }], [], false, commands, nativeMenu: native);
            check("经典模式首次呈现完整原生树且不注入内置入口", window.VisibleRows.Count == 3 && window.VisibleRows[1].IsSeparator && window.VisibleRows[2].Children.Single().Checked && ((ItemsControl)window.FindName("Toolbar")).Visibility == Visibility.Collapsed && ((Button)window.FindName("More")).Visibility == Visibility.Collapsed, null);
            var surface = (FrameworkElement)window.FindName("Surface");
            var transform = (TransformGroup)surface.RenderTransform;
            var translation = (TranslateTransform)transform.Children[1];
            var scale = (ScaleTransform)transform.Children[0];
            var handle = new WindowInteropHelper(window).Handle;
            bool IsPopupToolWindow(ContextMenuWindow popup)
            {
                var popupHandle = new WindowInteropHelper(popup).Handle;
                long style = NativeMethods.GetWindowLongPtr(popupHandle, NativeMethods.GWL_STYLE).ToInt64();
                long extended = NativeMethods.GetWindowLongPtr(popupHandle, NativeMethods.GWL_EXSTYLE).ToInt64();
                return (style & NativeMethods.WS_SYSMENU) == 0 &&
                    (extended & NativeMethods.WS_EX_TOOLWINDOW) != 0 && (extended & NativeMethods.WS_EX_APPWINDOW) == 0;
            }
            check("根菜单以弹出工具窗口呈现，不声明为普通应用窗口", IsPopupToolWindow(window), null);
            var parentIme = NativeMethods.ImmGetContext(handle);
            check("菜单材质父窗口不关联输入法，搜索内容窗口独立保留", parentIme == 0, null);
            if (parentIme != 0) NativeMethods.ImmReleaseContext(handle, parentIme);
            NativeMethods.GetWindowRect(handle, out var initial);
            var samples = new List<(double Time, double Opacity, double Y, double Scale, int Height)>();
            var timer = Stopwatch.StartNew();
            void Frame(object? sender, EventArgs e)
            {
                NativeMethods.GetWindowRect(handle, out var rect);
                samples.Add((timer.Elapsed.TotalMilliseconds, surface.Opacity, translation.Y, scale.ScaleY, rect.Bottom - rect.Top));
            }
            CompositionTarget.Rendering += Frame;
            try
            {
                // Wait for a presented terminal frame, not just wall time on a busy desktop.
                while (timer.Elapsed.TotalMilliseconds < 2000 && (window.PresentationPending || !samples.Any(x => x.Time >= window.FirstFrameDelayMs + config.OpenAnimationMs + 25 && Math.Abs(x.Opacity - 1) < .001)))
                    await Task.Delay(25);
            }
            finally { CompositionTarget.Rendering -= Frame; }
            var end = samples.Where(x => x.Time >= window.FirstFrameDelayMs + config.OpenAnimationMs + 25).ToArray();
            NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int));
            check("动画首帧呈现后解除遮蔽，准备过程有界", !window.PresentationPending && cloaked == 0 && window.FirstFrameDelayMs < 250, $"ready={window.FirstFrameDelayMs:F2} ms");
            check("打开动画结束前后原生尺寸连续且不缩短", samples.Count >= 2 && samples.All(x => x.Height == initial.Bottom - initial.Top), $"frames={samples.Count}");
            check("打开动画终点保持透明度和变换不回跳", end.Length > 0 && end.All(x => Math.Abs(x.Opacity - 1) < .001 && Math.Abs(x.Y) < .001 && Math.Abs(x.Scale - 1) < .001), $"endFrames={end.Length}");
            check("菜单禁用重复 DWM 窗口过渡以只播放内容动画", window.NativeTransitionResult >= 0, $"HRESULT=0x{window.NativeTransitionResult:X8}");
            window.OpenGroup(window.VisibleRows[2]);
            check("原生子菜单保持勾选状态并向侧边展开", window.OpenSubmenu?.VisibleRows.Single().Checked == true && window.ContentCoversWindow, null);
            check("级联子菜单采用相同的弹出工具窗口类型", window.OpenSubmenu is { } popup && IsPopupToolWindow(popup), null);
            window.CloseMenu("原生回归结束", true);
            window.ShowAt(26, 42, new(), config, [], [], [], false, commands, nativeMenu: native);
            window.CloseMenu("首帧前取消", true);
            await Task.Delay(180);
            check("首帧前取消不会迟到重新显示", !window.IsOpen && !window.PresentationPending, null);
            var noAnimation = config.Clone(); noAnimation.EnableAnimations = false;
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, commands, nativeMenu: native);
            NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DWMWA_CLOAKED, out cloaked, sizeof(int));
            check("取消遮蔽后无动画重开仍可正常显示", window.IsOpen && cloaked == 0, null);
            window.CloseMenu("无动画验证结束", true);
            config.CompactMenu = true;
            bool refreshed = false;
            window.ShowAt(26, 42, new(), config, [], [], [], false, commands, nativeMenu: native, nativeRefresh: () => { refreshed = true; return Task.CompletedTask; });
            check("经典紧凑模式保留层级且关闭搜索", window.VisibleRows.Count == 3 && window.VisibleRows[2].IsGroup && ((TextBox)window.FindName("Search")).Visibility == Visibility.Collapsed, null);
            check("菜单访问键不进入输入法组合而搜索仍支持 IME", !InputMethod.GetIsInputMethodEnabled(surface) && InputMethod.GetIsInputMethodEnabled((TextBox)window.FindName("Search")), null);
            void Key(FrameworkElement target, System.Windows.Input.Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Key(surface, System.Windows.Input.Key.W);
            check("经典访问键使用原生助记键打开对应子菜单", window.OpenSubmenu?.IsOpen == true, null);
            if (window.OpenSubmenu is { } side) Key((FrameworkElement)side.FindName("Surface"), System.Windows.Input.Key.Left);
            Key(surface, System.Windows.Input.Key.F5);
            check("经典模式 F5 直接调用绑定视图刷新且关闭菜单", refreshed && !window.IsOpen, null);
            foreach (bool compact in new[] { false, true })
            {
                var metricsConfig = new AppConfig { CompactMenu = compact, EnableAnimations = false };
                ShellMenuNode[] groups = [new("短分组", Children: [new("原生打开", 1)]), new("长分组", Children: Enumerable.Range(0, 8).Select(_ => new ShellMenuNode("原生查看", 2)).ToArray())];
                window.ShowAt(26, 42, new(), metricsConfig, [], [], [], false, commands, nativeMenu: groups);
                window.OpenGroup(window.VisibleRows[0]);
                var layoutSide = window.OpenSubmenu!; var sideHandle = new WindowInteropHelper(layoutSide).Handle;
                window.OpenGroup(window.VisibleRows[1]);
                layoutSide = window.OpenSubmenu!;
                var rootList = (ListBox)window.FindName("ActionsList"); var childList = (ListBox)layoutSide.FindName("ActionsList");
                var parentRow = (FrameworkElement)rootList.ItemContainerGenerator.ContainerFromIndex(1);
                var firstChild = (FrameworkElement)childList.ItemContainerGenerator.ContainerFromIndex(0);
                NativeMethods.GetWindowRect(handle, out var parentRect); NativeMethods.GetWindowRect(sideHandle, out var childRect);
                int expectedGap = (int)Math.Round(MenuMetrics.CascadeGap * NativeMethods.GetDpiForWindow(handle) / 96.0);
                check((compact ? "紧凑" : "普通") + "模式长短子菜单统一宽度间隙且首行对齐", layoutSide.Width == window.Width && childRect.Left - parentRect.Right == expectedGap && Math.Abs(firstChild.PointToScreen(new()).Y - parentRow.PointToScreen(new()).Y) <= 1, null);
                check((compact ? "紧凑" : "普通") + "模式切换分组复用 HWND、恢复点击并保持行高", sideHandle == new WindowInteropHelper(layoutSide).Handle && layoutSide.IsHitTestVisible && ((FrameworkElement)layoutSide.FindName("Surface")).IsHitTestVisible && Math.Abs(parentRow.ActualHeight - firstChild.ActualHeight) < .01, null);
                window.CloseMenu("间距验证", true);
                check((compact ? "紧凑" : "普通") + "模式隐藏的复用子菜单移除旧可访问内容", PresentationSource.FromVisual(firstChild) == null, null);
            }
            var loading = new TaskCompletionSource<NativeMenuPage>();
            foreach (bool compact in new[] { false, true })
            {
                var density = new AppConfig { CompactMenu = compact, EnableAnimations = false };
                ShellMenuNode[] uniform = [new("短项目", 1), new("短项目", 2), new("短项目", 1)];
                window.ShowAt(26, 42, new(), density, [], [], [], false, commands, nativeMenu: uniform);
                double shortHeight = window.Height;
                ShellMenuNode[] mixed = [new("短项目", 1), new("第一行\n第二行", 2, Shortcut: new string('W', 160)), new("短项目", 1)];
                window.ShowAt(26, 42, new(), density, [], [], [], false, commands, nativeMenu: mixed);
                var mixedList = (ListBox)window.FindName("ActionsList"); mixedList.UpdateLayout();
                var first = (ListBoxItem)mixedList.ItemContainerGenerator.ContainerFromIndex(0);
                var multiline = (ListBoxItem)mixedList.ItemContainerGenerator.ContainerFromIndex(1);
                var mixedScroll = MenuUiChecks.VisualDescendants(mixedList).OfType<ScrollViewer>().First();
                check((compact ? "紧凑" : "普通") + "模式热缓存下多行项目单独测量且不出现多余滚动", window.Height > shortHeight + 5 && multiline.ActualHeight > first.ActualHeight + 5 && mixedScroll.ComputedVerticalScrollBarVisibility != Visibility.Visible, window.LayoutDiagnostics);
                var hint = MenuUiChecks.VisualDescendants(multiline).OfType<TextBlock>().Single(text => text.Name == "EntryHint");
                var title = MenuUiChecks.VisualDescendants(multiline).OfType<TextBlock>().Single(text => text.Name == "EntryTitle");
                check((compact ? "紧凑" : "普通") + "模式长提示宽度受限且标题仍有空间，完整提示可查看", hint.ActualWidth <= 120.01 && title.ActualWidth > 0 && hint.ToolTip as string == new string('W', 160), null);
                window.ShowAt(26, 42, new(), density, [], [], [], false, commands, nativeMenu: uniform);
                check((compact ? "紧凑" : "普通") + "模式多行异常不污染普通单行缓存", Math.Abs(window.Height - shortHeight) < .01, null);
            }
            window.CloseMenu("多行高度回归结束", true);
            ShellMenuNode[] lazy = [new("延迟分组", Children: [], DeferredSubmenu: 0)];
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => loading.Task);
            window.OpenGroup(window.VisibleRows.Single()); window.CloseMenu("取消延迟分组", true);
            loading.SetResult(new([new("原生打开", 1)], commands)); await Task.Delay(40);
            check("关闭后延迟子菜单结果不能重新打开窗口", !window.IsOpen && window.OpenSubmenu == null, null);
            var obsolete = new TaskCompletionSource<NativeMenuPage>(); var fresh = new TaskCompletionSource<NativeMenuPage>();
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => obsolete.Task);
            window.OpenGroup(window.VisibleRows.Single());
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => fresh.Task);
            obsolete.SetResult(new([new("旧请求错误目标", 1)], commands)); await Task.Delay(30);
            check("新菜单复用相同令牌也不会接收旧请求树或动作", window.VisibleRows.Single().DeferredSubmenu == 0 && window.VisibleRows.Single().Children.Count == 0, null);
            window.OpenGroup(window.VisibleRows.Single()); fresh.SetResult(new([new("当前正确目标", 1)], commands)); await Task.Delay(30);
            check("拒绝旧请求后新分组仍可正常读取与展开", window.OpenSubmenu?.VisibleRows.Single().Title == "当前正确目标", null);
            window.CloseMenu("代次复用回归结束", true);
            var cacheResult = new TaskCompletionSource<NativeMenuPage>(); int cacheLoads = 0;
            ShellMenuNode[] cacheTree = [new("等待分组", Children: [], DeferredSubmenu: 7), new("即时分组", Children: [new("即时动作", 1)])];
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, commands, nativeMenu: cacheTree, nativeLoad: _ => { cacheLoads++; return cacheResult.Task; });
            var waitingEntry = window.VisibleRows[0]; window.OpenGroup(waitingEntry); window.OpenGroup(window.VisibleRows[1]);
            cacheResult.SetResult(new([new("缓存动作", 2, Checked: true)], commands)); await Task.Delay(30);
            check("同会话合法晚到分组结果可缓存且不抢开当前子菜单", window.OpenSubmenu?.VisibleRows.Single().Title == "即时动作" && waitingEntry.DeferredSubmenu == null && waitingEntry.Children.Single().Checked, null);
            window.OpenGroup(waitingEntry);
            check("被取消悬停的合法分组再次打开复用原生命令且不重查", cacheLoads == 1 && window.OpenSubmenu?.VisibleRows.Single().Action?.Id == "native:2", null);
            window.CloseMenu("合法缓存回归结束", true);
            var groupA = new TaskCompletionSource<NativeMenuPage>(); var groupB = new TaskCompletionSource<NativeMenuPage>();
            ShellMenuNode[] concurrent = [new("A", Children: [], DeferredSubmenu: 10), new("B", Children: [], DeferredSubmenu: 11)];
            int groupLoads = 0;
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], true, [], nativeMenu: concurrent, nativeLoad: token => { groupLoads++; return token == 10 ? groupA.Task : groupB.Task; });
            window.OpenGroup(window.VisibleRows[0]); window.OpenGroup(window.VisibleRows[1]); window.OpenGroup(window.VisibleRows[1], true);
            groupA.SetResult(new([new("A 的动作", 1)], commands)); await Task.Delay(30);
            check("过期分组响应不会抢开当前点击的分组", window.OpenSubmenu == null, null);
            // Observe the transferred focus intent before normal foreground-loss handling.
            // A headless launcher has no Windows entitlement to steal the foreground;
            // sustained foreground/cascade behavior is checked by the live input fixture.
            groupB.SetResult(new([new("B 的动作", 1)], commands));
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Normal);
            var focusedSide = window.OpenSubmenu!;
            check("加载途中点击同一分组保留焦点意图且只查询一次", groupLoads == 2 && focusedSide?.VisibleRows.Single().Title == "B 的动作" && ((ListBox)focusedSide.FindName("ActionsList")).SelectedIndex == 0, $"loads={groupLoads}, rootOpen={window.IsOpen}, sideOpen={focusedSide?.IsOpen}");
            check("侧边子菜单不支持隐藏搜索聚焦", focusedSide != null && !focusedSide.FocusSearch() && !((TextBox)focusedSide.FindName("Search")).IsKeyboardFocusWithin, null);
            window.CloseMenu("分组焦点回归结束", true);
            var compactSearch = noAnimation.Clone(); compactSearch.CompactMenu = true;
            window.ShowAt(26, 42, new(), compactSearch, [], [], [], false, commands, nativeMenu: native);
            check("紧凑根菜单不支持隐藏搜索聚焦", !window.FocusSearch(), null);
            window.CloseMenu("紧凑搜索回归结束", true);
            var searching = new TaskCompletionSource<NativeMenuPage>();
            int allLoads = 0;
            ShellMenuNode[] partial = [lazy[0], new("原生查看", 2)];
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [commands[1]], nativeMenu: partial, nativeLoad: token => { if (token == null) allLoads++; return searching.Task; });
            var searchBox = (TextBox)window.FindName("Search"); searchBox.Text = "原";
            check("原生树补全未返回时立即过滤已加载项目", allLoads == 1 && window.VisibleRows.Single().Action?.Id == "native:2", null);
            searchBox.Text = "原生打开";
            check("补全途中继续输入立即更新结果且不重复读取", allLoads == 1 && window.VisibleRows.Count == 0, null);
            searching.SetResult(new([new("延迟分组", Children: [new("原生打开", 1)])], commands)); await Task.Delay(40);
            check("连续搜索只补全一次并直接呈现子菜单叶子", allLoads == 1 && window.VisibleRows.Single().Action?.Id == "native:1" && window.VisibleRows.Single().Title == "延迟分组 · 原生打开", null);
            window.CloseMenu("清空搜索补全回归", true);
            var clearedSearch = new TaskCompletionSource<NativeMenuPage>();
            ShellMenuNode[] clearedTree = [new("延迟分组", Children: [], DeferredSubmenu: 0), new("即时分组", Children: [new("原生查看", 2)])];
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [commands[1]], nativeMenu: clearedTree, nativeLoad: _ => clearedSearch.Task);
            searchBox.Text = "原"; searchBox.Text = "";
            var rootRowBefore = window.VisibleRows[0];
            window.OpenGroup(window.VisibleRows[1]);
            var openSideBefore = window.OpenSubmenu;
            clearedSearch.SetResult(new([new("延迟分组", Children: [new("原生打开", 1)]), new("即时分组", Children: [new("原生查看", 2)])], commands));
            await Task.Delay(40);
            check("清空搜索退回根菜单后迟到补全原位注入子项且不关闭已展开子菜单", ReferenceEquals(window.VisibleRows[0], rootRowBefore) && rootRowBefore.DeferredSubmenu == null && rootRowBefore.Children.Single().Action?.Id == "native:1" && window.OpenSubmenu == openSideBefore && openSideBefore?.IsOpen == true, null);
            window.CloseMenu("取消结果回归", true);
            var cancelledGroup = new TaskCompletionSource<NativeMenuPage>();
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => cancelledGroup.Task);
            double cleanHeight = window.Height; window.OpenGroup(window.VisibleRows.Single());
            cancelledGroup.SetException(new OperationCanceledException("旧请求已结束")); await Task.Delay(35);
            var status = (TextBlock)window.FindName("ShellStatus");
            check("被取代的分组读取不显示错误或改变菜单高度", status.Visibility == Visibility.Collapsed && Math.Abs(window.Height - cleanHeight) < .01 && window.VisibleRows.Single().DeferredSubmenu == 0, null);
            window.CloseMenu("失效错误回归", true);
            var lateFailure = new TaskCompletionSource<NativeMenuPage>();
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, commands, nativeMenu: cacheTree, nativeLoad: _ => lateFailure.Task);
            window.OpenGroup(window.VisibleRows[0]); window.OpenGroup(window.VisibleRows[1]); cleanHeight = window.Height;
            lateFailure.SetException(new IOException("旧分组失败夹具")); await Task.Delay(35);
            check("失去当前请求资格的读取失败不污染新子菜单", status.Visibility == Visibility.Collapsed && Math.Abs(window.Height - cleanHeight) < .01 && window.OpenSubmenu?.VisibleRows.Single().Title == "即时动作", null);
            window.CloseMenu("真实错误回归", true);
            var activeFailure = new TaskCompletionSource<NativeMenuPage>();
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => activeFailure.Task);
            window.OpenGroup(window.VisibleRows.Single()); activeFailure.SetException(new IOException("当前读取失败夹具")); await Task.Delay(35);
            check("当前请求的真实读取失败仍显示重试提示", status.Visibility == Visibility.Visible && status.Text.Contains("读取失败", StringComparison.Ordinal), null);
            window.CloseMenu("搜索取消回归", true);
            var cancelledSearch = new TaskCompletionSource<NativeMenuPage>();
            window.ShowAt(26, 42, new(), noAnimation, [], [], [], false, [], nativeMenu: lazy, nativeLoad: _ => cancelledSearch.Task);
            searchBox.Text = "查询夹具"; cleanHeight = window.Height;
            cancelledSearch.SetException(new OperationCanceledException("搜索请求已结束")); await Task.Delay(35);
            check("被取代的搜索补全不显示错误或改写菜单", status.Visibility == Visibility.Collapsed && Math.Abs(window.Height - cleanHeight) < .01 && window.VisibleRows.Count == 0, null);
        }
        finally { window.CloseMenu("清理原生回归", true); window.Close(); ThemeService.Apply("system"); }
    }
}
