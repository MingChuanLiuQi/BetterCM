using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public sealed class BatchRenameWindow : Window
{
    private readonly SelectedItem[] _items;
    private readonly TextBox _prefix = new(), _suffix = new(), _find = new(), _replace = new(), _start = new() { Text = "1" }, _digits = new() { Text = "3" };
    private readonly CheckBox _number = new() { Content = "在名称末尾添加编号（按选区顺序）" };
    private readonly ComboBox _letterCase = new();
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _preview = new() { Content = "生成预览" }, _execute = new() { Content = "执行重命名", IsEnabled = false }, _undo = new() { Content = "撤销本次重命名", IsEnabled = false };
    private readonly Button _cancel = new() { Content = "取消处理", IsEnabled = false };
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _idle;
    private bool _lastFailure;
    private IReadOnlyList<RenameRow> _rows = [];
    private BatchRename? _transaction;
    private bool _busy;
    private int _revision;
    public BatchRenameWindow(SelectedContext context)
    {
        _items = context.Items.ToArray();
        Title = "批量重命名"; Width = 940; Height = 700; MinWidth = 680; MinHeight = 480;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) root.RowDefinitions.Add(new() { Height = height });
        root.Children.Add(new TextBlock { Text = $"批量重命名 · {_items.Length} 个项目", FontSize = 23, Margin = new Thickness(0, 0, 0, 14) });
        var fields = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        void Field(string title, Control control, double width = 165)
        {
            var field = new StackPanel { Width = width, Margin = new Thickness(0, 0, 12, 12) };
            field.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 0, 6) });
            System.Windows.Automation.AutomationProperties.SetName(control, title); field.Children.Add(control); fields.Children.Add(field);
        }
        Field("前缀", _prefix); Field("后缀", _suffix); Field("查找（区分大小写）", _find); Field("替换为", _replace);
        foreach (string value in new[] { "保持大小写", "主文件名转小写", "主文件名转大写" }) _letterCase.Items.Add(value);
        _letterCase.SelectedIndex = 0; Field("主文件名大小写", _letterCase); Field("编号起始值", _start, 100); Field("编号位数", _digits, 100);
        var options = new StackPanel(); options.Children.Add(fields); options.Children.Add(_number);
        options.Children.Add(new TextBlock { Text = "顺序：查找替换 → 大小写 → 前后缀 → 编号。文件扩展名保留；最多 1000 项。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12) });
        Grid.SetRow(options, 1); root.Children.Add(options);
        foreach (var column in new[] { ("原名称", nameof(RenameRow.OriginalName)), ("新名称", nameof(RenameRow.NewName)), ("冲突或错误", nameof(RenameRow.Error)) })
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new System.Windows.Data.Binding(column.Item2), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(_grid, 2); root.Children.Add(_grid);
        _status.Margin = new Thickness(0, 12, 0, 8); Grid.SetRow(_status, 3); root.Children.Add(_status);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in new[] { _preview, _execute, _undo, _cancel }) { button.Margin = new Thickness(8, 4, 0, 0); buttons.Children.Add(button); }
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        Grid.SetRow(buttons, 4); root.Children.Add(buttons); Content = root;
        _preview.Click += async (_, _) => await PreviewAsync(); _execute.Click += async (_, _) => await ExecuteAsync(); _undo.Click += async (_, _) => await UndoAsync();
        foreach (var box in new[] { _prefix, _suffix, _find, _replace, _start, _digits }) box.TextChanged += (_, _) => InvalidatePreview();
        _letterCase.SelectionChanged += (_, _) => InvalidatePreview(); _number.Checked += (_, _) => InvalidatePreview(); _number.Unchecked += (_, _) => InvalidatePreview();
        Closing += (_, e) => { if (_busy) { _cancellation?.Cancel(); e.Cancel = true; _status.Text = "正在取消并恢复文件，请等待完成后关闭。"; } };
        _status.Text = "填写规则后生成预览。执行后可在本窗口撤销一次；关闭窗口后不保留撤销记录。";
    }
    private void InvalidatePreview() { _revision++; _execute.IsEnabled = false; }
    private void Busy(bool value)
    {
        if (value) { _cancellation?.Dispose(); _cancellation = new(); _lastFailure = false; _idle = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        _busy = value; _cancel.IsEnabled = value; _preview.IsEnabled = !value && _transaction == null; _execute.IsEnabled = false; _undo.IsEnabled = !value && _transaction != null;
        if (!value) _idle?.TrySetResult();
    }
    internal async Task<bool> StopAsync()
    {
        if (!_busy) return true;
        _cancellation?.Cancel();
        await _idle!.Task;
        return !_lastFailure;
    }
    internal async Task PreviewAsync()
    {
        if (_busy || _transaction != null) return;
        try
        {
            if (!int.TryParse(_start.Text, out int start) || !int.TryParse(_digits.Text, out int digits)) throw new InvalidDataException("编号起始值和位数需要整数");
            var options = new RenameOptions { Prefix = _prefix.Text, Suffix = _suffix.Text, Find = _find.Text, Replace = _replace.Text, LetterCase = _letterCase.SelectedIndex switch { 1 => "lower", 2 => "upper", _ => "keep" }, Numbered = _number.IsChecked == true, Start = start, Digits = digits };
            int revision = _revision;
            Busy(true); _status.Text = "正在检查名称和目标冲突…";
            _rows = await Task.Run(() => BatchRename.Preview(_items, options, _cancellation!.Token)); _grid.ItemsSource = _rows;
            int errors = _rows.Count(x => x.Error.Length > 0), changed = _rows.Count(x => x.Changed);
            Busy(false); _execute.IsEnabled = revision == _revision && errors == 0 && changed > 0;
            _status.Text = $"预览：{changed} 项名称变化，{errors} 项冲突。执行时会再次检查目标名称，已有文件不会被覆盖。";
        }
        catch (OperationCanceledException) { Busy(false); _status.Text = "预览已取消，文件保持原名称。"; }
        catch (Exception ex) { _lastFailure = true; Busy(false); _status.Text = ex.Message; }
    }
    internal async Task ExecuteAsync()
    {
        if (_busy || !_execute.IsEnabled) return;
        Busy(true); _status.Text = "正在重命名…";
        try { _transaction = await Task.Run(() => BatchRename.Execute(_rows, _cancellation!.Token)); _status.Text = "重命名完成。本窗口可撤销一次；文件会核对内容，文件夹仅核对自身时间戳，原名称被占用时停止撤销。"; }
        catch (OperationCanceledException) { _status.Text = "重命名已取消，已恢复原名称。"; }
        catch (Exception ex) { _lastFailure = true; _status.Text = ex.Message; }
        finally { Busy(false); }
    }
    internal async Task UndoAsync()
    {
        if (_busy || _transaction == null) return;
        Busy(true);
        try { await Task.Run(() => _transaction.Undo(_cancellation!.Token)); _transaction = null; _grid.ItemsSource = null; _status.Text = "本次重命名已撤销。"; }
        catch (OperationCanceledException) { _status.Text = "撤销检查已取消，保留当前名称。"; }
        catch (Exception ex) { _lastFailure = true; _status.Text = ex.Message; }
        finally { Busy(false); }
    }
}
