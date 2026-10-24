using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

internal static class Iteration220Checks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check, string fixture)
    {
        var app = (App)Application.Current;
        if (!app.ReloadConfiguration()) throw new IOException(app.ConfigurationError);
        var original = app.Config.Clone();
        string root = Path.Combine(fixture, "iteration220-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        SelectedItem Item(string name) { string path = Path.Combine(root, name); File.WriteAllText(path, "220 payload"); return new(path, false); }
        var items = new[] { Item("报告1.txt"), Item("报告2.txt"), Item("报告3.txt") };
        var rename = new BatchRenameWindow(new() { Items = items.ToList(), CurrentDirectory = root }) { ShowActivated = false };
        RenameHistoryWindow? history = null; Window? host = null;
        void Check(string id, string title, bool passed) => check(new("Iteration220/" + id, title), passed, null);
        void Save(AppConfig value) { string? error = app.SaveSettings(value, StartupService.Enabled); if (error != null) throw new IOException(error); }
        try
        {
            var enabled = original.Clone(); enabled.KeepRenameHistory = true; Save(enabled);
            rename.Show(); rename.ApplyOptions(new() { Numbered = true, Digits = 2, Suffix = "-", SortOrder = "name" }); await rename.PreviewAsync();
            var grid = Descendants<DataGrid>(rename).Single(); var rows = grid.Items.Cast<RenamePreviewRow>().ToArray();
            rows[1].Included = false;
            Check("SelectionInvalidates", "取消勾选立即禁用执行并保留文件", !Descendants<Button>(rename).Single(x => (string?)x.Content == "执行重命名").IsEnabled && items.All(x => File.Exists(x.Path)));
            await rename.PreviewAsync(); rows = grid.Items.Cast<RenamePreviewRow>().ToArray();
            Check("SelectionRenumbers", "按勾选项重新编号且排除项仍可重新勾选", rows.Count(x => x.Included) == 2 && rows[1].Plan.NewName == "报告3-02.txt" && rows.Single(x => !x.Included).Plan.Source == items[1].Path && !grid.CanUserSortColumns);
            await rename.Dispatcher.InvokeAsync(rename.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle); CustomActionChecks.Capture(rename, Path.Combine(root, "selective-preview.png"));
            await rename.ExecuteAsync();
            var entry = (await Task.Run(() => new RenameHistoryStore().Load())).Single(x => x.Changes.Any(c => c.Source == items[0].Path));
            Check("HistorySaved", "完成的勾选任务保存到历史且排除文件保持原名", entry.Count == 2 && entry.Status == "completed" && File.Exists(items[1].Path) && rows.Where(x => x.Included).All(x => File.Exists(x.Plan.Target)));
            rename.Close(); history = new(entry) { ShowActivated = false }; history.Show(); await history.Dispatcher.InvokeAsync(history.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CustomActionChecks.Capture(history, Path.Combine(root, "history-details.png")); await history.UndoAsync();
            Check("HistoryReopenUndo", "关闭原窗口后从历史详情校验并恢复原名", items.All(x => File.Exists(x.Path)) && (await Task.Run(() => new RenameHistoryStore().Load())).Single(x => x.Id == entry.Id).Status == "undone");
            history.Close(); history = null;
            var disabled = app.Config.Clone(); disabled.KeepRenameHistory = false; Save(disabled); int before = (await Task.Run(() => new RenameHistoryStore().Load())).Count;
            rename = new(new() { Items = items.ToList(), CurrentDirectory = root }) { ShowActivated = false }; rename.Show(); rename.ApplyOptions(new() { Prefix = "不保留-" }); await rename.PreviewAsync(); await rename.ExecuteAsync();
            Check("HistoryOptOut", "关闭历史保留后仍可执行但不新增本机记录", (await Task.Run(() => new RenameHistoryStore().Load())).Count == before && File.Exists(Path.Combine(root, "不保留-报告1.txt")));
            await rename.UndoAsync();
            rename.ApplyOptions(new() { RemoveEnd = 1 }); await rename.PreviewAsync(); rename.ExcludeConflicts(); await rename.PreviewAsync();
            Check("ExcludeAllConflicts", "排除全部冲突后空选区拒绝执行且可恢复全选", !Descendants<Button>(rename).Single(x => (string?)x.Content == "执行重命名").IsEnabled && items.All(x => File.Exists(x.Path)));
            rename.SelectAll(); rename.ApplyOptions(new() { Prefix = "safe-" }); await rename.PreviewAsync();
            Check("SelectAll", "全选后重新预览恢复所有项目的有效计划", Descendants<DataGrid>(rename).Single().Items.Cast<RenamePreviewRow>().Count(x => x.Included && x.Error.Length == 0) == 3);
            var title = Descendants<TextBox>(rename).Single(x => AutomationProperties.GetName(x) == "预设名称"); title.Text = "220 保存选择";
            Descendants<Button>(rename).Single(x => (string?)x.Content == "保存预设").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var choice = Descendants<ComboBox>(rename).Single(x => AutomationProperties.GetName(x) == "重命名预设"); var saved = (RenamePreset)((ComboBoxItem)choice.SelectedItem).Tag;
            Check("PresetSelection", "保存预设后保持选中刚保存的用户预设", saved.Title == title.Text && app.Config.RenamePresets.Any(x => x.Id == saved.Id));
            rename.ApplyOptions(new() { Suffix = "updated" }); Descendants<Button>(rename).Single(x => (string?)x.Content == "保存预设").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("PresetUpdate", "同名预设更新规则时保持稳定标识与选择", ((RenamePreset)((ComboBoxItem)choice.SelectedItem).Tag).Id == saved.Id && app.Config.RenamePresets.Single(x => x.Id == saved.Id).Options.Suffix == "updated");
            var editor = new MenuWorkspaceEditor(); var config = new AppConfig { AutoSelectProfile = true, ActiveProfileId = "p1", MenuProfiles = [new() { Id = "p1", Title = "第一", DirectoryRoot = root }, new() { Id = "p2", Title = "第二", DirectoryRoot = root }], FavoriteDestinations = [new() { Id = "f1", Title = "第一", Path = root }, new() { Id = "f2", Title = "第二", Path = root }] };
            editor.Load(config, () => [], new() { Items = items.ToList(), CurrentDirectory = root });
            host = new Window { Content = editor, Width = 900, Height = 700, ShowActivated = false, Style = (Style)Application.Current.FindResource(typeof(Window)) }; host.Show(); host.UpdateLayout();
            var grids = Descendants<DataGrid>(editor).ToArray(); var profiles = grids.Single(x => x.Items[0] is MenuProfile); profiles.SelectedIndex = 0;
            Descendants<Button>(editor).Single(x => (string?)x.Content == "下移方案").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var applied = new AppConfig(); editor.Apply(applied);
            Check("ProfileOrdering", "方案下移影响同目录自动优先级并保留手动方案", applied.MenuProfiles[0].Id == "p2" && applied.ActiveProfileId == "p1" && ActionRules.SelectProfile(applied, new() { Items = items.ToList(), CurrentDirectory = root })?.Id == "p2" && !profiles.CanUserSortColumns);
            var favorites = grids.Single(x => x.Items[0] is FavoriteDestination); favorites.SelectedIndex = 0; Descendants<Button>(editor).Single(x => (string?)x.Content == "下移").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); editor.Apply(applied);
            Check("FavoriteOrdering", "收藏目录下移保存实际顺序且禁止列排序产生误导", applied.FavoriteDestinations[0].Id == "f2" && !favorites.CanUserSortColumns);
            host.Close(); host = null;
            var panel = new RenameHistoryPanel(app); host = new Window { Content = panel, Width = 960, Height = 650, ShowActivated = false, Style = (Style)Application.Current.FindResource(typeof(Window)) }; host.Show(); await panel.RefreshAsync(); await host.Dispatcher.InvokeAsync(host.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CustomActionChecks.Capture(host, Path.Combine(root, "history-list.png"));
            Check("HistoryPanel", "历史设置页显示本机记录和隐私选项", Descendants<CheckBox>(panel).Single().IsChecked == false && Descendants<Button>(panel).Any(x => (string?)x.Content == "查看与撤销"));
        }
        finally { history?.Close(); rename.Close(); host?.Close(); Save(original); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
