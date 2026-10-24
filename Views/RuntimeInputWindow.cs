using System.Windows;
using System.Windows.Controls;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

internal sealed class RuntimeInputWindow : Window
{
    private readonly Dictionary<string, Func<string>> _read = new(StringComparer.Ordinal);
    private readonly TextBox _preview = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    internal IReadOnlyDictionary<string, string>? Values { get; private set; }
    internal RuntimeInputWindow(CustomActionItem action, SelectedContext context)
    {
        Title = action.Title + " · 运行参数"; Width = 760; Height = 680; MinWidth = 480; MinHeight = 420;
        Style = (Style)FindResource(typeof(Window)); ThemeService.Attach(this); SettingsWindowSizing.Attach(this); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Margin = new Thickness(24) };
        foreach (var size in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new() { Height = size });
        root.Children.Add(new TextBlock { Text = action.Title, FontSize = 22, Margin = new Thickness(0, 0, 0, 12) });
        var fields = new StackPanel();
        var run = new Button { Content = "按此预览运行", IsDefault = true };
        foreach (var parameter in action.Parameters)
        {
            fields.Children.Add(new TextBlock { Text = parameter.Title + (parameter.Required ? " *" : "（可选）"), Margin = new Thickness(0, 8, 0, 6) });
            if (parameter.Kind == "choice")
            {
                var choice = new ComboBox { ItemsSource = parameter.Required ? parameter.Choices : new[] { "" }.Concat(parameter.Choices).ToList(), SelectedItem = parameter.DefaultValue.Length == 0 && parameter.Required ? parameter.Choices[0] : parameter.DefaultValue };
                System.Windows.Automation.AutomationProperties.SetName(choice, parameter.Title); fields.Children.Add(choice); _read.Add(parameter.Id, () => choice.SelectedItem as string ?? ""); choice.SelectionChanged += (_, _) => Refresh();
            }
            else
            {
                var box = new TextBox { Text = parameter.DefaultValue, MaxLength = 1024 };
                System.Windows.Automation.AutomationProperties.SetName(box, parameter.Title); fields.Children.Add(box); _read.Add(parameter.Id, () => box.Text); box.TextChanged += (_, _) => Refresh();
                if (parameter.Kind == "number") fields.Children.Add(new TextBlock { Text = $"整数范围：{parameter.Minimum}–{parameter.Maximum}", Margin = new Thickness(0, 4, 0, 0) });
            }
        }
        var scroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        Grid.SetRow(_preview, 2); _preview.Margin = new Thickness(0, 14, 0, 12); root.Children.Add(_preview);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.Children.Add(new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        footer.Children.Add(run); Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
        void Refresh()
        {
            try { var values = _read.ToDictionary(x => x.Key, x => x.Value(), StringComparer.Ordinal); _preview.Text = new CustomActionPlan(action, context, values, previewInputs: false).Describe(); run.IsEnabled = true; }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException) { _preview.Text = ex.Message; run.IsEnabled = false; }
        }
        run.Click += (_, _) => { try { Values = RuntimeInputs.Values(action, _read.ToDictionary(x => x.Key, x => x.Value(), StringComparer.Ordinal), preview: false); DialogResult = true; } catch (Exception ex) { _preview.Text = ex.Message; } };
        Refresh();
    }
}
