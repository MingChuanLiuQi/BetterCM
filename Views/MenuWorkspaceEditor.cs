using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public sealed class MenuWorkspaceEditor : UserControl
{
    private readonly ObservableCollection<FavoriteDestination> _favorites = [];
    private readonly ObservableCollection<MenuProfile> _profiles = [];
    private readonly ComboBox _active = new();
    private readonly CheckBox _automatic = new() { Content = "按目标目录自动选择方案（最深目录优先，无匹配时使用手动方案）" };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private Func<IReadOnlyList<(string Id, string Title)>> _actions = () => [];
    private SelectedContext? _recent;
    private Func<SelectedContext?>? _recentProvider;
    private List<RenamePreset> _renamePresets = [];
    public MenuWorkspaceEditor()
    {
        var panel = new StackPanel { Margin = new Thickness(4, 0, 12, 0) };
        void Heading(string title, string description)
        {
            panel.Children.Add(new TextBlock { Text = title, FontSize = 21, Margin = new Thickness(0, 18, 0, 8) });
            panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        }
        Heading("菜单方案", "方案决定增强菜单中提供哪些动作。工具栏保留；经典模式不受影响。场景规则和程序可用性仍会继续筛选。默认方案提供全部已启用动作。");
        System.Windows.Automation.AutomationProperties.SetName(_active, "手动菜单方案"); panel.Children.Add(_active); panel.Children.Add(_automatic);
        var profiles = GridFor(_profiles, [("方案名称", "Title"), ("自动匹配目录", "DirectoryRoot")], 145); panel.Children.Add(profiles);
        var profileButtons = new WrapPanel(); panel.Children.Add(profileButtons);
        Button(profileButtons, "添加方案", () => EditProfile(null));
        Button(profileButtons, "编辑方案", () => { if (profiles.SelectedItem is MenuProfile value) EditProfile(value); });
        Button(profileButtons, "移除方案", () => { if (profiles.SelectedItem is MenuProfile value) { string active = ActiveId; _profiles.Remove(value); RefreshProfiles(active == value.Id ? "" : active); } });
        Button(profileButtons, "测试最近选区", () =>
        {
            _status.Text = TestRecentSelection();
        });
        Heading("收藏目录", "增强菜单提供“复制到”和“移动到”子菜单。Windows 负责冲突选择、进度和取消；不占用剪贴板。日期子目录按执行当天创建，例如 2026-10-24。");
        var favorites = GridFor(_favorites, [("名称", "Title"), ("目标目录", "Path")], 180);
        favorites.Columns.Add(new DataGridCheckBoxColumn { Header = "日期子目录", Binding = new Binding(nameof(FavoriteDestination.DateSubfolder)), IsReadOnly = true }); panel.Children.Add(favorites);
        var favoriteButtons = new WrapPanel(); panel.Children.Add(favoriteButtons);
        Button(favoriteButtons, "添加目录", () => EditFavorite(null));
        Button(favoriteButtons, "编辑目录", () => { if (favorites.SelectedItem is FavoriteDestination value) EditFavorite(value); });
        Button(favoriteButtons, "移除目录", () => { if (favorites.SelectedItem is FavoriteDestination value) _favorites.Remove(value); });
        Button(favoriteButtons, "上移", () => { if (favorites.SelectedItem is FavoriteDestination value && _favorites.IndexOf(value) > 0) _favorites.Move(_favorites.IndexOf(value), _favorites.IndexOf(value) - 1); });
        _status.Margin = new Thickness(0, 12, 0, 12); panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "此页修改进入设置草稿，点击底部“保存设置”后生效。动作的目录、名称和数量规则可在“内置动作 → 场景与分组”或自定义动作编辑器中设置。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private static DataGrid GridFor<T>(ObservableCollection<T> values, (string Title, string Property)[] columns, double height)
    {
        var grid = new DataGrid { ItemsSource = values, AutoGenerateColumns = false, IsReadOnly = true, Height = height, CanUserAddRows = false, Margin = new Thickness(0, 12, 0, 8) };
        foreach (var column in columns)
        {
            var text = new Style(typeof(TextBlock)); text.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            text.Setters.Add(new Setter(ToolTipProperty, new Binding(column.Property)));
            grid.Columns.Add(new DataGridTextColumn { Header = column.Title, Binding = new Binding(column.Property), ElementStyle = text, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        }
        return grid;
    }
    private static void Button(Panel panel, string title, Action click)
    {
        var button = new Button { Content = title, Margin = new Thickness(0, 4, 8, 4) }; button.Click += (_, _) => click(); panel.Children.Add(button);
    }
    private string ActiveId => (_active.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    private void RefreshProfiles(string id)
    {
        _active.Items.Clear(); _active.Items.Add(new ComboBoxItem { Content = "默认（全部已启用动作）", Tag = "" });
        foreach (var value in _profiles) _active.Items.Add(new ComboBoxItem { Content = value.Title, Tag = value.Id });
        _active.SelectedItem = _active.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == id) ?? _active.Items[0];
    }
    internal string TestRecentSelection()
    {
        var recent = _recentProvider == null ? _recent : _recentProvider();
        if (recent == null) return "请先在 Explorer 中打开一次菜单。";
        var config = new AppConfig(); Apply(config); var selected = ActionRules.SelectProfile(config, recent);
        return $"选区 {recent.CapturedAt:HH:mm:ss} · {recent.Items.Count} 项 · {recent.PrimaryName}\n" + (selected == null ? "使用默认方案。动作自身规则仍会继续筛选。" : $"使用“{selected.Title}”，方案中勾选 {selected.ActionIds.Count} 项。");
    }
    public void Load(AppConfig config, Func<IReadOnlyList<(string Id, string Title)>> actions, SelectedContext? recent, Func<SelectedContext?>? recentProvider = null)
    {
        _actions = actions; _recent = recent; _recentProvider = recentProvider;
        _profiles.Clear(); foreach (var value in config.MenuProfiles) _profiles.Add(value.Clone());
        _favorites.Clear(); foreach (var value in config.FavoriteDestinations) _favorites.Add(value.Clone());
        _automatic.IsChecked = config.AutoSelectProfile; RefreshProfiles(config.ActiveProfileId); _status.Text = "";
        _renamePresets = config.RenamePresets.Select(x => x.Clone()).ToList();
    }
    public void Apply(AppConfig config)
    {
        config.MenuProfiles = _profiles.Select(x => x.Clone()).ToList(); config.FavoriteDestinations = _favorites.Select(x => x.Clone()).ToList();
        config.ActiveProfileId = ActiveId; config.AutoSelectProfile = _automatic.IsChecked == true;
        config.RenamePresets = _renamePresets.Select(x => x.Clone()).ToList();
    }
    private void EditProfile(MenuProfile? original)
    {
        var value = original?.Clone() ?? new MenuProfile(); string active = ActiveId;
        var panel = new StackPanel(); var title = Field(panel, "方案名称", value.Title); var root = FolderField(panel, "自动匹配目录（可选）", value.DirectoryRoot);
        panel.Children.Add(new TextBlock { Text = "勾选方案提供的动作（最终仍受启用状态和场景规则限制）", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) });
        var choices = new List<(string Id, CheckBox Box)>();
        var available = _actions().Concat(new[] { ("favorite:copy", "收藏目录：复制到"), ("favorite:move", "收藏目录：移动到") }).ToList();
        foreach (var id in value.ActionIds.Except(available.Select(x => x.Item1), StringComparer.Ordinal)) available.Add((id, "暂不可用的动作：" + id));
        foreach (var action in available)
        {
            var box = new CheckBox { Content = action.Item2, IsChecked = value.ActionIds.Contains(action.Item1, StringComparer.Ordinal), Margin = new Thickness(0, 2, 0, 2) }; panel.Children.Add(box); choices.Add((action.Item1, box));
        }
        var dialog = Editor("菜单方案", panel, () =>
        {
            value.Title = title.Text.Trim(); value.DirectoryRoot = root.Text.Trim(); value.ActionIds = choices.Where(x => x.Box.IsChecked == true).Select(x => x.Id).ToList();
            ActionRules.ValidateConfiguration(new() { MenuProfiles = [value] });
        });
        if (dialog.ShowDialog() != true) return;
        if (original == null) _profiles.Add(value); else _profiles[_profiles.IndexOf(original)] = value;
        RefreshProfiles(active);
    }
    private void EditFavorite(FavoriteDestination? original)
    {
        var value = original?.Clone() ?? new FavoriteDestination(); var panel = new StackPanel();
        var title = Field(panel, "收藏名称", value.Title); var path = FolderField(panel, "目标目录", value.Path);
        var date = new CheckBox { Content = "按当天日期创建子目录", IsChecked = value.DateSubfolder, Margin = new Thickness(0, 14, 0, 0) }; panel.Children.Add(date);
        var dialog = Editor("收藏目录", panel, () => { value.Title = title.Text.Trim(); value.Path = path.Text.Trim(); value.DateSubfolder = date.IsChecked == true; ActionRules.ValidateConfiguration(new() { FavoriteDestinations = [value] }); });
        if (dialog.ShowDialog() != true) return;
        if (original == null) _favorites.Add(value); else _favorites[_favorites.IndexOf(original)] = value;
    }
    private static TextBox Field(Panel panel, string title, string value)
    {
        panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 10, 0, 6) });
        var box = new TextBox { Text = value }; System.Windows.Automation.AutomationProperties.SetName(box, title); panel.Children.Add(box); return box;
    }
    private static TextBox FolderField(Panel panel, string title, string value)
    {
        var box = Field(panel, title, value); var browse = new Button { Content = "选择文件夹…", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog(); if (picker.ShowDialog(Window.GetWindow(panel)) == true) box.Text = picker.FolderName; }; panel.Children.Add(browse); return box;
    }
    private Window Editor(string title, UIElement panel, Action apply)
    {
        var dialog = new Window { Title = title, Owner = Window.GetWindow(this), Width = 580, Height = 620, MinWidth = 400, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner, Style = (Style)FindResource(typeof(Window)) };
        ThemeService.Attach(dialog); SettingsWindowSizing.Attach(dialog);
        var root = new DockPanel { Margin = new Thickness(24) }; var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; footer.Children.Add(error);
        var save = new Button { Content = "应用到列表", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; footer.Children.Add(save);
        save.Click += (_, _) => { try { apply(); dialog.DialogResult = true; } catch (Exception ex) { error.Text = ex.Message; } };
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.Content = root; return dialog;
    }
}
