using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Views;

public sealed class CustomActionWindow : Window
{
    private readonly TextBox _title = new(), _executable = new(), _arguments = new(), _extensions = new();
    private readonly ComboBox _mode = new();
    private readonly CheckBox _admin = new() { Content = "以管理员身份运行" }, _primary = new() { Content = "直接显示在菜单第一层" };
    private readonly CheckBox _targetDirectory = new() { Content = "使用目标文件夹作为工作目录" };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    public CustomActionItem Result { get; private set; }
    public CustomActionWindow(CustomActionItem original)
    {
        Result = JsonSerializer.Deserialize<CustomActionItem>(JsonSerializer.Serialize(original))!;
        Style = (Style)FindResource(typeof(Window));
        ThemeService.Attach(this);
        Title = original.Title.Length == 0 ? "添加自定义动作" : "编辑自定义动作";
        Width = 640; Height = Math.Min(740, SystemParameters.WorkArea.Height); MinWidth = 560; MinHeight = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
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
        Hint("选择要打开的程序，再告诉它如何接收选中的文件。添加后仍需在设置页保存。" );
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
        Field("菜单中显示的名称", _title); Field("打开程序", _executable);
        Hint("填写 .exe 或 .com 的完整路径，或 PATH 中的程序名称（保存时固定为完整路径）。VS Code 可填写 Code.exe。" );
        Field("启动参数（可选）", _arguments);
        Hint("{path} 表示选中项目的路径，{dir} 表示所在文件夹，{name} 表示名称。\n例如：\"{path}\"。路径占位符请保留双引号，避免空格被拆开。" );
        Field("适用的文件类型", _extensions);
        Hint("用逗号分隔，例如 .txt, .md。* 表示所有文件，dir 表示文件夹。" );
        Field("选中多个项目时", _mode);
        Hint("选择“一次处理所有选中项目”时，在参数中用独立的 {paths} 传入全部路径。" );
        panel.Children.Add(_targetDirectory);
        Hint("默认在程序所在目录运行。只有程序确实需要时才勾选此项；目标文件夹中的 DLL 或插件可能被该程序加载。{dir} 参数仍始终表示目标所在目录。");
        panel.Children.Add(_admin); panel.Children.Add(_primary);
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
                Result.Title = _title.Text.Trim(); Result.Executable = ExecutableResolver.Require(_executable.Text.Trim()); Result.Arguments = _arguments.Text;
                Result.MatchExtensions = _extensions.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                Result.SelectionMode = ((ComboBoxItem)_mode.SelectedItem).Tag.ToString()!; Result.RunAsAdmin = _admin.IsChecked == true; Result.IsPrimary = _primary.IsChecked == true;
                Result.UseTargetWorkingDirectory = _targetDirectory.IsChecked == true;
                ConfigStore.Validate(new() { CustomActions = [Result] }); ActionExecutor.ParseArguments(Result.Arguments);
                DialogResult = true;
            }
            catch (Exception ex) { _error.Text = ex.Message; _error.Visibility = Visibility.Visible; }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save);
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var footer = new StackPanel();
        footer.Children.Add(_error); footer.Children.Add(buttons);
        Grid.SetRow(footer, 1); layout.Children.Add(footer);
        Content = layout;
    }
}
