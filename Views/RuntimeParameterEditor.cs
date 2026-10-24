using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class RuntimeParameterEditor : StackPanel
{
    private readonly ObservableCollection<RuntimeParameter> _items = [];
    internal event Action? Changed;
    internal RuntimeParameterEditor(IEnumerable<RuntimeParameter> parameters)
    {
        var grid = new DataGrid { ItemsSource = _items, AutoGenerateColumns = false, IsReadOnly = true, Height = 155, CanUserSortColumns = false };
        foreach (var column in new[] { ("标识", "Id"), ("名称", "Title"), ("默认值", "DefaultValue") }) grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Children.Add(grid);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; Children.Add(buttons);
        void Button(string title, Action action) { var button = new Button { Content = title, Margin = new Thickness(0, 0, 8, 0) }; button.Click += (_, _) => action(); buttons.Children.Add(button); }
        Button("添加参数", () => Edit(null)); Button("编辑", () => { if (grid.SelectedItem is RuntimeParameter parameter) Edit(parameter); });
        Button("移除", () => { if (grid.SelectedItem is RuntimeParameter parameter) { _items.Remove(parameter); Changed?.Invoke(); } });
        Children.Add(new TextBlock { Text = "在启动参数中使用 {input:标识}，例如 \"{input:width}\"。执行前填写并确认实际参数。支持文本、整数和下拉选项。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        Load(parameters);
    }
    internal void Load(IEnumerable<RuntimeParameter> parameters) { _items.Clear(); foreach (var parameter in parameters) _items.Add(parameter.Clone()); Changed?.Invoke(); }
    internal List<RuntimeParameter> Read() => _items.Select(x => x.Clone()).ToList();
    private void Edit(RuntimeParameter? original)
    {
        var value = original?.Clone() ?? new RuntimeParameter { Id = "value" + (_items.Count + 1) };
        var window = new Window { Title = "运行时参数", Owner = Window.GetWindow(this), Width = 520, Height = 650, MinWidth = 400, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Style = (Style)FindResource(typeof(Window)) };
        ThemeService.Attach(window); SettingsWindowSizing.Attach(window);
        var root = new DockPanel { Margin = new Thickness(24) }; var panel = new StackPanel();
        TextBox Field(string label, string text)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 6) }); var box = new TextBox { Text = text };
            System.Windows.Automation.AutomationProperties.SetName(box, label); panel.Children.Add(box); return box;
        }
        var id = Field("参数标识（小写英文、数字、下划线，英文开头）", value.Id); var title = Field("显示名称", value.Title);
        panel.Children.Add(new TextBlock { Text = "类型", Margin = new Thickness(0, 10, 0, 6) }); var kind = new ComboBox();
        foreach (var item in new[] { ("文本", "text"), ("整数", "number"), ("下拉选项", "choice") }) kind.Items.Add(new ComboBoxItem { Content = item.Item1, Tag = item.Item2 });
        kind.SelectedItem = kind.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == value.Kind); panel.Children.Add(kind);
        var defaultValue = Field("默认值（可留空，运行时填写）", value.DefaultValue);
        var required = new CheckBox { Content = "必填", IsChecked = value.Required, Margin = new Thickness(0, 12, 0, 0) }; panel.Children.Add(required);
        var min = Field("最小整数", value.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)); var max = Field("最大整数", value.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var options = Field("下拉选项（每行一项）", string.Join(Environment.NewLine, value.Choices)); options.AcceptsReturn = true; options.Height = 100; options.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }; footer.Children.Add(error);
        var save = new Button { Content = "应用参数", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; footer.Children.Add(save);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.Content = root;
        save.Click += (_, _) =>
        {
            try
            {
                if (!int.TryParse(min.Text, out int minimum) || !int.TryParse(max.Text, out int maximum)) throw new InvalidDataException("范围需要整数");
                value.Id = id.Text.Trim(); value.Title = title.Text.Trim(); value.Kind = (string)((ComboBoxItem)kind.SelectedItem).Tag; value.DefaultValue = defaultValue.Text; value.Required = required.IsChecked == true; value.Minimum = minimum; value.Maximum = maximum;
                value.Choices = options.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToList();
                var list = Read(); if (original == null) list.Add(value); else list[_items.IndexOf(original)] = value;
                RuntimeInputs.Validate(new() { Parameters = list, Arguments = "" });
                if (original == null) _items.Add(value); else _items[_items.IndexOf(original)] = value;
                Changed?.Invoke(); window.DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        window.ShowDialog();
    }
}
