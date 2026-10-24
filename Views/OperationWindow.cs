using System.Windows;
using System.Windows.Controls;
namespace BetterCM.Views;

public sealed class OperationWindow : Window
{
    private readonly ProgressBar _progress = new() { Height = 6, Maximum = 1 };
    private readonly TextBlock _text = new() { Margin = new Thickness(0, 10, 0, 12) };
    private readonly Button _cancel = new() { Content = "取消", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
    private bool _cancellable = true;
    public OperationWindow(CancellationTokenSource cancellation, string title = "正在执行操作", string? subtitle = null)
    {
        Style = (Style)FindResource(typeof(Window));
        BetterCM.Services.ThemeService.Attach(this);
        Title = title; Width = 400; Height = 190; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = subtitle ?? title, FontSize = 16, TextTrimming = TextTrimming.CharacterEllipsis });
        panel.Children.Add(_text); panel.Children.Add(_progress);
        _cancel.Click += (_, _) => cancellation.Cancel(); panel.Children.Add(_cancel); Content = panel;
        Closing += (_, _) => { if (_cancellable) cancellation.Cancel(); };
    }
    public void SetCancellable(bool value) { _cancellable = value; _cancel.IsEnabled = value; }
    public void Report(double value, string text) { _progress.Value = value; _text.Text = text; }
}
