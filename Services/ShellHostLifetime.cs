using System.Runtime.InteropServices;
namespace BetterCM.Services;

// Shell extensions may retain work after InvokeCommand returns. Keep the STA pumping until
// their registered references and visible UI are gone, even after the client disconnects.
internal sealed class ShellHostLifetime : IDisposable
{
    private nint _count = Marshal.AllocHGlobal(sizeof(int));
    private nint _reference;
    private readonly int _baseline;
    public ShellHostLifetime()
    {
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        Marshal.WriteInt32(_count, 0);
        Marshal.ThrowExceptionForHR(SHCreateThreadRef(_count, out _reference));
        Marshal.ThrowExceptionForHR(SHSetThreadRef(_reference));
        SHSetInstanceExplorer(_reference);
        _baseline = Marshal.ReadInt32(_count);
    }
    public async Task DrainAsync()
    {
        // Allow posted property-sheet/file-operation creation to reach this STA first.
        await Task.Delay(600);
        var started = System.Diagnostics.Stopwatch.StartNew();
        bool reported = false;
        while (Marshal.ReadInt32(_count) > _baseline || HasVisibleWindows())
        {
            if (!reported && started.Elapsed >= TimeSpan.FromSeconds(15))
            {
                // A live property dialog/progress operation owns this STA. Never terminate
                // it on an arbitrary timeout; report retained work while continuing to pump.
                AppLog.Write("shell-retained-work", new TimeoutException("原生窗口或扩展引用保留宿主超过 15 秒；等待原生任务结束，不终止任务。"));
                reported = true;
            }
            await Task.Delay(150);
        }
    }
    private static bool HasVisibleWindows()
    {
        bool found = false;
        EnumWindows((window, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out var process);
            if (process == Environment.ProcessId && IsWindowVisible(window)) found = true;
            return true;
        }, 0);
        return found;
    }
    public void Dispose()
    {
        SHSetInstanceExplorer(0); SHSetThreadRef(0);
        if (_reference != 0) { Marshal.Release(_reference); _reference = 0; }
        if (_count != 0) { Marshal.FreeHGlobal(_count); _count = 0; }
        OleUninitialize();
    }
    [DllImport("shlwapi.dll")] private static extern int SHCreateThreadRef(nint count, out nint reference);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("shlwapi.dll")] private static extern int SHSetThreadRef(nint reference);
    [DllImport("shell32.dll")] private static extern void SHSetInstanceExplorer(nint reference);
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
}
