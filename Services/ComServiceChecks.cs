using System.Runtime.InteropServices;
namespace BetterCM.Services;
internal static class ComServiceChecks
{
    internal static async Task RunAsync(Action<CheckIdentity, bool, string?> check)
    {
        nint desktop = NativeMethods.FindWindow("Progman", null);
        if (desktop == 0) { check(new CheckIdentity("ComService/C001", "真实 Shell 视图服务可从 RPC apartment 查询"), false, "未找到桌面视图"); return; }
        using var view = ShellViewBinding.Open(desktop, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        int result = await Task.Run(() =>
        {
            int initialized = CoInitializeEx(0, 0);
            try
            {
                var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"); var iid = new Guid("000214E2-0000-0000-C000-000000000046");
                int hr = view.QueryNativeService(ref service, ref iid, out var pointer);
                if (hr >= 0 && pointer == 0) return unchecked((int)0x80004002);
                if (pointer != 0) Marshal.Release(pointer);
                return hr;
            }
            finally { if (initialized >= 0) CoUninitialize(); }
        });
        check(new CheckIdentity("ComService/C001", "真实 Shell 视图服务可从 RPC apartment 查询"), result >= 0, $"HRESULT=0x{result:X8}");
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
