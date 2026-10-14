using System.Runtime.InteropServices;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class ComServiceReferenceTests
{
    [Fact] public async Task StaRegistrationSupportsRpcApartmentQueriesAndRevocation()
    {
        using var owner = new StaWorker("COM service owner");
        var provider = new TestProvider();
        var reference = await owner.Run(() =>
        {
            var pointer = Marshal.GetComInterfaceForObject(provider, typeof(IMenuServiceProvider));
            try { return new ComServiceReference(pointer); } finally { Marshal.Release(pointer); }
        });
        try
        {
            var hr = await Task.Run(() => Query(reference, true)); Assert.Equal(0, hr);
            using var otherSta = new StaWorker("COM service callback");
            Assert.Equal(0, await otherSta.Run(() => Query(reference, true)));
            Assert.Equal(unchecked((int)0x80004002), await Task.Run(() => Query(reference, false)));
        }
        finally { await owner.Run(() => { reference.Dispose(); return true; }); }
        Assert.Equal(unchecked((int)0x800401FD), await Task.Run(() => Query(reference, true)));
        GC.KeepAlive(provider);
    }
    private static int Query(ComServiceReference reference, bool supported)
    {
        int initialized = CoInitializeEx(0, 0);
        try
        {
            var service = supported ? TestProvider.Service : Guid.NewGuid(); var iid = new Guid("00000000-0000-0000-C000-000000000046");
            int result = reference.QueryService(ref service, ref iid, out var pointer);
            if (pointer != 0) Marshal.Release(pointer);
            return result;
        }
        finally { if (initialized >= 0) CoUninitialize(); }
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class TestProvider : IMenuServiceProvider
{
    public static readonly Guid Service = new("C5FBD76B-5FB4-4A4E-99B6-71438D098659");
    public int QueryService(ref Guid service, ref Guid iid, out nint result)
    {
        result = 0;
        if (service != Service) return unchecked((int)0x80004002);
        result = Marshal.GetIUnknownForObject(this); return 0;
    }
}
