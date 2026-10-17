using System.Windows;
using System.Windows.Interop;

namespace BetterCM.Services;

internal static class SettingsWindowSizing
{
    internal readonly record struct Bounds(double Width, double Height, double MinWidth, double MinHeight);
    internal static Bounds Fit(double workWidth, double workHeight, double width, double height, double minWidth, double minHeight)
    {
        double availableWidth = Math.Max(1, workWidth - 24), availableHeight = Math.Max(1, workHeight - 24);
        return new(Math.Min(width, availableWidth), Math.Min(height, availableHeight), Math.Min(minWidth, availableWidth), Math.Min(minHeight, availableHeight));
    }
    internal static void Attach(Window window)
    {
        double width = window.Width, height = window.Height, minWidth = window.MinWidth, minHeight = window.MinHeight;
        void Apply(double workWidth, double workHeight)
        {
            var bounds = Fit(workWidth, workHeight, width, height, minWidth, minHeight);
            window.MinWidth = bounds.MinWidth; window.MinHeight = bounds.MinHeight;
            window.Width = bounds.Width; window.Height = bounds.Height;
        }
        Apply(SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
            double scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
            Apply((info.rcWork.Right - info.rcWork.Left) / scale, (info.rcWork.Bottom - info.rcWork.Top) / scale);
        };
    }
}
