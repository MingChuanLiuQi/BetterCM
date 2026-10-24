using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class RenameHistoryPanel : UserControl
{
    private readonly App _app;
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly CheckBox _keep = new() { Content = "保留以后完成的批量重命名历史（立即保存此选项）" };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly List<Button> _buttons = [];
    private bool _loading, _refreshing;
    internal RenameHistoryPanel(App app)
    {
        _app = app;
        var root = new DockPanel { Margin = new Thickness(4, 12, 12, 0) };
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "重命名历史", FontSize = 23, Margin = new Thickness(0, 0, 0, 12) });
        top.Children.Add(new TextBlock { Text = "保留最近 20 次已完成任务，可在关闭窗口或重启后校验并撤销。记录仅在本机，包含完整路径、文件标识和 SHA256，不包含文件内容，也不加入工作区备份或诊断导出。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        top.Children.Add(_keep);
        top.Children.Add(new TextBlock { Text = "撤销前检查原名称占用、文件标识、时间和内容。文件夹只核对自身信息。处理中强制退出的任务不能自动恢复；关闭历史开关不会删除已有记录。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 12) });
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var buttons = new WrapPanel(); footer.Children.Add(buttons); footer.Children.Add(_status);
        void Button(string title, Func<Task> action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 10, 8, 0) }; _buttons.Add(button); buttons.Children.Add(button);
            button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { _status.Text = ex.Message; } };
        }
        Button("刷新历史", RefreshAsync);
        Button("查看与撤销", () => { if (_grid.SelectedItem is RenameHistoryEntry entry) _app.ShowRenameHistory(entry); else _status.Text = "请选择一条历史。"; return Task.CompletedTask; });
        Button("移除选中记录", async () => { if (_app.HasRenameWindow) throw new IOException("请先关闭批量重命名窗口。"); if (_grid.SelectedItem is RenameHistoryEntry entry) { await Task.Run(() => new RenameHistoryStore().Remove(entry.Id)); await RefreshAsync(); } });
        Button("清理已撤销记录", async () => { await Task.Run(() => new RenameHistoryStore().ClearUndone()); await RefreshAsync(); });
        foreach (var column in new[] { ("完成时间", nameof(RenameHistoryEntry.Time), 170.0), ("项目数", nameof(RenameHistoryEntry.Count), 70.0), ("状态", nameof(RenameHistoryEntry.State), 160.0), ("名称摘要", nameof(RenameHistoryEntry.Summary), 360.0) })
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = column.Item3 });
        root.Children.Add(_grid); Content = root;
        _keep.Click += (_, _) =>
        {
            if (_loading) return;
            var next = _app.Config.Clone(); next.KeepRenameHistory = _keep.IsChecked == true;
            string? error = _app.SaveSettings(next, StartupService.Enabled); _status.Text = error ?? "历史保留选项已保存，其他设置草稿仍然保留。";
            _keep.IsChecked = _app.Config.KeepRenameHistory;
        };
        Loaded += async (_, _) => await RefreshAsync();
    }
    internal async Task RefreshAsync()
    {
        if (_refreshing) return; _refreshing = true;
        _loading = true; _keep.IsChecked = _app.Config.KeepRenameHistory; _loading = false;
        foreach (var button in _buttons) button.IsEnabled = false;
        try { var entries = await Task.Run(() => new RenameHistoryStore().Load()); _grid.ItemsSource = entries; _status.Text = $"本机共 {entries.Count} 条记录；移除记录不会更改文件。"; }
        catch (Exception ex) { _grid.ItemsSource = null; _status.Text = "历史读取失败，原文件保留：" + ex.Message; }
        finally { _refreshing = false; foreach (var button in _buttons) button.IsEnabled = true; }
    }
}

