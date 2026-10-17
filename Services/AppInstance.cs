using System.Diagnostics;

namespace BetterCM.Services;

internal static class AppInstance
{
    internal static bool IsHelper(IReadOnlyList<string> args) => args.Count > 0 && args[0] is "--shell-host" or "--shell-test-host" or "--context-host" or "--context-test-host";
    // The running GUI owns this mutex until its OnExit cleanup is complete.
    // Missing events during early startup are retried, without killing processes.
    internal static async Task<bool> StopAsync(string name, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (!Mutex.TryOpenExisting(@"Local\" + name + ".App", out var instance)) return true;
            instance.Dispose();
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\" + name + ".Exit"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            await Task.Delay(50);
        }
        return false;
    }
    internal static async Task WakeAsync(string name)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try { using var wake = EventWaitHandle.OpenExisting(@"Local\" + name + ".Wake"); wake.Set(); return; }
            catch (WaitHandleCannotBeOpenedException) { if (attempt < 19) await Task.Delay(50); }
        }
    }
}
