using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class CustomActionImportWindow : Window
{
    private readonly IReadOnlyList<CustomActionItem> _existing, _incoming;
    private readonly ComboBox _policy = new();
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
    internal CustomActionMerge? Result { get; private set; }
    internal void SelectPolicy(CustomActionDuplicatePolicy policy) => _policy.SelectedIndex = (int)policy;
    internal CustomActionImportWindow(IEnumerable<CustomActionItem> existing, IEnumerable<CustomActionItem> incoming)
    {
        _existing = new AppConfig { CustomActions = existing.ToList() }.Clone().CustomActions;
        _incoming = new AppConfig { CustomActions = incoming.ToList() }.Clone().CustomActions;
        Style = (Style)FindResource(typeof(Window)); Title = "导入自定义动作";
        Width = 820; Height = 620; MinWidth = 560; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SettingsWindowSizing.Attach(this); ThemeService.Attach(this);
        var layout = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) layout.RowDefinitions.Add(new() { Height = height });
        layout.Children.Add(new TextBlock { Text = "确认导入内容", FontSize = 23, Margin = new Thickness(0, 0, 0, 12) });
        var choice = new StackPanel();
        choice.Children.Add(new TextBlock { Text = "重复项按相同 ID，或相同名称与启动配置识别。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        _policy.Items.Add("跳过重复项，保留当前设置"); _policy.Items.Add("替换重复项，保留原列表位置"); _policy.Items.Add("保留两份，作为独立动作");
        System.Windows.Automation.AutomationProperties.SetName(_policy, "重复动作处理方式");
        _policy.SelectedIndex = 0; choice.Children.Add(_policy); Grid.SetRow(choice, 1); layout.Children.Add(choice);
        Grid.SetRow(_summary, 2); layout.Children.Add(_summary);
        var list = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, ItemsSource = _incoming, CanUserAddRows = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        foreach (var column in new[] { ("动作名称", "Title"), ("程序", "Executable"), ("参数", "Arguments"), ("分组", "Group"), ("场景规则", "Rule"), ("多选方式", "SelectionMode"), ("管理员", "RunAsAdmin"), ("目标工作目录", "UseTargetWorkingDirectory"), ("启用", "Enabled") })
        {
            if (column.Item2 is "RunAsAdmin" or "UseTargetWorkingDirectory" or "Enabled")
            {
                list.Columns.Add(new DataGridCheckBoxColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = DataGridLength.Auto, MinWidth = 70 });
                continue;
            }
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            textStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(column.Item2) { Converter = column.Item2 == "Rule" ? new RuleText() : null }));
            textStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0)));
            list.Columns.Add(new DataGridTextColumn
            {
                Header = column.Item1,
                Binding = new Binding(column.Item2) { Converter = column.Item2 == "SelectionMode" ? new ModeText() : column.Item2 == "Rule" ? new RuleText() : null },
                ElementStyle = textStyle,
                Width = column.Item2 == "Rule" ? new DataGridLength(240) : column.Item2 is "Title" or "Executable" or "Arguments" ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto,
                MinWidth = 80
            });
        }
        Grid.SetRow(list, 3); layout.Children.Add(list);
        var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        footer.Children.Add(new TextBlock { Text = "加入列表后仍需保存设置。此操作不会启动程序，也不会切换菜单模式。", TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) });
        var apply = new Button { Content = "加入待保存列表", IsDefault = true };
        buttons.Children.Add(apply); footer.Children.Add(buttons); Grid.SetRow(footer, 4); layout.Children.Add(footer);
        void Refresh()
        {
            try
            {
                Result = CustomActionTransfer.Merge(_existing, _incoming, (CustomActionDuplicatePolicy)_policy.SelectedIndex);
                _summary.Text = $"文件包含 {_incoming.Count} 项：新增 {Result.Added}、替换 {Result.Replaced}、跳过 {Result.Skipped}。导入后共 {Result.Actions.Count} 项。";
                apply.IsEnabled = true;
            }
            catch (InvalidDataException ex) { Result = null; _summary.Text = ex.Message; apply.IsEnabled = false; }
        }
        _policy.SelectionChanged += (_, _) => Refresh(); apply.Click += (_, _) => { if (Result != null) DialogResult = true; };
        Content = layout; Refresh();
    }
    private sealed class RuleText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ActionRule rule) return "";
            string target = rule.Target switch { "files" => "文件", "folders" => "文件夹", "background" => "空白处", _ => "所有目标" };
            return $"{target}；{rule.NamePattern}；{rule.MinimumCount}–{(rule.MaximumCount == 0 ? "不限" : rule.MaximumCount.ToString(culture))} 项；{(rule.DirectoryRoot.Length == 0 ? "全部目录" : rule.DirectoryRoot)}";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
    private sealed class ModeText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as string) switch { "each" => "逐个运行", "batch" => "一次批量", _ => "单个项目" };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