internal sealed class RenameHistoryWindow : Window
{
    private readonly RenameHistoryEntry _entry;
    private readonly DataGrid _grid = new();
    private readonly TextBlock _heading = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 10) };
    private readonly Button _undo = new() { Content = "校验并撤销" }, _cancel = new() { Content = "取消校验", IsEnabled = false, Margin = new Thickness(10, 0, 0, 0) };
    private readonly ProgressBar _progress = new() { Maximum = 1, Height = 5 };
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _idle;
    private bool _busy, _failed;
    internal RenameHistoryWindow(RenameHistoryEntry entry)
    {
        _entry = entry; Title = "重命名历史 · " + entry.Time; Width = 960; Height = 600; MinWidth = 640; MinHeight = 400;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this);
        var root = new DockPanel { Margin = new Thickness(24) };
        _heading.Text = $"{entry.Time} · {entry.Count} 项 · {entry.State}"; _heading.FontSize = 21; _heading.Margin = new Thickness(0, 0, 0, 16); DockPanel.SetDock(_heading, Dock.Top); root.Children.Add(_heading);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(_status); footer.Children.Add(_progress);
        var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(_undo); buttons.Children.Add(_cancel); footer.Children.Add(buttons);
        _grid.AutoGenerateColumns = false; _grid.IsReadOnly = true; _grid.CanUserAddRows = false; _grid.CanUserSortColumns = false; _grid.ItemsSource = entry.Changes;
        foreach (var column in new[] { ("原名称", nameof(RenameHistoryChange.OriginalName), nameof(RenameHistoryChange.Source)), ("完成后名称", nameof(RenameHistoryChange.NewName), nameof(RenameHistoryChange.Target)), ("记录的当前名称", nameof(RenameHistoryChange.CurrentName), nameof(RenameHistoryChange.Current)), ("所在目录", nameof(RenameHistoryChange.ParentDirectory), nameof(RenameHistoryChange.ParentDirectory)) })
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(ToolTipProperty, new Binding(column.Item3))); style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), ElementStyle = style, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        }
        root.Children.Add(_grid); Content = root; _undo.IsEnabled = entry.Status == "completed";
        _status.Text = entry.Status == "completed" ? "撤销会恢复表格中的原路径，开始后先校验所有项目。任何冲突都会阻止撤销。" : "该记录不能再次自动撤销。请根据记录的路径检查文件；中断记录中的位置可能不是最终状态。";
        _undo.Click += async (_, _) => await UndoAsync(); _cancel.Click += (_, _) => _cancellation?.Cancel();
        Closing += (_, e) => { if (_busy) { _cancellation?.Cancel(); e.Cancel = true; _status.Text = "正在取消校验或等待文件恢复完成，请稍后关闭。"; } };
    }
    internal async Task UndoAsync()
    {
        if (_busy || !_undo.IsEnabled) return;
        if ((Application.Current as App)?.HasRenameWindow == true) { _status.Text = "请先关闭批量重命名窗口，再从历史撤销。"; return; }
        _busy = true; _failed = false; _undo.IsEnabled = false; _cancel.IsEnabled = true; _cancellation?.Dispose(); _cancellation = new(); _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Progress<(double Value, string Text)>(x => { if (_busy) { _progress.Value = x.Value; _status.Text = x.Text; } });
        try { string? warning = await Task.Run(() => new RenameHistoryStore().Undo(_entry.Id, _cancellation.Token, progress)); _entry.Status = "undone"; _entry.Changes = _entry.Changes.Select(x => x with { Current = x.Source }).ToList(); _grid.ItemsSource = _entry.Changes; _heading.Text = $"{_entry.Time} · {_entry.Count} 项 · {_entry.State}"; _status.Text = warning ?? "已恢复原名称。此记录已标记为撤销。"; }
        catch (OperationCanceledException) { _status.Text = "校验已取消，保留当前名称。关闭后刷新历史可重试。"; }
        catch (Exception ex) { _failed = true; _status.Text = ex.Message + "；请关闭后刷新历史查看最新状态。"; }
        finally { _busy = false; _cancel.IsEnabled = false; _idle.TrySetResult(); }
    }
    internal async Task<bool> StopAsync() { if (!_busy) return true; _cancellation?.Cancel(); await _idle!.Task; return !_failed; }
}
