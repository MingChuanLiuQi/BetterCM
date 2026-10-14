using System.Diagnostics;
using System.Windows.Threading;
namespace BetterCM.Services;

// One dispatcher timer exists only during an eligible hover. New menus and pointer exit cancel it.
public sealed class HoverExpansion
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input);
    private Action? _pending;
    private long _started;
    private int _delayMs;
    public bool IsPending => _timer.IsEnabled;
    public HoverExpansion() => _timer.Tick += (_, _) =>
    {
        double remaining = _delayMs - Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
        if (remaining > 0) { _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, remaining)); return; }
        var action = _pending; Cancel(); action?.Invoke();
    };
    public void Schedule(int delayMs, Action action) { Cancel(); _pending = action; _delayMs = delayMs; _started = Stopwatch.GetTimestamp(); _timer.Interval = TimeSpan.FromMilliseconds(delayMs); _timer.Start(); }
    public void Cancel() { _timer.Stop(); _pending = null; }
}
