using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class WorkspaceChecks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var app = (App)Application.Current;
        string baseline = ConfigJson.Serialize(app.Config);
        string root = Path.Combine(fixture, "workspace-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string source = Path.Combine(root, "报告 {dir} A.txt"); await File.WriteAllTextAsync(source, "workspace payload");
        var context = new SelectedContext { Items = [new(source, false)], CurrentDirectory = root };
        var rename = new BatchRenameWindow(context) { ShowActivated = false };
        var menu = new ContextMenuWindow { ShowActivated = false };
        var settings = new MainWindow(app) { ShowActivated = false }; settings.Closing += (_, e) => e.Cancel = false;
        try
        {
            rename.Show(); await rename.Dispatcher.InvokeAsync(rename.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var prefix = Descendants<TextBox>(rename).Single(x => AutomationProperties.GetName(x) == "前缀"); prefix.Text = "归档-";
            await rename.PreviewAsync();
            var rows = Descendants<DataGrid>(rename).Single().Items.Cast<RenameRow>().ToArray();
            check(new("Workspace/RenamePreview", "批量重命名窗口预览中文和花括号且不改变原文件"), rows.Length == 1 && rows[0].NewName == "归档-报告 {dir} A.txt" && rows[0].Error.Length == 0 && File.Exists(source), null);
            await rename.Dispatcher.InvokeAsync(rename.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CustomActionChecks.Capture(rename, Path.Combine(root, "rename-preview.png"));
            await rename.ExecuteAsync();
            check(new("Workspace/RenameExecute", "批量重命名窗口实际执行并保留文件内容"), !File.Exists(source) && File.ReadAllText(rows[0].Target) == "workspace payload", null);
            await rename.UndoAsync();
            check(new("Workspace/RenameUndo", "批量重命名窗口可撤销本次操作"), File.Exists(source) && !File.Exists(rows[0].Target), null);
            var pendingPreview = rename.PreviewAsync();
            bool stopped = await rename.StopAsync(); await pendingPreview;
            check(new("Workspace/RenameStop", "退出前可取消并等待重命名窗口处理完成且保留原文件"), stopped && File.Exists(source) && !File.Exists(rows[0].Target), null);
            var profile = new MenuProfile { Title = "资料整理", DirectoryRoot = root, ActionIds = ["batch-rename", "favorite:copy", "favorite:move"] };
            string copyRoot = Path.Combine(root, "复制目标"), moveRoot = Path.Combine(root, "移动目标"); Directory.CreateDirectory(copyRoot); Directory.CreateDirectory(moveRoot);
            var favorite = new FavoriteDestination { Title = "资料库", Path = copyRoot, DateSubfolder = true };
            var draft = app.Config.Clone(); draft.MenuProfiles = [profile]; draft.ActiveProfileId = profile.Id; draft.AutoSelectProfile = true; draft.FavoriteDestinations = [favorite];
            var editor = (MenuWorkspaceEditor)((ContentControl)settings.FindName("WorkspaceHost")).Content;
            editor.Load(draft, () => [("batch-rename", "批量重命名")], context);
            var read = settings.ReadSettings();
            check(new("Workspace/SettingsDraft", "菜单方案和收藏目录进入草稿且不改动已保存配置"), read.ActiveProfileId == profile.Id && read.AutoSelectProfile && read.FavoriteDestinations.Single().Path == copyRoot && baseline == ConfigJson.Serialize(app.Config), null);
            settings.Show(); var tabs = Descendants<TabControl>(settings).First(); tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(x => (string)x.Header == "场景与整理");
            await settings.Dispatcher.InvokeAsync(settings.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CustomActionChecks.Capture(settings, Path.Combine(root, "workspace-settings.png"));
            var restored = new AppConfig(); editor.Apply(restored); restored.FavoriteDestinations[0].Title = "changed";
            check(new("Workspace/SettingsIsolation", "菜单方案和收藏目录读取为独立副本"), settings.ReadSettings().FavoriteDestinations[0].Title == favorite.Title && draft.FavoriteDestinations[0].Title == favorite.Title, null);
            settings.RefreshSettings();
            var afterRestore = settings.ReadSettings();
            check(new("Workspace/SettingsRestore", "撤销设置还原菜单方案和收藏目录"), afterRestore.ActiveProfileId == app.Config.ActiveProfileId && afterRestore.AutoSelectProfile == app.Config.AutoSelectProfile &&
                System.Text.Json.JsonSerializer.Serialize(afterRestore.MenuProfiles) == System.Text.Json.JsonSerializer.Serialize(app.Config.MenuProfiles) &&
                System.Text.Json.JsonSerializer.Serialize(afterRestore.FavoriteDestinations) == System.Text.Json.JsonSerializer.Serialize(app.Config.FavoriteDestinations), null);
            var bounds = SettingsWindowSizing.Fit(800, 520, rename.Width, rename.Height, rename.MinWidth, rename.MinHeight);
            rename.MinWidth = bounds.MinWidth; rename.MinHeight = bounds.MinHeight; rename.Width = bounds.Width; rename.Height = bounds.Height;
            await rename.Dispatcher.InvokeAsync(rename.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var button = Descendants<Button>(rename).Single(x => (string?)x.Content == "生成预览"); var position = button.TranslatePoint(new Point(), rename);
            check(new("Workspace/RenameSmallWorkArea", "批量重命名窗口在小工作区保留可达的操作按钮"), position.Y >= 0 && position.Y + button.ActualHeight <= rename.ActualHeight, null);
            var reason = new ActionRuleEditor(new() { DirectoryRoot = root, Target = "files" }, "资料", context);
            check(new("Workspace/RuleEditor", "场景规则编辑器读取类型目录和分组并匹配最近选区"), reason.Group == "资料" && ActionRules.Matches(reason.ReadRule(), context), null);
            List<MenuAction> grouped = [new() { Id = "group-test-a", Title = "整理文件", Group = "资料工具" }, new() { Id = "group-test-b", Title = "检查文件", Group = "资料工具" }];
            menu.ShowAt(30, 40, context, new() { ClassicMenu = false, EnableAnimations = false, ShowMoreItems = false }, [], grouped, [], false);
            await menu.Dispatcher.InvokeAsync(menu.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var group = menu.VisibleRows.Single(); menu.OpenGroup(group);
            check(new("Workspace/GroupedSubmenu", "一级自定义分组实际打开侧边子菜单且保留两个动作"), group.IsGroup && menu.OpenSubmenu?.VisibleRows.Count == 2 && ReferenceEquals(menu.OpenSubmenu.VisibleRows[0].Action, grouped[0]), null);
            menu.CloseMenu("工作区检查结束", true);
            string sourceFolder = Path.Combine(root, "项目 目录"); Directory.CreateDirectory(sourceFolder); await File.WriteAllTextAsync(Path.Combine(sourceFolder, "nested.txt"), "nested payload");
            string copyResult = await FavoriteTransfer.TransferAsync(context with { Items = [new(source, false), new(sourceFolder, true)] }, favorite, move: false);
            string copied = Path.Combine(FavoriteTransfer.Destination(favorite, DateTime.Today), Path.GetFileName(source));
            check(new("Workspace/NativeCopy", "独立 Shell 宿主通过 Windows 文件操作复制到日期目录"), File.Exists(source) && File.ReadAllText(copied) == "workspace payload" &&
                File.ReadAllText(Path.Combine(FavoriteTransfer.Destination(favorite, DateTime.Today), "项目 目录", "nested.txt")) == "nested payload" && Directory.Exists(sourceFolder) && copyResult == "Windows 文件操作已完成", null);
            var moveFavorite = new FavoriteDestination { Title = "归档", Path = moveRoot };
            string moveResult = await FavoriteTransfer.TransferAsync(context with { Items = [new(copied, false)] }, moveFavorite, move: true);
            check(new("Workspace/NativeMove", "独立 Shell 宿主通过 Windows 文件操作移动文件"), !File.Exists(copied) && File.ReadAllText(Path.Combine(moveRoot, Path.GetFileName(source))) == "workspace payload" && File.Exists(source) && moveResult == "Windows 文件操作已完成", null);
        }
        finally { menu.Close(); settings.Close(); rename.Close(); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
