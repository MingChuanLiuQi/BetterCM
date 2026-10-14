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
    private readonly CheckBox _admin = new() { Content = "以管理员身份运行" }, _primary = new() { Content = "直接显示在一级菜单" };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    public CustomActionItem Result { get; private set; }
    public CustomActionWindow(CustomActionItem original)
    {
        Result = JsonSerializer.Deserialize<CustomActionItem>(JsonSerializer.Serialize(original))!;
        Style = (Style)FindResource(typeof(Window));
        ThemeService.Attach(this);
        Title = "自定义动作"; Width = 580; Height = 620; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        void Field(string title, UIElement control) { panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 10, 0, 5) }); panel.Children.Add(control); }
        _title.Text = Result.Title; _executable.Text = Result.Executable; _arguments.Text = Result.Arguments;
        _extensions.Text = string.Join(", ", Result.MatchExtensions);
        _mode.Items.Add(new ComboBoxItem { Content = "仅单选可用", Tag = "single" });
        _mode.Items.Add(new ComboBoxItem { Content = "多选时逐个执行", Tag = "each" });
        _mode.Items.Add(new ComboBoxItem { Content = "一次传入全部项目（使用 {paths}）", Tag = "batch" });
        _mode.SelectedIndex = Result.SelectionMode switch { "each" => 1, "batch" => 2, _ => 0 }; _mode.Height = 32;
        _admin.IsChecked = Result.RunAsAdmin; _primary.IsChecked = Result.IsPrimary;
        Field("动作名称", _title); Field("程序路径或命令（例如 code、python.exe）", _executable);
        Field("参数模板", _arguments);
        panel.Children.Add(new TextBlock { Text = "{path} 单个路径 · {dir} 目录 · {name} 名称 · {paths} 全部路径\n带空格的占位符用双引号包围；参数会按独立参数传入。", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 6, 0, 0) });
        Field("匹配扩展名（逗号分隔；* 为全部，dir 为文件夹）", _extensions); Field("多选行为", _mode);
        panel.Children.Add(_admin); panel.Children.Add(_primary); panel.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
        var save = new Button { Content = "保存动作", IsDefault = true };
        save.Click += (_, _) =>
        {
            try
            {
                Result.Title = _title.Text.Trim(); Result.Executable = _executable.Text.Trim(); Result.Arguments = _arguments.Text;
                Result.MatchExtensions = _extensions.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                Result.SelectionMode = ((ComboBoxItem)_mode.SelectedItem).Tag.ToString()!; Result.RunAsAdmin = _admin.IsChecked == true; Result.IsPrimary = _primary.IsChecked == true;
                ConfigStore.Validate(new() { CustomActions = [Result] }); ActionExecutor.ParseArguments(Result.Arguments);
                DialogResult = true;
            }
            catch (Exception ex) { _error.Text = ex.Message; }
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        Content = panel;
    }
}
