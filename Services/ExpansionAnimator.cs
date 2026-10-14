using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BetterCM.Config;
namespace BetterCM.Services;

// A finite rendering subscription resizes only while an expansion is in progress.
// Composer/COM queries never run on a frame. Reopen/close/search cancel this subscription.
public sealed class ExpansionAnimator
{
    private readonly Stopwatch _clock = new();
    private Action<double>? _frame;
    private Action? _completed;
    private Action? _started;
    private DispatcherTimer? _fallback;
    private bool _commitQueued;
    private bool _rendering;
    private int _serial;
    private TimeSpan? _lastRenderTime;
    private double _from, _to;
    private int _duration;
    private string _easing = "cubic";
    public bool Active => _frame != null;
    internal bool UpdatingFrame => _rendering;
    internal int Revision => _serial;
    internal bool Preparing { get; private set; }
    internal double Progress { get; private set; }
    public void Cancel()
    {
        CompositionTarget.Rendering -= Render;
        _serial++; _frame = null; _completed = null; _started = null; _clock.Stop();
        _fallback?.Stop(); _fallback = null; Preparing = false; _commitQueued = false; _lastRenderTime = null;
    }
    public static bool Enabled(AppConfig config, string style) => config.EnableAnimations && config.EnableExpansionAnimations && style != "none" && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    public void Play(double from, double to, int milliseconds, string easing, Action<double> frame, Action completed, Action? started = null)
    {
        Cancel(); _from = from; _to = to; _duration = milliseconds; _easing = easing;
        _frame = frame; _completed = completed; _started = started; _clock.Reset(); Preparing = true; Progress = 0;
        int serial = _serial;
        frame(from);
        if (serial != _serial || !Active) return;
        CompositionTarget.Rendering += Render;
        _fallback = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
        _fallback.Tick += (_, _) => Commit(serial); _fallback.Start();
    }
    private void Commit(int serial)
    {
        if (!Active || !Preparing || serial != _serial) return;
        _fallback?.Stop(); _fallback = null;
        // Rendering fires before WPF submits its layered bitmap. Let that render finish,
        // then wait for composition before revealing any additional parent pixels.
        // Unlike an opening menu, this already visible window must never be cloaked.
        NativeMethods.DwmFlush();
        if (serial != _serial || !Active) return;
        Preparing = false; _clock.Restart();
        var started = _started; _started = null; started?.Invoke();
    }
    public static double Ease(double progress, string easing)
    {
        double t = Math.Clamp(progress, 0, 1);
        return easing switch { "cubic" => 1 - Math.Pow(1 - t, 3), "quintic" => 1 - Math.Pow(1 - t, 5), "sine" => .5 - .5 * Math.Cos(Math.PI * t), _ => t };
    }
    private void Render(object? sender, EventArgs e)
    {
        // Native layered-window synchronization can pump a nested Rendering event.
        // Never start another geometry update before the current frame has completed.
        if (_rendering) return;
        _rendering = true;
        try { RenderFrame(e); } finally { _rendering = false; }
    }
    private void RenderFrame(EventArgs e)
    {
        int serial = _serial;
        if (e is RenderingEventArgs render)
        {
            if (_lastRenderTime == render.RenderingTime) return;
            _lastRenderTime = render.RenderingTime;
        }
        if (Preparing)
        {
            if (!_commitQueued)
            {
                _commitQueued = true;
                Dispatcher.CurrentDispatcher.BeginInvoke(() => Commit(serial), DispatcherPriority.Background);
            }
            return;
        }
        double progress = Math.Min(1, _clock.Elapsed.TotalMilliseconds / _duration);
        Progress = Ease(progress, _easing);
        _frame?.Invoke(_from + (_to - _from) * Progress);
        if (serial != _serial || !Active) return;
        if (progress < 1) return;
        var completed = _completed; Cancel(); completed?.Invoke();
    }
}
