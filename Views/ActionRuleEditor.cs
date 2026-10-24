using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public sealed class ActionRuleEditor : StackPanel
{
    private readonly TextBox _group = new(), _pattern = new(), _root = new(), _minimum = new(), _maximum = new();
    private readonly ComboBox _target = new();
    public ActionRuleEditor(ActionRule rule, string group, SelectedContext? recent = null, bool allowGroup = true)
    {
        void Field(string title, UIElement control)
        {
            Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 12, 0, 6) });
            System.Windows.Automation.AutomationProperties.SetName(control, title); Children.Add(control);
        }
        _group.Text = group; _group.IsEnabled = allowGroup; Field("菜单分组（留空为不分组，仅一层）", _group);
        foreach (var item in new[] { ("所有文件系统目标", "any"), ("仅选中的文件", "files"), ("仅选中的文件夹", "folders"), ("仅文件夹空白处", "background") }) _target.Items.Add(new ComboBoxItem { Content = item.Item1, Tag = item.Item2 });
        _target.SelectedItem = _target.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == rule.Target); Field("目标类型", _target);
        _pattern.Text = rule.NamePattern; Field("名称通配符（* 匹配任意内容，? 匹配一个字符）", _pattern);
        _root.Text = rule.DirectoryRoot;
        var directory = new DockPanel(); var browse = new Button { Content = "选择…", Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right); directory.Children.Add(browse); directory.Children.Add(_root);
        browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog(); if (picker.ShowDialog(Window.GetWindow(this)) == true) _root.Text = picker.FolderName; };
        Field("限定目录及其子目录（可选）", directory);
        _minimum.Text = rule.MinimumCount.ToString(); _maximum.Text = rule.MaximumCount.ToString();
        Field("最少选中数量（空白处为 0 项）", _minimum); Field("最多选中数量（0 表示不限）", _maximum);
        var test = new Button { Content = "用最近选区测试规则", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0), IsEnabled = recent?.HasFileTarget == true };
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        test.Click += (_, _) => { try { string reason = ActionRules.Explain(ReadRule(), recent!); result.Text = reason.Length == 0 ? "规则匹配。动作还需满足启用状态、文件类型、多选方式及菜单方案。" : "规则不匹配：" + reason; } catch (Exception ex) { result.Text = ex.Message; } };
        Children.Add(test); Children.Add(result);
    }
    public string Group => _group.Text.Trim();
    public ActionRule ReadRule()
    {
        if (!int.TryParse(_minimum.Text, out int minimum) || !int.TryParse(_maximum.Text, out int maximum)) throw new InvalidDataException("选中数量需要非负整数");
        var rule = new ActionRule { Target = (string)((ComboBoxItem)_target.SelectedItem).Tag, NamePattern = _pattern.Text.Trim(), DirectoryRoot = _root.Text.Trim(), MinimumCount = minimum, MaximumCount = maximum };
        ActionRules.ValidateRule(rule, Group); return rule;
    }
}

public sealed class ActionRuleWindow : Window
{
    private readonly ActionRuleEditor _editor;
    public ActionRule Result { get; private set; }
    public string Group { get; private set; }
    public ActionRuleWindow(string title, ActionRule rule, string group, SelectedContext? recent, bool allowGroup = true)
    {
        Result = rule.Clone(); Group = group;
        Title = title + " · 场景与分组"; Width = 560; Height = 660; MinWidth = 400; MinHeight = 420;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; footer.Children.Add(error);
        var apply = new Button { Content = "应用到列表", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        footer.Children.Add(apply);
        _editor = new(rule, group, recent, allowGroup);
        root.Children.Add(new ScrollViewer { Content = _editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        apply.Click += (_, _) => { try { Result = _editor.ReadRule(); Group = _editor.Group; DialogResult = true; } catch (Exception ex) { error.Text = ex.Message; } };
    }
}
