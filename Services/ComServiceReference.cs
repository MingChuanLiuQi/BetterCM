using System.Runtime.InteropServices;
namespace BetterCM.Services;

// The GIT cookie is process-wide, but its COM interface must be obtained in the caller's
// apartment. A managed RCW cached on the view STA can fail when an RPC callback uses it.
internal sealed class ComServiceReference : IDisposable
{
    private uint _cookie;
    private readonly Guid _interfaceId;
    private static readonly Guid ProviderId = typeof(IMenuServiceProvider).GUID;
    private static readonly Guid TableClass = new("00000323-0000-0000-C000-000000000046");
    private static readonly Guid TableId = new("00000146-0000-0000-C000-000000000046");
    private const int Disconnected = unchecked((int)0x800401FD);
    public ComServiceReference(nint provider, Guid? interfaceId = null)
    {
        _interfaceId = interfaceId ?? ProviderId;
        var table = OpenTable();
        try { Marshal.ThrowExceptionForHR(Method<Register>(table, 3)(table, provider, in _interfaceId, out _cookie)); }
        finally { Marshal.Release(table); }
    }
    private nint GetProxy()
    {
        uint cookie = Volatile.Read(ref _cookie);
        if (cookie == 0) throw new COMException("Shell 服务已断开", Disconnected);
        var table = OpenTable();
        try
        {
            Marshal.ThrowExceptionForHR(Method<Get>(table, 5)(table, cookie, in _interfaceId, out var proxy));
            return proxy;
        }
        finally { Marshal.Release(table); }
    }
    public int QueryService(ref Guid service, ref Guid iid, out nint result)
    {
        result = 0; nint proxy = 0, provider = 0;
        try
        {
            proxy = GetProxy();
            int hr = Marshal.QueryInterface(proxy, in ProviderId, out provider);
            return hr < 0 ? hr : Method<Query>(provider, 3)(provider, ref service, ref iid, out result);
        }
        catch (COMException ex) { return ex.HResult; }
        finally { if (provider != 0) Marshal.Release(provider); if (proxy != 0) Marshal.Release(proxy); }
    }
    internal object GetObject()
    {
        var proxy = GetProxy();
        try { return Marshal.GetObjectForIUnknown(proxy); } finally { Marshal.Release(proxy); }
    }
    public void Dispose()
    {
        uint cookie = Interlocked.Exchange(ref _cookie, 0);
        if (cookie == 0) return;
        var table = OpenTable();
        try { Marshal.ThrowExceptionForHR(Method<Revoke>(table, 4)(table, cookie)); }
        finally { Marshal.Release(table); }
    }
    private static nint OpenTable()
    {
        Marshal.ThrowExceptionForHR(CoCreateInstance(in TableClass, 0, 1, in TableId, out var table));
        return table;
    }
    private static T Method<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Register(nint self, nint instance, in Guid iid, out uint cookie);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Get(nint self, uint cookie, in Guid iid, out nint instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Revoke(nint self, uint cookie);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Query(nint self, ref Guid service, ref Guid iid, out nint result);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
}
