using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Services;
using BetterCM.Models;
namespace BetterCM.Views;

public sealed class CustomActionWindow : Window
{
    private readonly TextBox _title = new(), _executable = new(), _arguments = new(), _extensions = new();
    private readonly ComboBox _mode = new();
    private readonly CheckBox _admin = new() { Content = "以管理员身份运行" }, _primary = new() { Content = "直接显示在菜单第一层" };
    private readonly CheckBox _targetDirectory = new() { Content = "使用目标文件夹作为工作目录" };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _templates = new(), _previewChoice = new();
    private readonly TextBox _preview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _templateHint = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SelectedContext? _recentContext;
    private bool _previewReady;
    private readonly ActionRuleEditor _rules;
    private readonly RuntimeParameterEditor _parameters;
    public CustomActionItem Result { get; private set; }
    public CustomActionWindow(CustomActionItem original, SelectedContext? recentContext = null)
    {
        _recentContext = recentContext == null ? null : recentContext with { Items = recentContext.Items.ToArray(), ShellParsingPaths = recentContext.ShellParsingPaths.ToArray() };
        Result = JsonSerializer.Deserialize<CustomActionItem>(JsonSerializer.Serialize(original))!;
        Style = (Style)FindResource(typeof(Window));
        ThemeService.Attach(this);
        Title = original.Title.Length == 0 ? "添加自定义动作" : "编辑自定义动作";
        Width = 1040; Height = Math.Min(800, SystemParameters.WorkArea.Height); MinWidth = 760; MinHeight = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SettingsWindowSizing.Attach(this);
        var layout = new Grid { Margin = new Thickness(28) };
        layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        var heading = new TextBlock { Text = Title, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(heading);
        void Hint(string text)
        {
            var hint = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 20, Margin = new Thickness(0, 8, 0, 4) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); panel.Children.Add(hint);
        }
        Hint("选择要打开的程序，再告诉它如何接收选中的文件。添加后仍需在设置页保存。");
        var templateRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var applyTemplate = new Button { Name = "ApplyActionTemplateButton", Content = "使用模板", Margin = new Thickness(10, 0, 0, 0) };
        DockPanel.SetDock(applyTemplate, Dock.Right); templateRow.Children.Add(applyTemplate);
        foreach (var template in CustomActionTemplates.All) _templates.Items.Add(new ComboBoxItem { Content = template.Title, Tag = template.Id, ToolTip = template.Description });
        _templates.SelectedIndex = 0;
        System.Windows.Automation.AutomationProperties.SetName(_templates, "常用动作模板");
        templateRow.Children.Add(_templates); panel.Children.Add(templateRow);
        _templateHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        _templateHint.Margin = new Thickness(0, 8, 0, 0); panel.Children.Add(_templateHint);
        void TemplateHint() => _templateHint.Text = CustomActionTemplates.All.FirstOrDefault(x => x.Id == ((_templates.SelectedItem as ComboBoxItem)?.Tag as string))?.Description ?? "";
        _templates.SelectionChanged += (_, _) => TemplateHint(); TemplateHint();
        applyTemplate.Click += (_, _) => { if ((_templates.SelectedItem as ComboBoxItem)?.Tag is string id) ApplyTemplate(id); };
        void Field(string title, UIElement control)
        {
            panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 18, 0, 8) });
            System.Windows.Automation.AutomationProperties.SetName(control, title); panel.Children.Add(control);
        }
        _title.Text = Result.Title; _executable.Text = Result.Executable; _arguments.Text = Result.Arguments;
        _extensions.Text = string.Join(", ", Result.MatchExtensions);
        _mode.Items.Add(new ComboBoxItem { Content = "只处理一个项目", Tag = "single" });
        _mode.Items.Add(new ComboBoxItem { Content = "为每个选中项目分别运行", Tag = "each" });
        _mode.Items.Add(new ComboBoxItem { Content = "一次处理所有选中项目", Tag = "batch" });
        _mode.SelectedIndex = Result.SelectionMode switch { "each" => 1, "batch" => 2, _ => 0 }; _mode.Height = 32;
        _admin.IsChecked = Result.RunAsAdmin; _primary.IsChecked = Result.IsPrimary;
        _targetDirectory.IsChecked = Result.UseTargetWorkingDirectory;
        var executableRow = new Grid();
        executableRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        executableRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        System.Windows.Automation.AutomationProperties.SetName(_executable, "打开程序");
        var browse = new Button { Name = "BrowseExecutableButton", Content = "浏览…", Margin = new Thickness(10, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要打开的程序",
                Filter = "可执行程序 (*.exe;*.com)|*.exe;*.com",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true) ApplySelectedExecutable(dialog.FileName);
        };
        Grid.SetColumn(browse, 1);
        executableRow.Children.Add(_executable);
        executableRow.Children.Add(browse);
        Field("菜单中显示的名称", _title); Field("打开程序", executableRow);
        Hint("填写 .exe 或 .com 的完整路径，或 PATH 中的程序名称（保存时固定为完整路径）。VS Code 可填写 Code.exe。");
        Field("启动参数（可选）", _arguments);
        _parameters = new(Result.Parameters); _parameters.Changed += RefreshPreview;
        panel.Children.Add(new Expander { Header = "执行前填写的参数", Content = _parameters, Margin = new Thickness(0, 12, 0, 0) });
        Hint("{path} 表示选中项目的路径，{dir} 表示所在文件夹，{name} 表示名称。\n例如：\"{path}\"。路径占位符请保留双引号，避免空格被拆开。");
        Field("适用的文件类型", _extensions);
        Hint("用逗号分隔，例如 .txt, .md。* 表示所有文件，dir 表示文件夹。");
        Field("选中多个项目时", _mode);
        Hint("选择“一次处理所有选中项目”时，在参数中用独立的 {paths} 传入全部路径。");
        panel.Children.Add(_targetDirectory);
        Hint("默认在程序所在目录运行。只有程序确实需要时才勾选此项；目标文件夹中的 DLL 或插件可能被该程序加载。{dir} 参数仍始终表示目标所在目录。");
        panel.Children.Add(_admin); panel.Children.Add(_primary);
        _rules = new(Result.Rule, Result.Group, _recentContext);
        panel.Children.Add(new Expander { Header = "场景规则与菜单分组", Content = _rules, Margin = new Thickness(0, 14, 0, 0) });
        _previewChoice.Items.Add("示例：一个文件"); _previewChoice.Items.Add("示例：两个含空格的文件"); _previewChoice.Items.Add("示例：一个文件夹");
        if (_recentContext?.HasFileTarget == true) _previewChoice.Items.Add("最近一次右键选区");
        _previewChoice.SelectedIndex = 0;
        System.Windows.Automation.AutomationProperties.SetName(_previewChoice, "预览使用的选区");
        System.Windows.Automation.AutomationProperties.SetName(_preview, "启动参数预览");
        foreach (var box in new[] { _title, _executable, _arguments, _extensions }) box.TextChanged += (_, _) => RefreshPreview();
        _mode.SelectionChanged += (_, _) => RefreshPreview(); _previewChoice.SelectionChanged += (_, _) => RefreshPreview();
        foreach (var box in new[] { _admin, _targetDirectory })
        { box.Checked += (_, _) => RefreshPreview(); box.Unchecked += (_, _) => RefreshPreview(); }
        _error.Visibility = Visibility.Collapsed;
        _error.Margin = new Thickness(0, 12, 0, 0);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
        var save = new Button { Content = "应用到列表", IsDefault = true };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        save.Click += (_, _) =>
        {
            try
            {
                var draft = ReadDraft(); draft.Executable = ExecutableResolver.Require(draft.Executable);
                ConfigStore.Validate(new() { CustomActions = [draft] }); ActionExecutor.ParseArguments(draft.Arguments);
                Result = draft;
                DialogResult = true;
            }
            catch (Exception ex) { _error.Text = ex.Message; _error.Visibility = Visibility.Visible; }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save);
        var content = new Grid();
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1.15, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 12, 0) });
        var previewPanel = new Grid { Margin = new Thickness(12, 0, 0, 0) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star) }) previewPanel.RowDefinitions.Add(new() { Height = height });
        previewPanel.Children.Add(new TextBlock { Text = "启动预览", FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        Grid.SetRow(_previewChoice, 1); previewPanel.Children.Add(_previewChoice);
        var previewHint = new TextBlock { Text = "只预览参数，不运行程序或检查文件是否存在。示例名称按文件类型调整。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 10, 0, 12) };
        previewHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); Grid.SetRow(previewHint, 2); previewPanel.Children.Add(previewHint);
        Grid.SetRow(_preview, 3); previewPanel.Children.Add(_preview); Grid.SetColumn(previewPanel, 1); content.Children.Add(previewPanel);
        layout.Children.Add(content);
        var footer = new StackPanel();
        footer.Children.Add(_error); footer.Children.Add(buttons);
        Grid.SetRow(footer, 1); layout.Children.Add(footer);
        Content = layout;
        _previewReady = true; RefreshPreview();
    }
    private CustomActionItem ReadDraft() => new()
    {
        Id = Result.Id,
        Enabled = Result.Enabled,
        Title = _title.Text.Trim(),
        Executable = _executable.Text.Trim(),
        Arguments = _arguments.Text,
        MatchExtensions = _extensions.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
        SelectionMode = ((_mode.SelectedItem as ComboBoxItem)?.Tag as string) ?? "single",
        RunAsAdmin = _admin.IsChecked == true,
        IsPrimary = _primary.IsChecked == true,
        UseTargetWorkingDirectory = _targetDirectory.IsChecked == true,
        Rule = _rules.ReadRule(),
        Group = _rules.Group,
        Parameters = _parameters.Read(),
        PreserveInputLiterals = Result.PreserveInputLiterals && _parameters.Read().Count == 0
    };
    internal void ApplyTemplate(string id)
    {
        var template = CustomActionTemplates.All.First(x => x.Id == id); var action = template.Create();
        _templates.SelectedItem = _templates.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == id);
        _previewReady = false;
        _title.Text = action.Title; _executable.Text = action.Executable; _arguments.Text = action.Arguments;
        _extensions.Text = string.Join(", ", action.MatchExtensions); _mode.SelectedIndex = action.SelectionMode switch { "each" => 1, "batch" => 2, _ => 0 };
        _admin.IsChecked = action.RunAsAdmin; _targetDirectory.IsChecked = action.UseTargetWorkingDirectory;
        _parameters.Load(action.Parameters);
        _previewReady = true; RefreshPreview();
    }
    private void RefreshPreview()
    {
        if (!_previewReady) return;
        try
        {
            var action = ReadDraft(); bool disabled = !action.Enabled; action.Enabled = true;
            if (string.IsNullOrWhiteSpace(action.Title)) action.Title = "动作预览";
            string extension = action.MatchExtensions.FirstOrDefault(x => x.StartsWith('.') && x.Length is > 1 and <= 16 && x[1..].All(char.IsAsciiLetterOrDigit)) ?? ".txt";
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BetterCM 示例");
            var context = _previewChoice.SelectedIndex switch
            {
                3 when _recentContext != null => _recentContext,
                2 => new SelectedContext { CurrentDirectory = folder, Items = [new(Path.Combine(folder, "示例 文件夹"), true)] },
                1 => new SelectedContext { CurrentDirectory = folder, Items = [new(Path.Combine(folder, "报告 2026" + extension), false), new(Path.Combine(folder, "项目 {dir}" + extension), false)] },
                _ => new SelectedContext { CurrentDirectory = folder, Items = [new(Path.Combine(folder, "报告 2026" + extension), false)] }
            };
            _preview.Text = (disabled ? "当前动作已停用；以下为启用后的参数预览。\n" : "") + new CustomActionPlan(action, context).Describe();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException)
        { _preview.Text = "暂不能预览：" + ex.Message; }
    }
    internal string PreviewText => _preview.Text;
    internal string SelectedTemplateTitle => (_templates.SelectedItem as ComboBoxItem)?.Content as string ?? "";
    internal void SelectPreview(int index) => _previewChoice.SelectedIndex = index;
    internal void ApplySelectedExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _executable.Text = path;
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Length > 0) _title.Text = "使用 " + name + " 打开";
        }
    }
    internal string TitleText => _title.Text;
    internal string ExecutableText => _executable.Text;
}
