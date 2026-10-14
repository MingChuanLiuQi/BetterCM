using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

// In-process WPF component checks exercise real templates, animations and dispatcher scheduling.
public static class MenuUiChecks
{
    public static async Task RunAsync(Action<string, bool, string?> check)
    {
        void Check(string name, bool passed, string? detail = null) => check(name, passed, detail);
        ThemeService.Apply("dark", "solid");
        var menu = new ContextMenuWindow { ShowActivated = false };
        var context = new SelectedContext { Source = "组件回归", CurrentDirectory = AppContext.BaseDirectory };
        var config = new AppConfig { ShowMoreItems = false, EnableAnimations = false, IncludeShellActions = true, Material = "solid" };
        List<MenuAction> primary = [new() { Id = "custom:fixture", Title = "测试动作" }];
        List<MenuAction> more = [new() { Id = "extra", Title = "其他动作" }];
        List<MenuAction> shell = [new() { Id = "zip", ShellKey = "zip", Group = "NanaZip", Title = "添加到压缩包" }, new() { Id = "sha", ShellKey = "sha", Group = "NanaZip", Title = "SHA-256" }];
        var list = (ListBox)menu.FindName("ActionsList");
        List<MenuEntry> Rows() => list.Items.Cast<MenuEntry>().ToList();
        int Show(AppConfig options) => menu.ShowAt(24, 40, context, options, [], primary, more, false);
        async Task WaitForExpansion()
        {
            var timeout = Stopwatch.StartNew();
            while (menu.IsExpanding && timeout.ElapsedMilliseconds < 1000) await Task.Delay(16);
            await Task.Delay(20); // Let the final WM_SIZE update WPF's public metrics.
        }
        try
        {
            int generation = Show(config); menu.SetShell(generation, shell, null); menu.UpdateLayout();
            var replacement = new ExpansionAnimator(); int replacementCompleted = 0, discardedCompleted = 0; bool replacedInitial = false;
            replacement.Play(0, 1, 60, "linear", _ =>
            {
                if (replacedInitial) return;
                replacedInitial = true;
                replacement.Play(10, 20, 60, "linear", _ => { }, () => replacementCompleted++);
            }, () => discardedCompleted++);
            await Task.Delay(250);
            Check("动画首帧回调重开后旧请求不覆盖新计时器或完成回调", !replacement.Active && replacementCompleted == 1 && discardedCompleted == 0);
            bool replacedFinal = false; int newCompleted = 0, oldCompleted = 0;
            replacement.Play(0, 1, 60, "linear", value =>
            {
                if (value < 1 || replacedFinal) return;
                replacedFinal = true;
                replacement.Play(0, 1, 60, "linear", _ => { }, () => newCompleted++);
            }, () => oldCompleted++);
            await Task.Delay(350);
            Check("动画终帧回调重开后旧清理不取消新动画", replacedFinal && newCompleted == 1 && oldCompleted == 0 && !replacement.Active);
            replacement.Cancel();
            Check("材质与内容分层且透明内容子窗口归属正确", menu.HasTransparentContentWindow);
            Check("独立内容层继承正确文字颜色", ((TextBlock)menu.FindName("TargetName")).Foreground == menu.FindResource("TextBrush"));
            var search = (TextBox)menu.FindName("Search");
            menu.Activate(); list.Focus(); await Task.Delay(20);
            Check("空搜索框未聚焦显示提示", menu.SearchHintVisible);
            search.Focus(); await Task.Delay(20);
            Check("空搜索框聚焦隐藏提示只保留光标", search.IsKeyboardFocusWithin && !menu.SearchHintVisible);
            search.Text = "x"; list.Focus();
            Check("搜索框有文字时不叠加提示", !menu.SearchHintVisible);
            search.Text = "";
            Check("清空搜索并失焦恢复提示", menu.SearchHintVisible);
            Check("搜索提示由 Adorner 呈现，不占用编辑区域或鼠标输入", Descendants((DependencyObject)menu.FindName("Surface")).OfType<SearchPlaceholder>().All(x => !x.IsHitTestVisible) && menu.FindName("SearchHint") == null);
            Check("菜单原生窗口不绘制系统标题栏按钮", (NativeMethods.GetWindowLongPtr(new WindowInteropHelper(menu).Handle, NativeMethods.GWL_STYLE).ToInt64() & NativeMethods.WS_SYSMENU) == 0);
            Check("关闭更多折叠时立即显示额外动作", Rows().Any(x => x.Id == "extra") && ((Button)menu.FindName("More")).Visibility == Visibility.Collapsed);
            Check("取消更多折叠仍保留分组并包含全部原生命令", Rows().Any(x => x.IsGroup && x.Title == "NanaZip") && Leaves(Rows()).Count(x => x.Id is "zip" or "sha") == 2 && Rows().All(x => x.Id != "zip" && x.Id != "sha"));
            double normalHeight = menu.Height;
            config.CompactMenu = true; config.ShowMoreItems = true; generation = Show(config); menu.SetShell(generation, shell, null);
            Check("小尺寸模式关闭搜索、更多和图标栏但保留分组", search.Visibility == Visibility.Collapsed && ((Button)menu.FindName("More")).Visibility == Visibility.Collapsed && ((ItemsControl)menu.FindName("Toolbar")).Visibility == Visibility.Collapsed && Rows().Any(x => x.IsGroup) && Leaves(Rows()).Any(x => x.Id == "sha"));
            Check("小尺寸模式实际宽高更紧凑且内容覆盖窗口", menu.Width == 300 && menu.Height < normalHeight && menu.ContentCoversWindow);
            config.CompactMenu = false; generation = Show(config); menu.SetShell(generation, shell, null); menu.ExpandMore(); menu.UpdateLayout();
            var group = Descendants(list).OfType<Button>().First(x => x.Tag is MenuEntry { IsGroup: true });
            group.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            group.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent });
            group.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
            Check("分组悬停先等待延时，主菜单不向下展开", menu.OpenSubmenu == null && Rows().Any(x => x.Title == "NanaZip" && !x.IsExpanded));
            double parentHeight = menu.Height;
            group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); menu.UpdateLayout();
            var side = menu.OpenSubmenu!;
            Check("点击分组向侧边展开且主菜单高度不变", side?.IsOpen == true && side.VisibleRows.Any(x => x.Id == "zip" && x.Action == shell[0]) && Math.Abs(menu.Height - parentHeight) < 1 && Rows().All(x => x.Id != "zip"));
            NativeMethods.GetWindowRect(new WindowInteropHelper(menu).Handle, out var parentRect);
            NativeMethods.GetWindowRect(new WindowInteropHelper(side!).Handle, out var sideRect);
            Check("侧边子菜单具有真实独立窗口、透明内容层和完整布局", side!.HasTransparentContentWindow && side.ContentCoversWindow && sideRect.Left >= parentRect.Right - 1);
            var sideScroll = Descendants((DependencyObject)side.FindName("ActionsList")).OfType<ScrollViewer>().First();
            Check("短侧边菜单使用实际 DPI 测量且无需滚动", sideScroll.ComputedVerticalScrollBarVisibility != Visibility.Visible, side.LayoutDiagnostics + $"; extent={sideScroll.ExtentHeight}, viewport={sideScroll.ViewportHeight}, scroll={sideScroll.ActualHeight}, list={((ListBox)side.FindName("ActionsList")).ActualHeight}; " + string.Join("; ", Descendants((DependencyObject)side.FindName("ActionsList")).OfType<ListBoxItem>().Select(x => $"row actual={x.ActualHeight}, desired={x.DesiredSize.Height}, margin={x.Margin}")));
            MenuObservation? observation = null;
            menu.ObservationChanged += state => observation = state;
            group = Descendants((DependencyObject)side.FindName("ActionsList")).OfType<Button>().First(x => x.Tag is MenuEntry { Title: "校验与哈希" });
            group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("嵌套分组继续侧边展开并保留原始叶子命令", side.OpenSubmenu?.VisibleRows.Single().Action == shell[1] && side.VisibleRows.All(x => x.Id != "sha"));
            NativeMethods.GetWindowRect(new WindowInteropHelper(side.OpenSubmenu!).Handle, out var nestedRect);
            Check("点击任意子菜单不会被输入监控当作外部点击", observation?.Submenus?.Count == 2 && observation.ContainsHandle(new WindowInteropHelper(side).Handle) && observation.ContainsPoint(new() { X = nestedRect.Left + 8, Y = nestedRect.Top + 8 }));
            using (var diagnostic = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(menu.CascadeLayout)))
                Check("多级菜单诊断可序列化实际句柄和物理边界", diagnostic.RootElement.GetProperty("Regions").GetArrayLength() == 2 && diagnostic.RootElement.GetProperty("Regions")[1].GetProperty("Right").GetInt32() == nestedRect.Right);
            group = Descendants(list).OfType<Button>().First(x => x.Tag is MenuEntry { Title: "NanaZip" });
            group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("重复点击当前分组保持子菜单可用", ReferenceEquals(menu.OpenSubmenu, side) && side.IsOpen);
            ((TextBox)menu.FindName("Search")).Text = "SHA";
            Check("搜索关闭子菜单并直达深层命令", menu.OpenSubmenu == null && !side.IsOpen && Rows().Count == 1 && Rows()[0].Action == shell[1]);
            menu.CloseMenu("组件检查");
            Check("禁用动画时关闭同步完成", !menu.IsVisible && !menu.IsOpen);
            config.ShowMoreItems = true; generation = Show(config); menu.SetShell(generation, shell, null);
            Check("启用更多折叠时初始只显示一级动作", Rows().Count == 1 && ((Button)menu.FindName("More")).Visibility == Visibility.Visible);
            double collapsedHeight = menu.Height;
            ((Button)menu.FindName("More")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("点击更多立即展开且分组保持合并", Rows().Any(x => x.Id == "extra") && Rows().Any(x => x.IsGroup && !x.IsExpanded));
            var menuHandle = new WindowInteropHelper(menu).Handle;
            NativeMethods.GetWindowRect(menuHandle, out var expandedBounds);
            Check("展开更多后原生窗口同步增高，底部内容不被旧尺寸裁切", menu.Height > collapsedHeight && expandedBounds.Bottom - expandedBounds.Top == (int)Math.Ceiling(menu.Height * NativeMethods.GetDpiForWindow(menuHandle) / 96.0), $"collapsed={collapsedHeight:F2}, expanded={menu.Height:F2}, native={expandedBounds.Bottom - expandedBounds.Top}, dpi={NativeMethods.GetDpiForWindow(menuHandle)}");
            menu.MaxHeight = 280; menu.SetShell(generation, shell, "第一行：系统项目读取失败\n第二行：可以重试\n第三行：也可以使用 Shift + 右键");
            var surfaceForBounds = (FrameworkElement)menu.FindName("Surface");
            NativeMethods.GetWindowRect(menuHandle, out var limitedBounds);
            var footer = ((Button)menu.FindName("SettingsButton"));
            var footerBottom = footer.PointToScreen(new Point(0, footer.ActualHeight));
            Check("多行 Shell 提示下网格自动分配列表高度且底栏留在窗口内", surfaceForBounds.ActualHeight <= menu.MaxHeight + 1 && footerBottom.Y <= limitedBounds.Bottom + 1);
            Check("Shell 叶子显示来源，分组具有独立视觉标识", Rows().Any(x => x.IsGroup && x.IsShell && x.Source == "Shell"));
            menu.CloseMenu("展开动效检查", true);
            config.EnableAnimations = true; config.OpenAnimation = "none"; config.MoreExpansionMs = config.GroupExpansionMs = 100;
            Show(config); double beforeExpansion = menu.Height; menu.ExpandMore();
            bool motionAvailable = ExpansionAnimator.Enabled(config, config.MoreExpansionAnimation);
            Check("更多展开开始时保留起始尺寸并启动有限动画", !motionAvailable || menu.IsExpanding && Math.Abs(menu.Height - beforeExpansion) < 1);
            await WaitForExpansion();
            Check("更多展开动画结束后释放帧订阅并保持最终内容", !menu.IsExpanding && Rows().Any(x => x.Id == "extra") && menu.Height > beforeExpansion);
            foreach (string expansionStyle in new[] { "fade", "slide", "fade-slide" })
            {
                menu.CloseMenu("逐帧展开检查", true); config.MoreExpansionAnimation = expansionStyle; config.MoreExpansionMs = 180;
                Show(config); menu.UpdateLayout(); beforeExpansion = menu.Height;
                var frames = new List<(int Parent, int Child, double Opacity, double Translation)>();
                ListBoxItem? revealed = null;
                void ExpansionFrame(object? sender, EventArgs e)
                {
                    if (menu.ExpansionFrameUpdating) return;
                    NativeMethods.GetWindowRect(menuHandle, out var parentFrame);
                    NativeMethods.GetWindowRect(menu.ContentHandle, out var childFrame);
                    revealed ??= list.ItemContainerGenerator.ContainerFromItem(Rows().FirstOrDefault(x => x.Id == "extra")) as ListBoxItem;
                    frames.Add((parentFrame.Bottom - parentFrame.Top, childFrame.Bottom - childFrame.Top, revealed?.Opacity ?? 1, (revealed?.RenderTransform as TranslateTransform)?.Y ?? 0));
                }
                menu.ExpandMore();
                CompositionTarget.Rendering += ExpansionFrame;
                try
                {
                    var expansionWait = Stopwatch.StartNew();
                    while (menu.IsExpanding && expansionWait.ElapsedMilliseconds < 2000) await Task.Delay(16);
                    await Task.Delay(30);
                }
                finally { CompositionTarget.Rendering -= ExpansionFrame; }
                bool increasing = frames.Zip(frames.Skip(1)).All(pair => pair.Second.Parent >= pair.First.Parent && pair.Second.Opacity + .001 >= pair.First.Opacity && pair.Second.Translation + .001 >= pair.First.Translation);
                Check("展开逐帧不回缩、不先亮后暗且内容窗口不重复缩放：" + expansionStyle, frames.Count > 2 && increasing && frames.Select(x => x.Child).Distinct().Count() == 1 && !menu.IsExpanding && menu.Height > beforeExpansion, $"frames={frames.Count}, increasing={increasing}, samples={string.Join(';', frames.Select(x => $"{x.Parent}/{x.Child}/{x.Opacity:F3}/{x.Translation:F3}"))}");
                Check("展开终点行可见且最终动画状态保持：" + expansionStyle, revealed != null && Math.Abs(revealed.Opacity - 1) < .001 && Math.Abs((revealed.RenderTransform as TranslateTransform)?.Y ?? 0) < .001);
            }
            config.MoreExpansionAnimation = "fade-slide"; config.MoreExpansionMs = 100;
            menu.CloseMenu("长列表动效检查", true);
            var longMore = Enumerable.Range(0, 40).Select(i => new MenuAction { Id = "long:" + i, Title = "长列表项目 " + i }).ToList();
            menu.ShowAt(24, 40, context, config, [], primary, longMore, false); menu.ExpandMore(); await WaitForExpansion();
            Check("长列表展开不固化中间视口高度，结束后完整使用允许高度", !menu.IsExpanding && Math.Abs(menu.Height - menu.MaxHeight) < 1 && Math.Abs(menu.ActualHeight - menu.MaxHeight) < 1 && menu.ContentCoversWindow, System.Text.Json.JsonSerializer.Serialize(menu.LayoutState));
            list.ScrollIntoView(Rows().Last()); list.UpdateLayout(); await Task.Delay(30);
            Check("展开结束后滚动复用行恢复可见与正确动作", Descendants(list).OfType<ListBoxItem>().All(row => row.Opacity >= .999) && Descendants(list).OfType<Button>().Any(button => button.Tag is MenuEntry { Id: "long:39" }));
            menu.CloseMenu("展开期间滚动检查", true); config.MoreExpansionMs = 400;
            menu.ShowAt(24, 40, context, config, [], primary, longMore, false); menu.ExpandMore(); await Task.Delay(80);
            list.ScrollIntoView(Rows().Last()); list.UpdateLayout(); await Task.Delay(30);
            var lateRow = list.ItemContainerGenerator.ContainerFromItem(Rows().Last()) as ListBoxItem;
            Check("展开期间滚入的虚拟化行接入当前动画进度", !motionAvailable || menu.IsExpanding && lateRow != null && lateRow.Opacity > 0 && lateRow.Opacity < 1);
            await WaitForExpansion();
            Check("展开结束后滚入行保持最终可见状态而不重播动画", lateRow != null && Math.Abs(lateRow.Opacity - 1) < .001);
            config.MoreExpansionMs = 100;
            menu.CloseMenu("工作区底部展开检查", true);
            var bottomMonitor = NativeMethods.MonitorFromPoint(new() { X = 24, Y = 40 }, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var bottomWork = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            NativeMethods.GetMonitorInfo(bottomMonitor, ref bottomWork);
            menu.ShowAt(bottomWork.rcWork.Left + 24, bottomWork.rcWork.Bottom - 60, context, config, [], primary, longMore, false);
            bool alignedAtEdge = true; int edgeFrames = 0;
            var edgeOffsets = new List<string>();
            void EdgeFrame(object? sender, EventArgs e)
            {
                if (menu.ExpansionFrameUpdating) return;
                var origin = new NativeMethods.POINT(); NativeMethods.ClientToScreen(menuHandle, ref origin);
                NativeMethods.GetWindowRect(menu.ContentHandle, out var childFrame);
                alignedAtEdge &= childFrame.Top == origin.Y && childFrame.Left == origin.X;
                if (childFrame.Top != origin.Y || childFrame.Left != origin.X) edgeOffsets.Add($"{edgeFrames}:{childFrame.Left - origin.X},{childFrame.Top - origin.Y}");
                edgeFrames++;
            }
            menu.ExpandMore(); CompositionTarget.Rendering += EdgeFrame;
            try { await WaitForExpansion(); } finally { CompositionTarget.Rendering -= EdgeFrame; }
            NativeMethods.GetWindowRect(menuHandle, out var edgeBounds);
            Check("工作区底部展开逐帧同步透明内容位置且底栏不被裁切", alignedAtEdge && edgeFrames > 0 && edgeBounds.Bottom <= bottomWork.rcWork.Bottom && footer.PointToScreen(new Point(0, footer.ActualHeight)).Y <= edgeBounds.Bottom, $"frames={edgeFrames}, aligned={alignedAtEdge}, offsets={string.Join(';', edgeOffsets)}");
            menu.CloseMenu("恢复分组夹具", true); Show(config); menu.ExpandMore(); await Task.Delay(160);
            menu.SetShell(menu.VisibleGeneration, shell, null); await Task.Delay(160);
            group = Descendants(list).OfType<Button>().First(x => x.Tag is MenuEntry { IsGroup: true });
            parentHeight = menu.Height;
            group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("分组动画只作用于侧边窗口且命令身份不变", !menu.IsExpanding && menu.OpenSubmenu?.VisibleRows.Any(x => x.Id == "zip" && x.Action == shell[0]) == true && Math.Abs(menu.Height - parentHeight) < 1);
            search.Text = "SHA";
            Check("动画中搜索立即取消旧展开，直接显示匹配结果", !menu.IsExpanding && Rows().Count == 1 && Rows()[0].Action == shell[1]);
            search.Text = ""; menu.CloseMenu("动画重开准备", true); Show(config); menu.ExpandMore();
            generation = Show(config); await Task.Delay(160);
            Check("快速重开取消展开动画，旧完成回调不会撑大新菜单", !menu.IsExpanding && menu.VisibleGeneration == generation && Rows().Count == 1);
            menu.ExpandMore(); menu.CloseMenu("中途关闭", true); await Task.Delay(160);
            Check("展开中关闭后释放动画，旧帧不能重新显示菜单", !menu.IsVisible && !menu.IsExpanding);
            config.ShowMoreItems = false;
            generation = menu.ShowAt(24, 40, context, config, [], primary, more, false, shell);
            double initialHeight = menu.Height;
            NativeMethods.GetWindowRect(menuHandle, out var initialBounds);
            await Task.Delay(250);
            NativeMethods.GetWindowRect(menuHandle, out var settledBounds);
            Check("关闭折叠时首次显示完整菜单且没有二次展开或缩短", !menu.IsExpanding && Math.Abs(menu.Height - initialHeight) < .1 && initialBounds.Bottom - initialBounds.Top == settledBounds.Bottom - settledBounds.Top && Rows().Any(x => x.IsGroup));
            var zip = Rows().Single(x => x.Title == "NanaZip");
            menu.OpenGroup(zip); var firstSide = menu.OpenSubmenu!;
            menu.CloseMenu("父菜单关闭", true);
            Check("关闭主菜单同时释放侧边窗口及嵌套菜单", !firstSide.IsOpen && menu.OpenSubmenu == null);
            var handleForWork = new WindowInteropHelper(menu).Handle;
            var monitor = NativeMethods.MonitorFromPoint(new() { X = 24, Y = 40 }, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            NativeMethods.GetMonitorInfo(monitor, ref info);
            menu.ShowAt(info.rcWork.Right - 8, info.rcWork.Bottom - 80, context, config, [], primary, more, false, shell);
            menu.OpenGroup(Rows().Single(x => x.Title == "NanaZip"));
            NativeMethods.GetWindowRect(handleForWork, out parentRect);
            NativeMethods.GetWindowRect(new WindowInteropHelper(menu.OpenSubmenu!).Handle, out sideRect);
            Check("屏幕右侧子菜单改向左展开且不超出工作区", sideRect.Right <= parentRect.Left + 1 && sideRect.Left >= info.rcWork.Left && sideRect.Top >= info.rcWork.Top && sideRect.Bottom <= info.rcWork.Bottom);
            menu.CloseMenu("同层切换检查", true);
            var another = new MenuAction { Id = "other-tool", ShellKey = "other", Group = "另一工具", Title = "原生命令" };
            menu.ShowAt(24, 40, context, config, [], primary, more, false, shell.Concat(new[] { another }).ToList());
            menu.OpenGroup(Rows().Single(x => x.Title == "NanaZip")); var oldSide = menu.OpenSubmenu!;
            oldSide.OpenGroup(oldSide.VisibleRows.Single(x => x.IsGroup)); var oldNested = oldSide.OpenSubmenu!;
            menu.OpenGroup(Rows().Single(x => x.Title == "另一工具"));
            Check("切换同层工具复用窗口、清除旧子菜单并保留新命令", menu.OpenSubmenu == oldSide && !oldNested.IsOpen && oldSide.OpenSubmenu == null && menu.OpenSubmenu.VisibleRows.Single().Action == another);
            menu.CloseMenu("恢复动效夹具", true); config.ShowMoreItems = true;
            config.EnableAnimations = false;
            menu.CloseMenu("计时检查");
            var hover = new HoverExpansion(); int fired = 0;
            var watch = Stopwatch.StartNew(); var completion = new TaskCompletionSource();
            hover.Schedule(100, () => { fired++; completion.SetResult(); });
            Check("悬停请求仅安排计时，不同步展开", fired == 0 && hover.IsPending);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check("悬停展开等待设定延时并停止计时器", fired == 1 && watch.ElapsedMilliseconds >= 100 && !hover.IsPending, $"{watch.Elapsed.TotalMilliseconds:F2} ms");
            hover.Schedule(80, () => fired++); hover.Cancel(); await Task.Delay(120);
            Check("移出或关闭取消待展开请求", fired == 1 && !hover.IsPending);
            hover.Schedule(80, () => fired += 100); hover.Schedule(80, () => fired += 10); await Task.Delay(140);
            Check("新悬停请求替换旧请求，旧回调不执行", fired == 11 && !hover.IsPending);
            config.EnableAnimations = true; config.OpenAnimationMs = 70; config.CloseAnimationMs = 80;
            config.OpenAnimation = config.CloseAnimation = "fade"; config.OpenAnimationMs = 300;
            Show(config); await Task.Delay(30);
            var surface = (FrameworkElement)menu.FindName("Surface"); double partialOpacity = surface.Opacity;
            menu.CloseMenu("中断入场动画");
            Check("中断入场后关闭从当前视觉状态继续，无闪回", !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast || surface.Opacity <= partialOpacity + .01);
            await Task.Delay(120); config.OpenAnimationMs = 70;
            foreach (var style in new[] { "none", "fade", "slide", "zoom", "fade-slide", "fade-zoom" })
            {
                config.OpenAnimation = config.CloseAnimation = style;
                generation = Show(config); await Task.Delay(110);
                Check("打开动画完成并恢复视觉状态：" + style, menu.IsOpen && ((FrameworkElement)menu.FindName("Surface")).Opacity == 1);
                menu.CloseMenu("动画关闭检查"); await Task.Delay(120);
                Check("关闭动画完成并隐藏窗口：" + style, !menu.IsVisible && !menu.IsOpen);
            }
            foreach (var easing in new[] { "cubic", "quintic", "sine", "linear" })
            {
                config.AnimationEasing = easing; config.OpenAnimation = config.CloseAnimation = "fade-zoom";
                Show(config); menu.CloseMenu("旧菜单关闭");
                bool logicalClosed = !menu.IsOpen; generation = Show(config); await Task.Delay(150);
                Check("快速重开不被旧关闭动画隐藏：" + easing, logicalClosed && menu.IsOpen && menu.VisibleGeneration == generation);
                int count = Rows().Count; menu.Dismiss(generation - 1, MenuDismissReason.Pointer);
                menu.SetShell(generation - 1, shell, null);
                Check("动画重开后拒绝旧关闭消息与旧 Shell 数据：" + easing, menu.IsOpen && Rows().Count == count);
                menu.CloseMenu("轮次结束", true);
            }
            Show(config);
            foreach (var material in new[] { "mica", "acrylic", "solid" })
            {
                ThemeService.Apply("dark", material);
                var handle = new WindowInteropHelper(menu).Handle;
                int result = NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, out int backdrop, sizeof(int));
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                bool enabled = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && !SystemParameters.HighContrast && key?.GetValue("EnableTransparency") is not 0 && material != "solid";
                bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
                bool controlled = ThemeService.HasStableMenuBackdrop(menu);
                Check("系统材质与纯色回退：" + material, supported ? result >= 0 && backdrop == (enabled && !controlled ? material == "mica" ? NativeMethods.DWMSBT_TABBEDWINDOW : NativeMethods.DWMSBT_TRANSIENTWINDOW : NativeMethods.DWMSBT_NONE) : result < 0 && ((SolidColorBrush)menu.FindResource("MenuSurfaceBg")).Color.A == 255, $"DWM={backdrop}, controller={controlled}, HRESULT={result}");
                Check("菜单材质不依赖前台焦点：" + material, material == "solid" || !enabled || controlled);
                Check("材质不启用 WPF 分层透明窗口：" + material, !menu.AllowsTransparency);
                Check("系统材质可透过底层背景，回退背景保持不透明：" + material, ((SolidColorBrush)menu.FindResource("WindowBg")).Color.A == (enabled ? 0 : 255));
                var checkbox = new CheckBox { IsChecked = true, Style = (Style)Application.Current.FindResource(typeof(CheckBox)) }; checkbox.ApplyTemplate();
                var mark = (TextBlock)checkbox.Template.FindName("Mark", checkbox);
                bool darkLegible = mark.Visibility == Visibility.Visible && ((SolidColorBrush)mark.Foreground).Color.A == 255;
                ThemeService.Apply("light", material);
                Check("深浅主题下勾选标记与下拉背景保持可读：" + material, darkLegible && ((SolidColorBrush)mark.Foreground).Color.A == 255 && ((SolidColorBrush)menu.FindResource("MenuBg")).Color.A == 255);
            }
        }
        finally { menu.CloseMenu("测试结束", true); menu.Close(); ThemeService.Apply("system"); }
        await NativeUiChecks.RunAsync(Check);
    }
    private static IEnumerable<MenuEntry> Leaves(IEnumerable<MenuEntry> rows) => rows.SelectMany(x => x.IsGroup ? Leaves(x.Children) : x.IsSeparator ? [] : new[] { x });
    internal static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root) => Descendants(root);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
