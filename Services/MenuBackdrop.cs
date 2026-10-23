using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using Windows.System;
using WinRT;
namespace BetterCM.Services;

// Native system material uses an explicit popup policy, independently of foreground activation.
// No screen capture, blur loop, input-queue attachment, or simulated keyboard input.
public sealed class MenuBackdrop : IDisposable
{
    private static DispatcherQueueController? _queue;
    private Compositor? _compositor;
    private DesktopWindowTarget? _target;
    private MicaController? _mica;
    private DesktopAcrylicController? _acrylic;
    private SystemBackdropConfiguration? _configuration;
    private string _material = "";
    public bool IsActive => _mica != null || _acrylic != null;
    public bool KeepsMaterialWithoutFocus => IsActive && (_configuration?.IsInputActive ?? true);
    internal string Material => _material;
    internal bool HostBackdropEnabled { get; private set; }
    internal float EffectiveTintOpacity => _acrylic?.TintOpacity ?? _mica?.TintOpacity ?? 1f;
    internal float EffectiveLuminosityOpacity => _acrylic?.LuminosityOpacity ?? _mica?.LuminosityOpacity ?? 1f;
    public bool Apply(nint handle, string material, bool light)
    {
        // SetTarget does not enable the HWND's host backdrop brush. Every top-level
        // cascade window needs its own flag, including cached windows after a switch.
        // Both "mica" and "acrylic" menu backdrops sample live behind-window content via
        // HostBackdropBrush so local wallpaper graphics and underlying windows blur through.
        int hostBackdrop = 1;
        int hostResult = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_HOSTBACKDROPBRUSH, ref hostBackdrop, sizeof(int));
        if (hostResult < 0) { Dispose(); return false; }
        HostBackdropEnabled = true;
        if (IsActive && _material == material)
        {
            _configuration!.Theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
            _configuration.IsInputActive = true;
            ConfigureAppearance(material, light);
            return true;
        }
        Dispose();
        HostBackdropEnabled = true;
        try
        {
            if (!DesktopAcrylicController.IsSupported() && !MicaController.IsSupported()) return false;
            EnsureQueue();
            _compositor = new Compositor();
            _target = CreateTarget(_compositor, handle);
            _target.Root = _compositor.CreateContainerVisual();
            _configuration = new() { Theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark, IsInputActive = true };
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            bool active;
            if (DesktopAcrylicController.IsSupported())
            {
                _acrylic = new();
                active = _acrylic.SetTarget(id, _target);
                _acrylic.SetSystemBackdropConfiguration(_configuration);
                ConfigureAppearance(material, light);
            }
            else
            {
                _mica = new() { Kind = MicaKind.BaseAlt };
                active = _mica.SetTarget(id, _target);
                _mica.SetSystemBackdropConfiguration(_configuration);
                ConfigureAppearance(material, light);
            }
            if (!active) { Dispose(); return false; }
            _material = material;
            return true;
        }
        catch (Exception ex) { AppLog.Write("menu-backdrop", ex); Dispose(); return false; }
    }
    private void ConfigureAppearance(string material, bool light)
    {
        bool mica = material == "mica";
        var tint = light
            ? Windows.UI.Color.FromArgb(255, 246, 246, 248)
            : Windows.UI.Color.FromArgb(255, 24, 24, 28);
        var fallback = light
            ? Windows.UI.Color.FromArgb(255, 243, 243, 243)
            : Windows.UI.Color.FromArgb(255, 32, 32, 32);
        if (_acrylic != null)
        {
            _acrylic.TintColor = tint;
            _acrylic.TintOpacity = mica ? (light ? 0.42f : 0.36f) : (light ? 0.26f : 0.22f);
            _acrylic.LuminosityOpacity = mica ? (light ? 0.62f : 0.42f) : (light ? 0.48f : 0.28f);
            _acrylic.FallbackColor = fallback;
        }
        if (_mica != null)
        {
            _mica.TintColor = tint;
            _mica.TintOpacity = light ? 0.45f : 0.38f;
            _mica.LuminosityOpacity = light ? 0.68f : 0.50f;
            _mica.FallbackColor = fallback;
        }
    }
    private static void EnsureQueue()
    {
        if (DispatcherQueue.GetForCurrentThread() != null) return;
        var options = new DispatcherQueueOptions { Size = Marshal.SizeOf<DispatcherQueueOptions>(), ThreadType = NativeMethods.DQTYPE_THREAD_CURRENT, ApartmentType = NativeMethods.DQTAT_COM_STA };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out var pointer));
        try { _queue = MarshalInterface<DispatcherQueueController>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
    }
    private static DesktopWindowTarget CreateTarget(Compositor compositor, nint handle)
    {
        nint pointer = MarshalInspectable<Compositor>.FromManaged(compositor), interop = 0, target = 0;
        try
        {
            var iid = new Guid("29E691FA-4567-4DCA-B319-D0F207EB6807");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(pointer, in iid, out interop));
            var create = Marshal.GetDelegateForFunctionPointer<CreateTargetCallback>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(interop), 3 * IntPtr.Size));
            Marshal.ThrowExceptionForHR(create(interop, handle, false, out target));
            return MarshalInterface<DesktopWindowTarget>.FromAbi(target);
        }
        finally { if (target != 0) Marshal.Release(target); if (interop != 0) Marshal.Release(interop); Marshal.Release(pointer); }
    }
    public void Dispose()
    {
        _mica?.Dispose(); _acrylic?.Dispose(); _target?.Dispose(); _compositor?.Dispose();
        _mica = null; _acrylic = null; _target = null; _compositor = null; _configuration = null; _material = "";
        HostBackdropEnabled = false;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DispatcherQueueOptions { public int Size, ThreadType, ApartmentType; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTargetCallback(nint self, nint window, [MarshalAs(UnmanagedType.Bool)] bool topmost, out nint target);
    [DllImport("CoreMessaging.dll")] private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint controller);
}
