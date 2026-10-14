using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
namespace BetterCM.Views;

// The editable TextBox retains its native IME/composition behavior. This adorner is
// noninteractive and is absent whenever the editor owns keyboard focus.
public sealed class SearchPlaceholder : Adorner
{
    private readonly TextBox _editor;
    public static readonly DependencyProperty HintBrushProperty = DependencyProperty.Register(nameof(HintBrush), typeof(Brush), typeof(SearchPlaceholder), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush HintBrush { get => (Brush)GetValue(HintBrushProperty); set => SetValue(HintBrushProperty, value); }
    public SearchPlaceholder(TextBox editor) : base(editor)
    {
        _editor = editor; IsHitTestVisible = false; SetResourceReference(HintBrushProperty, "MutedBrush");
        editor.TextChanged += (_, _) => Refresh();
        editor.IsKeyboardFocusWithinChanged += (_, _) => Refresh();
        editor.IsVisibleChanged += (_, _) => Refresh();
        TextCompositionManager.AddPreviewTextInputStartHandler(editor, (_, _) => Visibility = Visibility.Collapsed);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(editor, (_, _) => Visibility = Visibility.Collapsed);
        Refresh();
    }
    public void Refresh() => Visibility = _editor.IsVisible && _editor.Text.Length == 0 && !_editor.IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed;
    protected override void OnRender(DrawingContext context)
    {
        var text = new FormattedText("搜索全部动作", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch), 12, HintBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(text, new Point(_editor.Padding.Left + _editor.BorderThickness.Left, Math.Max(0, (_editor.ActualHeight - text.Height) / 2)));
    }
}
