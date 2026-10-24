using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using BetterCM.Models;
using BetterCM.Services;
using BetterCM.Config;
namespace BetterCM.Views;

internal sealed class RenamePreviewRow(RenameRow plan, bool included, Action<bool> changed) : INotifyPropertyChanged
{
    public RenameRow Plan { get; } = plan;
    private bool _included = included, _canSelect = true;
    public bool Included { get => _included; set { if (_included == value || !_canSelect) return; _included = value; PropertyChanged?.Invoke(this, new(nameof(Included))); changed(value); } }
    public bool CanSelect { get => _canSelect; set { _canSelect = value; PropertyChanged?.Invoke(this, new(nameof(CanSelect))); } }
    public string OriginalName => Plan.OriginalName;
    public string NewName => Plan.NewName;
    public string Error => Plan.Error;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class BatchRenameWindow : Window
{
    private readonly SelectedItem[] _items;
    private readonly HashSet<string> _chosenSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RenamePreviewRow> _displayRows = [];
    private readonly Button _selectAll = new() { Content = "全选" }, _excludeConflicts = new() { Content = "排除冲突项" };
    private string? _historyId;
    private static string Source(SelectedItem item) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path));
    private readonly TextBox _prefix = new(), _suffix = new(), _find = new(), _replace = new(), _start = new() { Text = "1" }, _digits = new() { Text = "3" };
    private readonly CheckBox _number = new() { Content = "在名称末尾添加编号（按选区顺序）" };
    private readonly ComboBox _letterCase = new();
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserSortColumns = false, CanUserAddRows = false };
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
    private int _operationGeneration;
    private readonly ComboBox _sort = new(), _presets = new();
    private readonly TextBox _removeStart = new() { Text = "0" }, _removeEnd = new() { Text = "0" }, _presetTitle = new();
    private readonly CheckBox _trim = new() { Content = "清理主文件名首尾空格" };
    private readonly ProgressBar _progress = new() { Maximum = 1, Height = 5, Margin = new Thickness(0, 6, 0, 6) };
    public BatchRenameWindow(SelectedContext context)
    {
        _items = context.Items.ToArray();
        foreach (var item in _items) _chosenSources.Add(Source(item));
        Title = "批量重命名"; Width = 940; Height = 700; MinWidth = 680; MinHeight = 480;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1.5, GridUnitType.Star), new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) root.RowDefinitions.Add(new() { Height = height });
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
        foreach (var item in new[] { ("捕获的选区顺序", "selection"), ("名称自然升序", "name"), ("名称自然降序", "name-desc"), ("修改时间从早到晚", "modified"), ("修改时间从晚到早", "modified-desc") }) _sort.Items.Add(new ComboBoxItem { Content = item.Item1, Tag = item.Item2 });
        _sort.SelectedIndex = 0; Field("排序后再编号", _sort, 200); Field("移除前几个字符", _removeStart, 120); Field("移除后几个字符", _removeEnd, 120);
        var options = new StackPanel();
        var presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) }; _presets.Width = 210; System.Windows.Automation.AutomationProperties.SetName(_presets, "重命名预设"); presetRow.Children.Add(_presets);
        var applyPreset = new Button { Content = "应用预设", Margin = new Thickness(8, 0, 8, 0) }; presetRow.Children.Add(applyPreset);
        presetRow.Children.Add(new TextBlock { Text = "预设名称", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _presetTitle.Width = 150; _presetTitle.ToolTip = "保存预设的名称，相同名称会更新原预设"; System.Windows.Automation.AutomationProperties.SetName(_presetTitle, "预设名称"); presetRow.Children.Add(_presetTitle);
        var savePreset = new Button { Content = "保存预设", Margin = new Thickness(8, 0, 8, 0) }; presetRow.Children.Add(savePreset);
        var removePreset = new Button { Content = "移除预设" }; presetRow.Children.Add(removePreset); options.Children.Add(presetRow);
        applyPreset.Click += (_, _) => { if ((_presets.SelectedItem as ComboBoxItem)?.Tag is RenamePreset preset) { ApplyOptions(preset.Options); _presetTitle.Text = RenamePresets.Builtins.Any(x => x.Id == preset.Id) ? "" : preset.Title; } };
        savePreset.Click += (_, _) => SavePreset(remove: false); removePreset.Click += (_, _) => SavePreset(remove: true);
        options.Children.Add(fields); options.Children.Add(_number); options.Children.Add(_trim);
        var selection = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; selection.Children.Add(_selectAll); _excludeConflicts.Margin = new Thickness(8, 0, 8, 0); selection.Children.Add(_excludeConflicts);
        selection.Children.Add(new TextBlock { Text = "仅执行勾选项；更改后重新预览，按勾选项重新编号。", VerticalAlignment = VerticalAlignment.Center }); options.Children.Add(selection);
        _selectAll.Click += (_, _) => SelectAll(); _excludeConflicts.Click += (_, _) => ExcludeConflicts();
        options.Children.Add(new TextBlock { Text = "顺序：移除字符／清理空格 → 查找替换 → 大小写 → 前后缀 → 编号。预览表按实际编号顺序显示，文件扩展名保留。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12) });
        var optionsScroll = new ScrollViewer { Content = options, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(optionsScroll, 1); root.Children.Add(optionsScroll);
        var choice = new FrameworkElementFactory(typeof(CheckBox));
        choice.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding(nameof(RenamePreviewRow.Included)) { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        choice.SetBinding(IsEnabledProperty, new System.Windows.Data.Binding(nameof(RenamePreviewRow.CanSelect)));
        choice.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "执行此项目");
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "执行", CellTemplate = new DataTemplate { VisualTree = choice }, Width = 60 });
        foreach (var column in new[] { ("原名称", nameof(RenameRow.OriginalName)), ("新名称", nameof(RenameRow.NewName)), ("冲突或错误", nameof(RenameRow.Error)) })
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new System.Windows.Data.Binding(column.Item2), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(_grid, 2); root.Children.Add(_grid);
        _status.Margin = new Thickness(0, 12, 0, 8); var statusPanel = new StackPanel(); statusPanel.Children.Add(_progress); statusPanel.Children.Add(_status); Grid.SetRow(statusPanel, 3); root.Children.Add(statusPanel);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in new[] { _preview, _execute, _undo, _cancel }) { button.Margin = new Thickness(8, 4, 0, 0); buttons.Children.Add(button); }
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        Grid.SetRow(buttons, 4); root.Children.Add(buttons); Content = root;
        _preview.Click += async (_, _) => await PreviewAsync(); _execute.Click += async (_, _) => await ExecuteAsync(); _undo.Click += async (_, _) => await UndoAsync();
        foreach (var box in new[] { _prefix, _suffix, _find, _replace, _start, _digits, _removeStart, _removeEnd }) box.TextChanged += (_, _) => InvalidatePreview();
        _sort.SelectionChanged += (_, _) => InvalidatePreview(); _trim.Checked += (_, _) => InvalidatePreview(); _trim.Unchecked += (_, _) => InvalidatePreview();
        _letterCase.SelectionChanged += (_, _) => InvalidatePreview(); _number.Checked += (_, _) => InvalidatePreview(); _number.Unchecked += (_, _) => InvalidatePreview();
        Closing += (_, e) => { if (_busy) { _cancellation?.Cancel(); e.Cancel = true; _status.Text = "正在取消并恢复文件，请等待完成后关闭。"; } };
        _status.Text = "填写规则后生成预览，可取消勾选后重新编号。启用历史时，完成任务可在设置的“操作历史”中校验撤销。";
        RefreshPresets();
    }
    private void RefreshPresets(string? selectedId = null)
    {
        _presets.Items.Clear();
        foreach (var preset in RenamePresets.Builtins.Concat((Application.Current as App)?.Config.RenamePresets.Select(x => x.Clone()) ?? [])) _presets.Items.Add(new ComboBoxItem { Content = preset.Title, Tag = preset });
        _presets.SelectedItem = _presets.Items.Cast<ComboBoxItem>().FirstOrDefault(x => ((RenamePreset)x.Tag).Id == selectedId) ?? _presets.Items[0];
    }
    internal RenameOptions ReadOptions()
    {
        if (!int.TryParse(_start.Text, out int start) || !int.TryParse(_digits.Text, out int digits) || !int.TryParse(_removeStart.Text, out int removeStart) || !int.TryParse(_removeEnd.Text, out int removeEnd)) throw new InvalidDataException("编号和移除字符数量需要整数");
        var options = new RenameOptions { Prefix = _prefix.Text, Suffix = _suffix.Text, Find = _find.Text, Replace = _replace.Text, LetterCase = _letterCase.SelectedIndex switch { 1 => "lower", 2 => "upper", _ => "keep" }, Numbered = _number.IsChecked == true, Start = start, Digits = digits, SortOrder = (string)((ComboBoxItem)_sort.SelectedItem).Tag, RemoveStart = removeStart, RemoveEnd = removeEnd, TrimSpaces = _trim.IsChecked == true };
        BatchRename.ValidateOptions(options); return options;
    }
    internal void ApplyOptions(RenameOptions options)
    {
        _prefix.Text = options.Prefix; _suffix.Text = options.Suffix; _find.Text = options.Find; _replace.Text = options.Replace;
        _letterCase.SelectedIndex = options.LetterCase switch { "lower" => 1, "upper" => 2, _ => 0 }; _number.IsChecked = options.Numbered;
        _start.Text = options.Start.ToString(); _digits.Text = options.Digits.ToString(); _removeStart.Text = options.RemoveStart.ToString(); _removeEnd.Text = options.RemoveEnd.ToString(); _trim.IsChecked = options.TrimSpaces;
        _sort.SelectedItem = _sort.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == options.SortOrder); InvalidatePreview();
    }
    private void SavePreset(bool remove)
    {
        if (_busy) return;
        try
        {
            var app = Application.Current as App ?? throw new InvalidOperationException("应用不可用"); var next = app.Config.Clone(); string? savedId = null;
            if (remove)
            {
                if ((_presets.SelectedItem as ComboBoxItem)?.Tag is not RenamePreset selected || !next.RenamePresets.Any(x => x.Id == selected.Id)) throw new InvalidOperationException("请选择一个已保存的用户预设");
                next.RenamePresets.RemoveAll(x => x.Id == selected.Id);
            }
            else
            {
                string title = _presetTitle.Text.Trim(); if (title.Length == 0) throw new InvalidDataException("请填写预设名称");
                var existing = next.RenamePresets.FirstOrDefault(x => x.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                if (existing == null) { existing = new() { Title = title, Options = ReadOptions() }; next.RenamePresets.Add(existing); } else existing.Options = ReadOptions();
                savedId = existing.Id;
            }
            string? error = app.SaveSettings(next, StartupService.Enabled); if (error != null) throw new IOException(error);
            RefreshPresets(savedId); _status.Text = remove ? "预设已移除。" : "预设已保存，设置页未保存的其他编辑仍然保留。";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
    private IProgress<(double Value, string Text)> Progress()
    {
        int generation = _operationGeneration;
        return new Progress<(double Value, string Text)>(value => { if (_busy && generation == _operationGeneration) { _progress.Value = value.Value; _status.Text = value.Text; } });
    }
    internal void SelectAll()
    {
        if (_busy || _transaction != null) return;
        foreach (var item in _items) _chosenSources.Add(Source(item)); foreach (var row in _displayRows) row.Included = true; InvalidatePreview();
    }
    internal void ExcludeConflicts()
    {
        if (_busy || _transaction != null) return;
        foreach (var row in _displayRows.Where(x => x.Plan.Error.Length > 0)) row.Included = false;
        InvalidatePreview(); _status.Text = "已排除预览中的冲突项，请重新生成预览检查其余项目。";
    }
    private void DisplayPreview()
    {
        _displayRows.Clear();
        var plans = _rows.Concat(_items.Where(x => !_chosenSources.Contains(Source(x))).Select(x => new RenameRow(Source(x), Source(x), x.IsDirectory, "已排除")));
        foreach (var plan in plans) _displayRows.Add(new(plan, _chosenSources.Contains(plan.Source), value => { if (value) _chosenSources.Add(plan.Source); else _chosenSources.Remove(plan.Source); InvalidatePreview(); }));
        _grid.ItemsSource = _displayRows.ToArray();
    }
    private void InvalidatePreview() { _revision++; _execute.IsEnabled = false; }
    private void Busy(bool value)
    {
        if (value) { _operationGeneration++; _progress.Value = 0; _cancellation?.Dispose(); _cancellation = new(); _lastFailure = false; _idle = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        _busy = value; _cancel.IsEnabled = value; _preview.IsEnabled = !value && _transaction == null; _execute.IsEnabled = false; _undo.IsEnabled = !value && _transaction != null;
        foreach (var row in _displayRows) row.CanSelect = !value && _transaction == null;
        _selectAll.IsEnabled = _excludeConflicts.IsEnabled = !value && _transaction == null;
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
            var options = ReadOptions(); var selected = _items.Where(x => _chosenSources.Contains(Source(x))).ToArray();
            int revision = _revision;
            Busy(true); _status.Text = "正在检查名称和目标冲突…";
            var progress = Progress(); _rows = await Task.Run(() => BatchRename.Preview(selected, options, _cancellation!.Token, progress)); DisplayPreview();
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
        var progress = Progress();
        try
        {
            _transaction = await Task.Run(() => BatchRename.Execute(_rows, _cancellation!.Token, progress));
            _status.Text = "重命名完成，本窗口可撤销。";
            if ((Application.Current as App)?.Config.KeepRenameHistory == true)
            {
                try { _historyId = await Task.Run(() => new RenameHistoryStore().Add(_transaction)); _status.Text += "任务已保存到本机历史，关闭窗口后仍可校验撤销。"; }
                catch (Exception ex) { _status.Text += "历史未保存，请在本窗口撤销：" + ex.Message; }
            }
        }
        catch (OperationCanceledException) { _status.Text = "重命名已取消，已恢复原名称。"; }
        catch (Exception ex) { _lastFailure = true; _status.Text = ex.Message; }
        finally { Busy(false); }
    }
    internal async Task UndoAsync()
    {
        if (_busy || _transaction == null) return;
        Busy(true);
        var progress = Progress();
        try
        {
            string? warning = null;
            await Task.Run(() => { if (_historyId != null) warning = new RenameHistoryStore().Undo(_historyId, _cancellation!.Token, progress, _transaction); else _transaction.Undo(_cancellation!.Token, progress); });
            _transaction = null; _historyId = null; _grid.ItemsSource = null; _displayRows.Clear(); _status.Text = warning ?? "本次重命名已撤销。";
        }
        catch (OperationCanceledException) { _status.Text = "撤销检查已取消，保留当前名称。"; }
        catch (Exception ex) { _lastFailure = true; _status.Text = ex.Message; }
        finally { Busy(false); }
    }
}
