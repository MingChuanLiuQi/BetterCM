using System.Windows;
using System.Windows.Controls;
namespace BetterCM.Views;
public sealed class OperationWindow : Window
{
    private readonly ProgressBar _progress = new() { Height = 6, Maximum = 1 };
    private readonly TextBlock _text = new() { Margin = new Thickness(0, 10, 0, 12) };
    public OperationWindow(CancellationTokenSource cancellation)
    {
        Style = (Style)FindResource(typeof(Window));
        BetterCM.Services.ThemeService.Attach(this);
        Title = "正在计算 SHA256"; Width = 400; Height = 190; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "正在计算 SHA256", FontSize = 16 });
        panel.Children.Add(_text); panel.Children.Add(_progress);
        var cancel = new Button { Content = "取消", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        cancel.Click += (_, _) => cancellation.Cancel(); panel.Children.Add(cancel); Content = panel;
        Closing += (_, _) => cancellation.Cancel();
    }
    public void Report(double value, string text) { _progress.Value = value; _text.Text = text; }
}
