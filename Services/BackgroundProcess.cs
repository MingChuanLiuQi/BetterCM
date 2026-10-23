using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace BetterCM.Services;

// GUI helpers never display a window or ask Windows to animate the startup cursor.
internal static class BackgroundProcess
{
    internal const uint NoFeedback = 0x80;
    internal static Process Start(string executable, params string[] arguments)
    {
        // Do not set STARTF_USESHOWWINDOW/SW_HIDE: native property dialogs created later
        // in a Shell host must still be allowed to show normally.
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = NoFeedback };
        var commandLine = new StringBuilder(string.Join(" ", new[] { executable }.Concat(arguments).Select(Quote)));
        if (!CreateProcess(executable, commandLine, 0, 0, false, 0x08000000, 0, null, ref startup, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try { return Process.GetProcessById((int)info.ProcessId); }
        finally { CloseHandle(info.Thread); CloseHandle(info.Process); }
    }
    internal static string Quote(string value)
    {
        var quoted = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            quoted.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); quoted.Append(c); slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }
    internal static uint StartupFlags()
    {
        var info = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() }; GetStartupInfo(ref info); return info.Flags;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public nint Reserved, Desktop, Title;
        public uint X, Y, Width, Height, CharactersX, CharactersY, Fill, Flags;
        public ushort ShowWindow, ReservedSize; public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, nint processSecurity, nint threadSecurity, bool inherit, uint flags, nint environment, string? directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", EntryPoint = "GetStartupInfoW")] private static extern void GetStartupInfo(ref StartupInfo info);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
}
