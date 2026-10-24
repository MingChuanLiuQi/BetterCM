using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class WorkspaceBackupPanel : UserControl
{
    private readonly Func<AppConfig> _read;
    private readonly Func<string> _baseline;
    private readonly Func<IEnumerable<string>> _protectedPaths;
    private readonly Action<AppConfig, string> _apply;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
    private readonly Button _export = new() { Content = "导出完整备份" }, _import = new() { Content = "恢复或选择性导入", Margin = new Thickness(10, 0, 0, 0) };
    internal WorkspaceBackupPanel(Func<AppConfig> read, Func<string> baseline, Action<AppConfig, string> apply, Func<IEnumerable<string>>? protectedPaths = null)
    {
        _read = read; _baseline = baseline; _apply = apply; _protectedPaths = protectedPaths ?? (() => []);
        var panel = new StackPanel { Margin = new Thickness(4, 12, 18, 0) };
        panel.Children.Add(new TextBlock { Text = "备份与迁移", FontSize = 23, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new TextBlock { Text = "一个文件保存动作及运行参数、规则与分组、菜单方案、收藏目录、重命名预设，以及外观、快捷键和菜单行为。导出包含当前未保存的编辑。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "恢复时先查看差异，选择整体恢复或按类别合并。相同 ID 可跳过、替换或保留两份；导入的方案会自动关联保留的新动作。换电脑时可将旧根目录批量映射到新目录。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "备份包含真实程序路径和参数，分享前请检查。日志、操作历史和登录自启项不包含在内。恢复只加入设置草稿，保存后才生效。最多 4 MiB。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        var buttons = new WrapPanel(); buttons.Children.Add(_export); buttons.Children.Add(_import); panel.Children.Add(buttons); panel.Children.Add(_status); Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _export.Click += async (_, _) => await ExportAsync(); _import.Click += async (_, _) => await ImportAsync();
    }
    private void Busy(bool value) => _export.IsEnabled = _import.IsEnabled = !value;
    private async Task ExportAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "导出完整工作区备份", FileName = "BetterCM-Workspace.json", Filter = "BetterCM 工作区备份 (*.json)|*.json", OverwritePrompt = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Busy(true);
        try { var snapshot = _read().Clone(); await Task.Run(() => WorkspaceTransfer.WriteAsync(dialog.FileName, snapshot, _protectedPaths())); _status.Text = "备份已导出，包含当前未保存编辑、路径与启动参数。"; }
        catch (Exception ex) { _status.Text = "导出失败：" + ex.Message; }
        finally { Busy(false); }
    }
    private async Task ImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择工作区备份", Filter = "BetterCM 工作区备份 (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Busy(true);
        try
        {
            var incoming = await Task.Run(() => WorkspaceTransfer.ReadAsync(dialog.FileName));
            var owner = Window.GetWindow(this); if (owner?.IsVisible != true || (Application.Current as App)?.IsExiting == true) return;
            string baseline = _baseline(); var current = _read().Clone();
            var review = new WorkspaceImportWindow(current, incoming) { Owner = owner };
            if (review.ShowDialog() == true && review.Result != null) { _apply(review.Result.Configuration, baseline); _status.Text = "已加入设置草稿，请检查后保存；可用底部撤销按钮还原。"; }
        }
        catch (Exception ex) { _status.Text = "导入未应用：" + ex.Message; }
        finally { Busy(false); }
    }
}
