using System.Windows;
using System.Windows.Controls;
namespace BetterCM.Services;

public static class SettingsUiChecks
{
    public static async Task RunAsync(Action<string, bool, string?> check)
    {
        var app = (App)Application.Current;
        string? originalConfig = File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null;
        var window = new MainWindow(app) { ShowActivated = false };
        window.Closing += (_, e) => e.Cancel = false;
        try
        {
            check("设置构造完成即初始化经典开关及禁用态，无需手动刷新", ((CheckBox)window.FindName("ClassicOption")).IsChecked == app.Config.ClassicMenu && ((DataGrid)window.FindName("BuiltinGrid")).IsEnabled == !app.Config.ClassicMenu && ((StackPanel)window.FindName("CustomCommands")).IsEnabled == !app.Config.ClassicMenu, null);
            window.RefreshSettings(); window.Show(); await Task.Delay(20);
            var classic = (CheckBox)window.FindName("ClassicOption");
            classic.IsChecked = true;
            check("经典菜单设置禁用重排隐藏及内置增强选项", !((DataGrid)window.FindName("BuiltinGrid")).IsEnabled && !((DataGrid)window.FindName("ShellGrid")).IsEnabled && !((CheckBox)window.FindName("ShowMoreOption")).IsEnabled, null);
            classic.IsChecked = false;
            check("增强模式须手动切换且保留旧设置编辑能力", ((DataGrid)window.FindName("BuiltinGrid")).IsEnabled && ((CheckBox)window.FindName("ShowMoreOption")).IsEnabled, null);
            var hotkey = (TextBox)window.FindName("HotkeyText");
            var save = (Button)window.FindName("SaveButton");
            var validation = (TextBlock)window.FindName("HotkeyValidation");
            hotkey.Text = "Ctrl++X";
            check("快捷键编辑立即显示错误并禁用保存", !save.IsEnabled && validation.Text.Contains("不能为空", StringComparison.Ordinal), null);
            hotkey.Text = "Ctrl+Alt+F20";
            check("有效快捷键立即恢复保存并提供注册时机提示", save.IsEnabled && validation.Text.Contains("保存时", StringComparison.Ordinal), null);
            var more = (ComboBox)window.FindName("MoreExpansionStyle");
            var group = (ComboBox)window.FindName("GroupExpansionStyle");
            check("更多与分组动效分别提供独立配置", more.Items.Count == 4 && group.Items.Count == 4 && ((Slider)window.FindName("MoreExpansionDuration")).Value == app.Config.MoreExpansionMs, null);
            ((CheckBox)window.FindName("ReplaceMenu")).IsChecked = !app.Config.ReplaceExplorerMenu;
            ((CheckBox)window.FindName("CompactOption")).IsChecked = !app.Config.CompactMenu;
            var restore = (Button)window.FindName("RestoreButton");
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var smallBounds = SettingsWindowSizing.Fit(960, 520, 1040, 800, 920, 640);
            window.MinWidth = smallBounds.MinWidth; window.MinHeight = smallBounds.MinHeight;
            window.Width = smallBounds.Width; window.Height = smallBounds.Height;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var footerOrigin = save.TranslatePoint(new Point(0, 0), window);
            check("较低工作区下设置窗口缩小且底部保存按钮完整可达", window.ActualHeight <= 496.01 && footerOrigin.Y >= 0 && footerOrigin.Y + save.ActualHeight <= window.ActualHeight && footerOrigin.X + save.ActualWidth <= window.ActualWidth, null);
            // Exercise a startup/reload failure through the same App store used by
            // the settings page, within the isolated self-test profile only.
            Directory.CreateDirectory(Path.GetDirectoryName(app.ConfigPath)!);
            string recoveryText = BetterCM.Config.ConfigJson.Serialize(app.Config);
            File.WriteAllText(app.ConfigPath, recoveryText); app.ReloadConfiguration();
            try
            {
                File.WriteAllText(app.ConfigPath, "{ invalid");
                bool failedLoad = !app.ReloadConfiguration(); window.NotifyConfigurationChanged();
                hotkey.Text = "Ctrl+Alt+F20";
                check("配置读取失败时显示恢复入口且有效快捷键不能解除保存保护", failedLoad && !save.IsEnabled && ((Border)window.FindName("ConfigurationNotice")).Visibility == Visibility.Visible, null);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check("配置读取失败后直接触发保存事件仍保护原文件", File.ReadAllText(app.ConfigPath) == "{ invalid", null);
                File.WriteAllText(app.ConfigPath, recoveryText); app.ReloadConfiguration(); window.RefreshSettings();
                check("修复配置并重新读取后恢复正常保存能力", save.IsEnabled && ((Border)window.FindName("ConfigurationNotice")).Visibility == Visibility.Collapsed, null);
            }
            finally
            {
                File.WriteAllText(app.ConfigPath, recoveryText); app.ReloadConfiguration();
                if (originalConfig == null) File.Delete(app.ConfigPath); else File.WriteAllText(app.ConfigPath, originalConfig);
            }
            check("还原已保存设置取消热键和其他未保存编辑", hotkey.Text == app.Config.GlobalHotkey && ((CheckBox)window.FindName("ReplaceMenu")).IsChecked == app.Config.ReplaceExplorerMenu && save.IsEnabled, null);
            check("还原设置不写入用户配置", originalConfig == (File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null), null);
            check("小尺寸模式具有独立设置且还原恢复原值", ((CheckBox)window.FindName("CompactOption")).IsChecked == app.Config.CompactMenu, null);
            check("侧边悬停延时可独立设置且关闭更多折叠时仍可编辑", ((Slider)window.FindName("SubmenuDelay")).IsEnabled && ((Slider)window.FindName("SubmenuDelay")).Value == app.Config.SubmenuHoverDelayMs, null);
            hotkey.Text = "Ctrl+Alt+F20";
            string oldTheme = app.Config.Theme;
            try
            {
                app.Config.Theme = oldTheme == "dark" ? "light" : "dark";
                window.NotifyConfigurationChanged();
                check("外部配置更新保留编辑并提示加载最新已保存值", hotkey.Text == "Ctrl+Alt+F20" && ((TextBlock)window.FindName("SaveStatus")).Text.Contains("外部更新", StringComparison.Ordinal), null);
                check("外部配置更新阻止旧编辑覆盖新值", !save.IsEnabled, null);
                hotkey.Text = "Alt+F19";
                check("外部冲突下重新编辑有效快捷键不能绕过保存保护", !save.IsEnabled, null);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check("保存事件再次核对当前配置且不落盘", originalConfig == (File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null), null);
                restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check("还原最新已保存配置解除冲突并恢复保存", save.IsEnabled && hotkey.Text == app.Config.GlobalHotkey && ((ComboBox)window.FindName("ThemeChoice")).SelectedIndex == (app.Config.Theme == "dark" ? 1 : app.Config.Theme == "light" ? 2 : 0), null);
                app.Config.Theme = oldTheme;
                window.NotifyConfigurationChanged();
                restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check("仅外观外部变化且快捷键未变时撤销仍恢复保存能力", save.IsEnabled && hotkey.Text == app.Config.GlobalHotkey, null);
            }
            finally { app.Config.Theme = oldTheme; }
            var tabs = (TabControl)window.FindName("SettingsTabs");
            tabs.SelectedIndex = 3;
            classic.IsChecked = true;
            window.UpdateLayout();
            check("经典模式在自定义动作页解释不可用原因并提供切换入口", ((Border)window.FindName("CustomModeNotice")).IsVisible && !((StackPanel)window.FindName("CustomCommands")).IsEnabled, null);
            var switchMode = FindButtons(window).First(x => x.Content as string == "使用增强模式");
            switchMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("页内切换增强模式只更改未保存选项且不写入配置", classic.IsChecked == false && ((StackPanel)window.FindName("CustomCommands")).IsEnabled && originalConfig == (File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null), null);
            var custom = (DataGrid)window.FindName("CustomGrid");
            var customRows = (System.Collections.ObjectModel.ObservableCollection<BetterCM.Config.CustomActionItem>)custom.ItemsSource;
            var previousRows = customRows.ToArray();
            try
            {
                customRows.Clear(); window.UpdateLayout();
                check("无自定义动作时显示引导且禁用无选项的编辑移除", ((StackPanel)window.FindName("CustomEmptyState")).IsVisible && !((Button)window.FindName("EditCustomButton")).IsEnabled && !((Button)window.FindName("RemoveCustomButton")).IsEnabled, null);
                var item = new BetterCM.Config.CustomActionItem { Title = "界面检查动作", Executable = "notepad.exe" };
                customRows.Add(item); custom.SelectedItem = item; window.UpdateLayout();
                check("添加并选中动作后显示列表且编辑移除可用", !((StackPanel)window.FindName("CustomEmptyState")).IsVisible && custom.IsVisible && ((Button)window.FindName("EditCustomButton")).IsEnabled && ((Button)window.FindName("RemoveCustomButton")).IsEnabled, null);
            }
            finally { customRows.Clear(); foreach (var row in previousRows) customRows.Add(row); }
            tabs.SelectedIndex = 4;
            var first = new ShellItem(7, "第一个项目", "分组", "ui-check:first", "open", true);
            var second = new ShellItem(19, "第二个项目", "", "ui-check:second", "properties", true);
            window.RefreshShellItems([first], "第一个目标");
            var shell = (DataGrid)window.FindName("ShellGrid");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var pinBox = FindVisual<CheckBox>(shell).First(x => System.Windows.Automation.AutomationProperties.GetName(x).StartsWith("直接显示", StringComparison.Ordinal));
            var pinHeader = FindVisual<System.Windows.Controls.Primitives.DataGridColumnHeader>(shell).First(x => x.Column == shell.Columns[1]);
            double boxCenter = pinBox.TranslatePoint(new Point(pinBox.ActualWidth / 2, 0), shell).X;
            double headerCenter = pinHeader.TranslatePoint(new Point(pinHeader.ActualWidth / 2, 0), shell).X;
            check("系统项目勾选框与对应列标题水平居中对齐", Math.Abs(boxCenter - headerCenter) < 1.5, $"checkbox={boxCenter:F2}, header={headerCenter:F2}");
            ((ShellRow)shell.Items[0]).Pinned = true;
            window.RefreshShellItems([second], "第二个目标");
            ((ShellRow)shell.Items[0]).Hidden = true;
            window.RefreshShellItems([first], "第一个目标");
            var pending = window.ReadSettings();
            check("切换系统项目列表保留不同目标的未保存勾选且不写入配置", ((ShellRow)shell.Items[0]).Pinned && pending.PinnedShellActions.Contains(first.Key) && pending.HiddenShellActions.Contains(second.Key) && originalConfig == (File.Exists(app.ConfigPath) ? File.ReadAllText(app.ConfigPath) : null), null);
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("撤销同时清理已离开目标的临时系统项目修改", !window.ReadSettings().PinnedShellActions.Contains(first.Key) && !window.ReadSettings().HiddenShellActions.Contains(second.Key), null);
            ((CheckBox)window.FindName("Animations")).IsChecked = false;
            check("关闭菜单动画同时禁用展开动效选项", !((ComboBox)window.FindName("MoreExpansionStyle")).IsEnabled && !((ComboBox)window.FindName("GroupExpansionStyle")).IsEnabled, null);
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        finally { window.Close(); }
    }
    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisual<T>(child)) yield return nested;
        }
    }
    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Button button) yield return button;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }
}
