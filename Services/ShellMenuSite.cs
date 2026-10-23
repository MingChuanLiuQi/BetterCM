using System.Runtime.InteropServices;
using System.Windows.Threading;
namespace BetterCM.Services;

// Give the native New extension Explorer's editing service. The extension creates the item;
// its callback supplies that exact PIDL, avoiding filesystem guesses or simulated F2.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ShellMenuSite : IMenuServiceProvider, INativeNewMenuClient, IDisposable
{
    private readonly ShellViewBinding _view;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly object _pendingLock = new();
    private readonly List<Task> _pending = [];
    // One native command per client session: InvokeAsync reserves and transfers its
    // connection. Browsing New twice is not a second invocation on the same site.
    private readonly TaskCompletionSource _editCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ComServiceReference? _previousSite;
    private volatile bool _disposed;
    private int _invoking, _newServiceRequested, _newCallbackCount;
    private ShellMenuSite(ShellViewBinding view) => _view = view;
    internal static ShellMenuSite? Attach(object context, ShellViewBinding view)
    {
        if (context is not IObjectWithMenuSite withSite) return null;
        var site = new ShellMenuSite(view);
        var unknownId = new Guid("00000000-0000-0000-C000-000000000046");
        if (withSite.GetSite(ref unknownId, out var previous) >= 0 && previous != 0)
        {
            try { site._previousSite = new(previous, unknownId); } finally { Marshal.Release(previous); }
        }
        int result = withSite.SetSite(site);
        if (result >= 0) return site;
        site.Dispose(); AppLog.Write("shell-new-site", new COMException("无法绑定原生新建服务", result)); return null;
    }
    public int QueryService(ref Guid service, ref Guid iid, out nint result)
    {
        result = 0;
        if (_disposed) return unchecked((int)0x800401FD); // CO_E_OBJNOTCONNECTED.
        if (service == typeof(INativeNewMenuClient).GUID && iid == typeof(INativeNewMenuClient).GUID)
        {
            if (Volatile.Read(ref _invoking) != 0) Interlocked.Exchange(ref _newServiceRequested, 1);
            result = Marshal.GetComInterfaceForObject(this, typeof(INativeNewMenuClient)); return 0;
        }
        if (_previousSite != null)
        {
            int hr = _previousSite.QueryService(ref service, ref iid, out result);
            if (hr >= 0) return hr;
        }
        return _view.QueryNativeService(ref service, ref iid, out result);
    }
    public int IncludeItems(out uint flags) { flags = 3; return 0; } // NMCII_ITEMS | NMCII_FOLDERS.
    public int SelectAndEditItem(nint item, uint flags)
    {
        if (_disposed) return unchecked((int)0x800401FD);
        Interlocked.Increment(ref _newCallbackCount);
        Trace("callback", flags);
        // Explorer can call the CCW on an RPC worker. Its view pointer belongs to our STA.
        // Copy the borrowed PIDL before returning, then select/edit on that same dispatcher.
        nint copy;
        try { copy = ILClone(item); }
        catch (Exception ex) { AppLog.Write("shell-new-pidl", ex); return ex.HResult; }
        if (copy == 0) return unchecked((int)0x8007000E);
        var operation = _dispatcher.InvokeAsync(() =>
        {
            try { _view.SelectCreatedItem(copy, (flags & 1) != 0); Trace("selected", flags); }
            catch (Exception ex) { AppLog.Write("shell-new-edit", ex); }
            finally { Marshal.FreeCoTaskMem(copy); if ((flags & 1) != 0) _editCompleted.TrySetResult(); }
        }, DispatcherPriority.Input);
        lock (_pendingLock) _pending.Add(operation.Task);
        return 0;
    }
    private static void Trace(string stage, uint flags)
    {
#if QA
        if (Environment.GetEnvironmentVariable("BETTERCM_QA_REPORT") is not { Length: > 0 } path) return;
        try { File.AppendAllText(path + ".new.jsonl", System.Text.Json.JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Stage = stage, Flags = flags, Thread = Environment.CurrentManagedThreadId }) + "\n"); } catch { }
#endif
    }
    internal async Task DrainCallbacksAsync()
    {
        Task[] pending;
        lock (_pendingLock) pending = _pending.ToArray();
        await Task.WhenAll(pending);
    }
    internal async Task WaitForNewCallbackAsync()
    {
        // Ordinary verbs do not need a New callback grace period. Keep this site alive
        // through the subsequent native lifetime drain for callbacks already marshaled.
        await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (Volatile.Read(ref _newServiceRequested) != 0 || Volatile.Read(ref _newCallbackCount) != 0)
            await Task.WhenAny(_editCompleted.Task, Task.Delay(1200));
        await DrainCallbacksAsync();
    }
    internal void BeginInvocation() => Interlocked.Exchange(ref _invoking, 1);
    internal void RestoreSite(object context)
    {
        if (context is not IObjectWithMenuSite withSite) return;
        object? previous = _previousSite?.GetObject();
        try { withSite.SetSite(previous); }
        finally { if (previous != null && Marshal.IsComObject(previous)) Marshal.ReleaseComObject(previous); }
    }
    public void Dispose() { _disposed = true; _previousSite?.Dispose(); _previousSite = null; }
    [DllImport("shell32.dll")] private static extern nint ILClone(nint pidl);
}
[ComVisible(true), Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMenuServiceProvider
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out nint result);
}
[ComVisible(true), Guid("DCB07FDC-3BB5-451C-90BE-966644FED7B0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INativeNewMenuClient
{
    [PreserveSig] int IncludeItems(out uint flags);
    [PreserveSig] int SelectAndEditItem(nint item, uint flags);
}
[ComImport, Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectWithMenuSite
{
    [PreserveSig] int SetSite([MarshalAs(UnmanagedType.IUnknown)] object? site);
    [PreserveSig] int GetSite(ref Guid iid, out nint site);
}
