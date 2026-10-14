using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BetterCM.Config;
namespace BetterCM.Services;

// Animate the visual layer, never the HWND opacity: layered windows disable the native backdrop.
public sealed class SurfaceAnimator
{
    private readonly FrameworkElement _surface;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translation = new();
    private Storyboard? _active;
    private int _serial;
    public SurfaceAnimator(FrameworkElement surface)
    {
        _surface = surface;
        var transforms = new TransformGroup(); transforms.Children.Add(_scale); transforms.Children.Add(_translation);
        surface.RenderTransform = transforms; surface.RenderTransformOrigin = new Point(.5, 0);
    }
    public void Reset()
    {
        _surface.Opacity = 1; _scale.ScaleX = _scale.ScaleY = 1; _translation.X = _translation.Y = 0;
        _serial++; _active?.Remove(_surface); _active = null;
    }
    internal static bool OpeningAnimated(AppConfig config) => config.EnableAnimations && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast && config.OpenAnimation != "none";
    internal void PrepareOpening(AppConfig config, int horizontalDirection = 0)
    {
        Reset();
        if (!OpeningAnimated(config)) return;
        if (config.OpenAnimation.Contains("fade", StringComparison.Ordinal)) _surface.Opacity = 0;
        if (config.OpenAnimation.Contains("zoom", StringComparison.Ordinal)) _scale.ScaleX = _scale.ScaleY = config.AnimationScalePercent / 100.0;
        if (config.OpenAnimation.Contains("slide", StringComparison.Ordinal))
        {
            if (horizontalDirection == 0) _translation.Y = -config.AnimationDistance;
            else _translation.X = -horizontalDirection * config.AnimationDistance;
        }
    }
    public void Play(AppConfig config, bool opening, Action? completed = null, int horizontalDirection = 0)
    {
        double opacity = _surface.Opacity, scaleX = _scale.ScaleX, scaleY = _scale.ScaleY, translationX = _translation.X, translationY = _translation.Y;
        Reset();
        string style = opening ? config.OpenAnimation : config.CloseAnimation;
        if (!config.EnableAnimations || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast || style == "none") { completed?.Invoke(); return; }
        int serial = _serial;
        if (!opening) { _surface.Opacity = opacity; _scale.ScaleX = scaleX; _scale.ScaleY = scaleY; _translation.X = translationX; _translation.Y = translationY; }
        var storyboard = new Storyboard { FillBehavior = FillBehavior.HoldEnd };
        var duration = TimeSpan.FromMilliseconds(opening ? config.OpenAnimationMs : config.CloseAnimationMs);
        IEasingFunction? easing = config.AnimationEasing switch
        {
            "cubic" => new CubicEase { EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn },
            "quintic" => new QuinticEase { EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn },
            "sine" => new SineEase { EasingMode = EasingMode.EaseInOut }, _ => null
        };
        void Add(string property, double from, double to)
        {
            var animation = new DoubleAnimation(from, to, duration) { EasingFunction = easing };
            Storyboard.SetTarget(animation, _surface); Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }
        if (style.Contains("fade")) Add("Opacity", opening ? 0 : opacity, opening ? 1 : 0);
        if (style.Contains("slide"))
        {
            string axis = horizontalDirection == 0 ? "Y" : "X";
            double distance = horizontalDirection == 0 ? config.AnimationDistance : horizontalDirection * config.AnimationDistance;
            Add("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform." + axis + ")", opening ? -distance : horizontalDirection == 0 ? translationY : translationX, opening ? 0 : distance);
        }
        if (style.Contains("zoom"))
        {
            double factor = config.AnimationScalePercent / 100.0;
            Add("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)", opening ? factor : scaleX, opening ? 1 : factor);
            Add("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)", opening ? factor : scaleY, opening ? 1 : factor);
        }
        // Keep the final animation clock attached until hide/reuse. Removing it at completion
        // invalidated the layered bitmap just as DWM finished presenting the opening frame.
        storyboard.Completed += (_, _) => { if (serial == _serial) completed?.Invoke(); };
        _active = storyboard; storyboard.Begin(_surface, true);
    }
}
