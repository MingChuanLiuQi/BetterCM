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
    public bool KeepsMaterialWithoutFocus => IsActive && _configuration?.IsInputActive == true;
    public bool Apply(nint handle, string material, bool light)
    {
        if (IsActive && _material == material)
        {
            _configuration!.Theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
            _configuration.IsInputActive = true;
            return true;
        }
        Dispose();
        try
        {
            if (material == "mica" ? !MicaController.IsSupported() : !DesktopAcrylicController.IsSupported()) return false;
            EnsureQueue();
            _compositor = new Compositor();
            _target = CreateTarget(_compositor, handle);
            _target.Root = _compositor.CreateContainerVisual();
            _configuration = new() { Theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark, IsInputActive = true };
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            bool active;
            if (material == "mica")
            {
                _mica = new() { Kind = MicaKind.BaseAlt };
                active = _mica.SetTarget(id, _target);
                _mica.SetSystemBackdropConfiguration(_configuration);
            }
            else
            {
                _acrylic = new();
                active = _acrylic.SetTarget(id, _target);
                _acrylic.SetSystemBackdropConfiguration(_configuration);
            }
            if (!active) { Dispose(); return false; }
            _material = material;
            return true;
        }
        catch (Exception ex) { AppLog.Write("menu-backdrop", ex); Dispose(); return false; }
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
    }
    [StructLayout(LayoutKind.Sequential)] private struct DispatcherQueueOptions { public int Size, ThreadType, ApartmentType; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTargetCallback(nint self, nint window, [MarshalAs(UnmanagedType.Bool)] bool topmost, out nint target);
    [DllImport("CoreMessaging.dll")] private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint controller);
}
