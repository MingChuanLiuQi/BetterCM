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
            var restore = FindButtons(window).Single(x => x.Content as string == "还原为已保存");
            restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
            }
            finally { app.Config.Theme = oldTheme; }
        }
        finally { window.Close(); }
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
