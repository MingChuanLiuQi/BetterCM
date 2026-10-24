using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class WorkspaceImportWindow : Window
{
    private sealed record Row(string Status, string Category, string Title, string Detail);
    private readonly ComboBox _mode = new(), _policy = new();
    private readonly CheckBox _actions = new() { Content = "动作、分组及系统偏好", IsChecked = true }, _profiles = new() { Content = "菜单方案", IsChecked = true }, _favorites = new() { Content = "收藏目录", IsChecked = true }, _presets = new() { Content = "重命名预设", IsChecked = true }, _settings = new() { Content = "外观、快捷键与菜单行为" };
    private readonly TextBox _from = new(), _to = new();
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true };
    private readonly AppConfig _current, _incoming;
    private readonly Button _apply = new() { Content = "加入设置草稿", IsDefault = true };
    private bool _ready;
    private bool _refreshing, _wasRestore;
    private bool[]? _selectedCategories;
    internal WorkspaceMerge? Result { get; private set; }
    internal WorkspaceImportWindow(AppConfig current, AppConfig incoming)
    {
        _current = current.Clone(); _incoming = incoming.Clone();
        Title = "恢复与合并工作区备份"; Width = 1040; Height = 780; MinWidth = 700; MinHeight = 520;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1.1, GridUnitType.Star), GridLength.Auto, new GridLength(1.3, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new() { Height = height });
        root.Children.Add(new TextBlock { Text = "检查备份与设置差异", FontSize = 23, Margin = new Thickness(0, 0, 0, 10) });
        var controls = new StackPanel();
        _mode.Items.Add("选择性合并到当前草稿"); _mode.Items.Add("整体恢复备份中的全部设置"); _mode.SelectedIndex = 0; controls.Children.Add(_mode);
        var categories = new WrapPanel(); foreach (var box in new[] { _actions, _profiles, _favorites, _presets, _settings }) { box.Margin = new Thickness(0, 8, 16, 8); categories.Children.Add(box); box.Checked += (_, _) => Refresh(); box.Unchecked += (_, _) => Refresh(); }
        controls.Children.Add(categories);
        _policy.Items.Add("跳过相同 ID 的项目"); _policy.Items.Add("替换相同 ID 的项目"); _policy.Items.Add("保留两份，并重连导入方案中的动作引用"); _policy.SelectedIndex = 2; controls.Children.Add(_policy);
        controls.Children.Add(new TextBlock { Text = "重复项按 ID 识别。内置动作偏好仅在选择替换时覆盖；整体恢复包括快捷键和菜单接管方式，登录自启不在备份中。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        var map = new Grid(); map.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); map.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var field in new[] { ("旧根目录（可选路径映射）", _from, 0), ("本机新根目录", _to, 1) })
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; panel.Children.Add(new TextBlock { Text = field.Item1, Margin = new Thickness(0, 0, 0, 6) }); panel.Children.Add(field.Item2); System.Windows.Automation.AutomationProperties.SetName(field.Item2, field.Item1); Grid.SetColumn(panel, field.Item3); map.Children.Add(panel);
        }
        controls.Children.Add(map);
        var browse = new Button { Content = "选择本机新根目录…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog(); if (picker.ShowDialog(this) == true) _to.Text = picker.FolderName; }; controls.Children.Add(browse);
        var controlScroll = new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(controlScroll, 1); root.Children.Add(controlScroll);
        Grid.SetRow(_summary, 2); root.Children.Add(_summary);
        foreach (var column in new[] { ("变化", "Status", 90), ("类别", "Category", 110), ("名称", "Title", 180), ("合并后的内容／原内容", "Detail", 0) })
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis)); style.Setters.Add(new Setter(ToolTipProperty, new Binding(column.Item2)));
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), ElementStyle = style, Width = column.Item3 == 0 ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(column.Item3) });
        }
        Grid.SetRow(_grid, 3); root.Children.Add(_grid);
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; footer.Children.Add(new TextBlock { Text = "这里仅生成设置草稿，仍需保存后生效；不会运行程序。表格中长路径与参数可悬停查看。", TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) }); buttons.Children.Add(_apply); footer.Children.Add(buttons); Grid.SetRow(footer, 4); root.Children.Add(footer); Content = root;
        _mode.SelectionChanged += (_, _) => Refresh(); _policy.SelectionChanged += (_, _) => Refresh(); _from.TextChanged += (_, _) => Refresh(); _to.TextChanged += (_, _) => Refresh();
        _apply.Click += (_, _) => { Refresh(); if (Result != null) DialogResult = true; };
        _ready = true; Refresh();
    }
    internal void SelectRestore(bool restore) => _mode.SelectedIndex = restore ? 1 : 0;
    private void Refresh()
    {
        if (!_ready || _refreshing) return;
        _refreshing = true;
        bool restore = _mode.SelectedIndex == 1;
        var boxes = new[] { _actions, _profiles, _favorites, _presets, _settings };
        if (restore && !_wasRestore) { _selectedCategories = boxes.Select(x => x.IsChecked == true).ToArray(); foreach (var box in boxes) box.IsChecked = true; }
        else if (!restore && _wasRestore && _selectedCategories != null) { for (int i = 0; i < boxes.Length; i++) boxes[i].IsChecked = _selectedCategories[i]; }
        _wasRestore = restore;
        foreach (var box in boxes) box.IsEnabled = !restore;
        _policy.IsEnabled = !restore;
        try
        {
            Result = WorkspaceTransfer.Merge(_current, _incoming, new() { Restore = restore, Actions = _actions.IsChecked == true, Profiles = _profiles.IsChecked == true, Favorites = _favorites.IsChecked == true, Presets = _presets.IsChecked == true, Settings = _settings.IsChecked == true, Policy = (CustomActionDuplicatePolicy)_policy.SelectedIndex, FromRoot = _from.Text.Trim(), ToRoot = _to.Text.Trim() });
            var next = Result.Configuration; var rows = new List<Row>();
            void Rows<T>(string category, IEnumerable<T> before, IEnumerable<T> after, Func<T, string> id, Func<T, string> title, Func<T, string> detail)
            {
                var previous = before.GroupBy(id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal); var nextIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in after) { string key = id(item); nextIds.Add(key); bool exists = previous.TryGetValue(key, out var old); bool same = exists && System.Text.Json.JsonSerializer.Serialize(old) == System.Text.Json.JsonSerializer.Serialize(item); rows.Add(new(exists ? same ? "保留" : "更新" : "新增", category, title(item), detail(item))); }
                foreach (var item in before.Where(x => !nextIds.Contains(id(x)))) rows.Add(new("移除", category, title(item), detail(item)));
            }
            Rows("自定义动作", _current.CustomActions, next.CustomActions, x => x.Id, x => x.Title, x => x.Executable + " " + x.Arguments + $" · 分组 {x.Group} · 运行参数 {x.Parameters.Count} 项");
            Rows("内置偏好", Models.ActionCatalog.Ordered(_current).Select(x => x.Preference), Models.ActionCatalog.Ordered(next).Select(x => x.Preference), x => x.Id, x => Models.ActionCatalog.ById.GetValueOrDefault(x.Id)?.Title ?? x.Id, x => $"启用 {(x.Enabled ? "开" : "关")} · 直接显示 {(x.IsPrimary ? "开" : "关")} · 分组 {x.Group}");
            string ActionTitle(string id) => Models.ActionCatalog.ById.GetValueOrDefault(id)?.Title ?? next.CustomActions.FirstOrDefault(x => x.Id == id)?.Title ?? (id == "favorite:copy" ? "复制到收藏目录" : id == "favorite:move" ? "移动到收藏目录" : "暂不可用：" + id);
            Rows("菜单方案", _current.MenuProfiles, next.MenuProfiles, x => x.Id, x => x.Title, x => x.DirectoryRoot + " · 动作 " + string.Join(", ", x.ActionIds.Select(ActionTitle)));
            Rows("收藏目录", _current.FavoriteDestinations, next.FavoriteDestinations, x => x.Id, x => x.Title, x => x.Path + (x.DateSubfolder ? " · 日期子目录" : ""));
            Rows("重命名预设", _current.RenamePresets, next.RenamePresets, x => x.Id, x => x.Title, x => System.Text.Json.JsonSerializer.Serialize(x.Options));
            string ProfileTitle(AppConfig config) => config.MenuProfiles.FirstOrDefault(x => x.Id == config.ActiveProfileId)?.Title ?? "默认";
            rows.Add(new("设置", "方案选择", "手动／自动方案", $"{ProfileTitle(_current)} / 自动{(_current.AutoSelectProfile ? "开" : "关")} → {ProfileTitle(next)} / 自动{(next.AutoSelectProfile ? "开" : "关")}"));
            var labels = new Dictionary<string, string>
            {
                [nameof(AppConfig.GlobalHotkey)] = "全局快捷键",
                [nameof(AppConfig.ReplaceExplorerMenu)] = "接管资源管理器右键",
                [nameof(AppConfig.ClassicMenu)] = "经典菜单模式",
                [nameof(AppConfig.Theme)] = "主题",
                [nameof(AppConfig.Material)] = "窗口材质",
                [nameof(AppConfig.EnableAnimations)] = "启用动效",
                [nameof(AppConfig.ShowFileDetails)] = "显示文件详情",
                [nameof(AppConfig.IncludeShellActions)] = "包含系统菜单动作",
                [nameof(AppConfig.ShowNotifications)] = "通知",
                [nameof(AppConfig.ShowMoreItems)] = "折叠更多项目",
                [nameof(AppConfig.CompactMenu)] = "紧凑菜单",
                [nameof(AppConfig.MoreHoverDelayMs)] = "更多项目悬停延时",
                [nameof(AppConfig.SubmenuHoverDelayMs)] = "侧边菜单悬停延时",
                [nameof(AppConfig.OpenAnimation)] = "打开动画",
                [nameof(AppConfig.CloseAnimation)] = "关闭动画",
                [nameof(AppConfig.OpenAnimationMs)] = "打开时长",
                [nameof(AppConfig.CloseAnimationMs)] = "关闭时长",
                [nameof(AppConfig.AnimationDistance)] = "动画位移",
                [nameof(AppConfig.AnimationScalePercent)] = "动画缩放",
                [nameof(AppConfig.AnimationEasing)] = "动画节奏",
                [nameof(AppConfig.EnableExpansionAnimations)] = "展开动效",
                [nameof(AppConfig.MoreExpansionAnimation)] = "更多展开方式",
                [nameof(AppConfig.GroupExpansionAnimation)] = "子菜单展开方式",
                [nameof(AppConfig.MoreExpansionMs)] = "更多展开时长",
                [nameof(AppConfig.GroupExpansionMs)] = "子菜单展开时长"
            };
            foreach (var pair in labels)
            {
                var property = typeof(AppConfig).GetProperty(pair.Key)!; object? before = property.GetValue(_current), after = property.GetValue(next);
                string Display(object? value) => value is bool flag ? flag ? "开" : "关" : value?.ToString() ?? "";
                if (!Equals(before, after)) rows.Add(new("更新", "外观与输入", pair.Value, $"{Display(before)} → {Display(after)}"));
            }
            if (!_current.PinnedShellActions.SetEquals(next.PinnedShellActions)) rows.Add(new("更新", "系统偏好", "固定系统动作", string.Join("；", next.PinnedShellActions)));
            if (!_current.HiddenShellActions.SetEquals(next.HiddenShellActions)) rows.Add(new("更新", "系统偏好", "隐藏系统动作", string.Join("；", next.HiddenShellActions)));
            _grid.ItemsSource = rows;
            _summary.Text = $"当前 {_current.CustomActions.Count} 个动作 / {_current.MenuProfiles.Count} 个方案；备份 {_incoming.CustomActions.Count} 个动作 / {_incoming.MenuProfiles.Count} 个方案。\n合并后：新增 {Result.Added}、替换 {Result.Replaced}、跳过 {Result.Skipped}，映射路径 {Result.MappedPaths} 项，暂不可用的方案引用 {Result.UnresolvedReferences} 项（保留引用，恢复对应动作后可用）。";
            _apply.IsEnabled = true;
        }
        catch (Exception ex) { Result = null; _apply.IsEnabled = false; _summary.Text = ex.Message; _grid.ItemsSource = null; }
        finally { _refreshing = false; }
    }
}
