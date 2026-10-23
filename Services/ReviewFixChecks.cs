using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class ReviewFixChecks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var app = (App)Application.Current;
        string? originalConfig = File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null;
        string? originalBackup = File.Exists(app.ConfigPath + ".bak") ? File.ReadAllText(app.ConfigPath + ".bak") : null;
        if (!app.ReloadConfiguration()) throw new InvalidOperationException(app.ConfigurationError);
        var saved = app.Config.Clone();
        var fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var menuField = typeof(App).GetField("_menu", fields)!;
        var settingsField = typeof(App).GetField("_settings", fields)!;
        var inputField = typeof(App).GetField("_input", fields)!;
        var pausedField = typeof(App).GetField("_interceptionPaused", fields)!;
        object? oldMenu = menuField.GetValue(app), oldSettings = settingsField.GetValue(app), oldInput = inputField.GetValue(app);
        bool wasPaused = (bool)pausedField.GetValue(app)!;
        var menu = new ContextMenuWindow { Host = app, ShowActivated = false };
        var settings = new MainWindow(app) { ShowActivated = false };
        settings.Closing += (_, e) => e.Cancel = false;
        InputService? input = null;
        try
        {
            menuField.SetValue(app, menu); settingsField.SetValue(app, settings);
            settings.Show();
            var hotkey = (TextBox)settings.FindName("HotkeyText");
            var save = (Button)settings.FindName("SaveButton");
            hotkey.Text = "Ctrl+Alt+F20";
            const string pin = "review-fix:pin";
            app.ToggleShellPin(pin);
            check(new("ReviewFix/InternalPin", "应用内固定同步设置基线并保留未保存编辑"), save.IsEnabled && hotkey.Text == "Ctrl+Alt+F20" && settings.ReadSettings().PinnedShellActions.Contains(pin), null);
            settings.RefreshShellItems([new(1, "测试系统项目", "", pin, "open", true)], "目标");
            check(new("ReviewFix/InternalPinRow", "应用内固定同步系统项目勾选状态"), ((ShellRow)((DataGrid)settings.FindName("ShellGrid")).Items[0]).Pinned, null);
            app.ToggleShellPin(pin);
            check(new("ReviewFix/InternalUnpin", "应用内取消固定保留编辑且不会重新保存旧固定值"), save.IsEnabled && hotkey.Text == "Ctrl+Alt+F20" && !settings.ReadSettings().PinnedShellActions.Contains(pin), null);
            var previous = app.Config.Clone();
            app.Config.ReplaceExplorerMenu = !previous.ReplaceExplorerMenu;
            settings.NotifyConfigurationChanged(previous);
            check(new("ReviewFix/InternalPause", "应用内暂停同步接管选项并保留其他编辑"), save.IsEnabled && settings.ReadSettings().ReplaceExplorerMenu == app.Config.ReplaceExplorerMenu && hotkey.Text == "Ctrl+Alt+F20", null);
            settings.Hide();
            app.ShowSettings();
            check(new("ReviewFix/PreserveDraftOnReopen", "隐藏设置窗口后重新呼出保留未保存修改并提示状态"), hotkey.Text == "Ctrl+Alt+F20" && ((TextBlock)settings.FindName("SaveStatus")).Text.Contains("未保存", StringComparison.Ordinal), null);

            var customDialog = new CustomActionWindow(new());
            customDialog.ApplySelectedExecutable(@"C:\Tools\MyEditor.exe");
            bool populatedDefaultTitle = customDialog.ExecutableText == @"C:\Tools\MyEditor.exe" && customDialog.TitleText == "使用 MyEditor 打开";
            customDialog.ApplySelectedExecutable(@"C:\Tools\OtherTool.exe");
            bool preservedExistingTitle = customDialog.ExecutableText == @"C:\Tools\OtherTool.exe" && customDialog.TitleText == "使用 MyEditor 打开";
            customDialog.Close();
            var classicOption = (CheckBox)settings.FindName("ClassicOption");
            classicOption.IsChecked = false;
            var customGrid = (DataGrid)settings.FindName("CustomGrid");
            var customRows = (System.Collections.ObjectModel.ObservableCollection<CustomActionItem>)customGrid.ItemsSource;
            customRows.Clear();
            var firstCustom = new CustomActionItem { Title = "第一项", Executable = Environment.ProcessPath! };
            var secondCustom = new CustomActionItem { Title = "第二项", Executable = Environment.ProcessPath! };
            customRows.Add(firstCustom); customRows.Add(secondCustom);
            customGrid.SelectedItem = secondCustom;
            var moveUp = (Button)settings.FindName("MoveCustomUpButton");
            var moveDown = (Button)settings.FindName("MoveCustomDownButton");
            bool beforeMove = moveUp.IsEnabled && !moveDown.IsEnabled;
            moveUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool afterUp = customRows[0] == secondCustom && !moveUp.IsEnabled && moveDown.IsEnabled;
            moveDown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool afterDown = customRows[1] == secondCustom && moveUp.IsEnabled && !moveDown.IsEnabled;
            customRows.Clear();
            check(new("ReviewFix/CustomOrderAndBrowse", "自定义动作支持浏览回填默认名称与上下移动排序"), populatedDefaultTitle && preservedExistingTitle && beforeMove && afterUp && afterDown, null);
            settings.Hide();

            var options = new AppConfig { ClassicMenu = true, EnableAnimations = false, Material = "solid", CustomActions = [new() { Title = "批量检查", Executable = Environment.ProcessPath!, SelectionMode = "each" }] };
            var context = new SelectedContext { Items = [new(Path.Combine(fixture, "first.txt"), false), new(Path.Combine(fixture, "other", "second.txt"), false)], Source = "跨目录选区回归" };
            var show = typeof(App).GetMethod("ShowMenuAsync", fields)!;
            foreach (bool classic in new[] { true, false })
            {
                options.ClassicMenu = classic;
                await (Task)show.Invoke(app, [32, 48, context, options])!;
                var toolbar = (ItemsControl)menu.FindName("Toolbar");
                var actions = toolbar.Items.Cast<MenuAction>().Concat(menu.VisibleRows.Where(x => !x.IsGroup).Select(x => x.Action).OfType<MenuAction>()).ToArray();
                check(new("ReviewFix/ShellFailure/" + classic, "Shell 失败保留路径与自定义动作并移除原生命令 " + classic), menu.IsOpen && actions.Any(x => x.Id == "path" && x.Enabled) && actions.Any(x => x.Id == options.CustomActions[0].Id && x.Enabled) && !actions.Any(x => ShellCommandMap.IsNative(x.Id)), null);
                check(new("ReviewFix/FallbackMode/" + classic, "失败回退不会改写用户经典模式偏好 " + classic), options.ClassicMenu == classic && ((TextBlock)menu.FindName("ShellStatus")).Text.Contains("已回退", StringComparison.Ordinal), null);
            }
            var search = (TextBox)menu.FindName("Search");
            await menu.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            menu.FocusSearch();
            search.Text = "批量";
            var surface = (FrameworkElement)menu.FindName("Surface");
            void Key(Key key) => surface.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(surface), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            search.Clear(); bool searchFocused = search.Focus(); Key(System.Windows.Input.Key.Up);
            var list = (ListBox)menu.FindName("ActionsList");
            check(new("ReviewFix/SearchUp", "搜索框按上键选择最后一个可用动作"), searchFocused && list.SelectedItem == menu.VisibleRows.LastOrDefault(x => x.Enabled && !x.IsSeparator), "focused=" + searchFocused + ", selected=" + (list.SelectedItem as MenuEntry)?.Id);
            searchFocused = search.Focus(); Key(System.Windows.Input.Key.Down);
            check(new("ReviewFix/SearchDown", "搜索框按下键选择第一个可用动作"), searchFocused && list.SelectedItem == menu.VisibleRows.FirstOrDefault(x => x.Enabled && !x.IsSeparator), "focused=" + searchFocused + ", selected=" + (list.SelectedItem as MenuEntry)?.Id);
            search.Text = "批量";
            Key(System.Windows.Input.Key.Escape);
            check(new("ReviewFix/SearchEscape", "Escape 先清除搜索并保持菜单打开"), search.Text.Length == 0 && menu.IsOpen, null);
            Key(System.Windows.Input.Key.Escape);
            check(new("ReviewFix/SearchEscapeClose", "搜索为空时 Escape 正常关闭菜单"), !menu.IsOpen, null);
            using var cancellation = new CancellationTokenSource();
            var operation = new OperationWindow(cancellation, "正在启动自定义动作", "批量检查");
            check(new("ReviewFix/CustomProgress", "自定义批量操作使用动作标题而非哈希文案"), operation.Title == "正在启动自定义动作" && ((StackPanel)operation.Content).Children.OfType<TextBlock>().First().Text == "批量检查", null);
            operation.Close();

            settingsField.SetValue(app, null);
            input = new InputService(); inputField.SetValue(app, input);
            app.Config.ReplaceExplorerMenu = true; pausedField.SetValue(app, true);
            typeof(App).GetMethod("ApplyInput", fields)!.Invoke(app, null);
            check(new("ReviewFix/SafetyPause", "异常暂停保持 Windows 右键并在状态中解释恢复方式"), !input.InterceptionActive && app.InputStatus.Contains("异常保护", StringComparison.Ordinal), null);
            app.ApplySavedInput();
            check(new("ReviewFix/ExplicitResume", "用户显式启用清除异常暂停并恢复实际鼠标接管"), !(bool)pausedField.GetValue(app)! && input.InterceptionActive && !app.InputStatus.Contains("异常保护", StringComparison.Ordinal), input.Status);
        }
        finally
        {
            input?.Dispose();
            menuField.SetValue(app, oldMenu); settingsField.SetValue(app, oldSettings); inputField.SetValue(app, oldInput); pausedField.SetValue(app, wasPaused);
            app.Config.PinnedShellActions = saved.PinnedShellActions; app.Config.ReplaceExplorerMenu = saved.ReplaceExplorerMenu;
            if (originalConfig == null) File.Delete(app.ConfigPath); else File.WriteAllText(app.ConfigPath, originalConfig);
            if (originalBackup == null) File.Delete(app.ConfigPath + ".bak"); else File.WriteAllText(app.ConfigPath + ".bak", originalBackup);
            settings.Close(); menu.CloseMenu("回归结束", true); menu.Close();
        }
    }
}
