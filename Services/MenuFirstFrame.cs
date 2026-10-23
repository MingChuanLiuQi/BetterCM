using System.Windows;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
namespace BetterCM.Services;

// A reused layered child can still contain the previous bitmap when its parent is shown.
// Keep the animated parent cloaked until WPF has painted the prepared opening frame.
internal sealed class MenuFirstFrame
{
    private nint _handle;
    private EventHandler? _render;
    private DispatcherTimer? _fallback;
    private int _serial;
    private readonly Stopwatch _clock = new();
    internal bool Pending { get; private set; }
    internal double ReadyDelayMs { get; private set; }
    internal bool Hold(nint handle)
    {
        Cancel(); ReadyDelayMs = 0;
        int cloak = 1;
        if (NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_CLOAK, ref cloak, sizeof(int)) < 0) return false;
        _handle = handle; Pending = true; _clock.Restart(); return true;
    }
    internal void Present(Dispatcher dispatcher, Action begin)
    {
        int serial = _serial;
        void Complete()
        {
            if (!Pending || serial != _serial) return;
            Detach();
            NativeMethods.DwmFlush(); // Wait for the prepared child bitmap to reach composition.
            int cloak = 0; NativeMethods.DwmSetWindowAttribute(_handle, NativeMethods.DWMWA_CLOAK, ref cloak, sizeof(int));
            Pending = false; _handle = 0; ReadyDelayMs = _clock.Elapsed.TotalMilliseconds;
            begin();
        }
        _render = (_, _) =>
        {
            if (_render != null) CompositionTarget.Rendering -= _render;
            _render = null;
            dispatcher.BeginInvoke(Complete, DispatcherPriority.Background);
        };
        CompositionTarget.Rendering += _render;
        _fallback = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(120) };
        _fallback.Tick += (_, _) => Complete(); _fallback.Start();
    }
    internal void Cancel()
    {
        _serial++; Detach();
        if (_handle != 0) { int cloak = 0; NativeMethods.DwmSetWindowAttribute(_handle, NativeMethods.DWMWA_CLOAK, ref cloak, sizeof(int)); }
        _handle = 0; Pending = false;
    }
    private void Detach()
    {
        if (_render != null) CompositionTarget.Rendering -= _render;
        _render = null; _fallback?.Stop(); _fallback = null;
    }
}
