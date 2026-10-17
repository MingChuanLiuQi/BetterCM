using System.Runtime.InteropServices;
using System.Text;
namespace BetterCM.Services;

public static class NativeMethods
{
    public const int WH_MOUSE_LL = 14, GA_ROOT = 2, GWL_STYLE = -16;
    public const int WM_SETFOCUS = 0x0007;
    public const int DWMWA_CLOAK = 13, DWMWA_CLOAKED = 14;
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint eventId, uint flags, nint first, nint second);
    [DllImport("user32.dll")] public static extern nint SetFocus(nint window);
    public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3, DWMWA_USE_HOSTBACKDROPBRUSH = 17, DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_SYSTEMBACKDROP_TYPE = 38;
    public const int DWMWCP_ROUND = 2, DWMSBT_NONE = 1, DWMSBT_TRANSIENTWINDOW = 3, DWMSBT_TABBEDWINDOW = 4;
    public const int DQTYPE_THREAD_CURRENT = 2, DQTAT_COM_STA = 2;
    public const long WS_SYSMENU = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080, WS_EX_APPWINDOW = 0x00040000;
    public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;
    public const uint MIIM_STATE = 0x0001, MIIM_ID = 0x0002, MIIM_SUBMENU = 0x0004, MIIM_STRING = 0x0040, MIIM_FTYPE = 0x0100;
    public const uint MFT_SEPARATOR = 0x0800, MFS_DISABLED = 0x0003, MFS_CHECKED = 8, MFS_DEFAULT = 0x1000, CMF_NORMAL = 0, CMIC_MASK_UNICODE = 0x00004000, GCS_VERBW = 4;
    public const uint SHELL_COMMAND_FIRST = 1, SHELL_COMMAND_LAST = 0x7FFF, SVGIO_BACKGROUND = 0;
    public const uint SVGIO_SELECTION = 1, CMF_CANRENAME = 0x10, CMF_ITEMMENU = 0x80, CMF_SYNCCASCADEMENU = 0x1000, CMIC_MASK_NOASYNC = 0x100;
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint process);
    public const uint WM_INITMENUPOPUP = 0x0117;
    public const int SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1;
    public const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    public const int SW_SHOWNORMAL = 1;
    public const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_MBUTTONDOWN = 0x0207, WM_XBUTTONDOWN = 0x020B, WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E, WM_QUIT = 0x0012;
    public const int VK_SHIFT = 0x10, VK_F1 = 0x70, KEY_PRESSED_MASK = 0x8000, SM_CXDRAG = 68, SM_CYDRAG = 69, MK_RBUTTON = 0x0002;
    public const uint EVENT_SYSTEM_FOREGROUND = 3, EVENT_SYSTEM_MENUPOPUPSTART = 6, EVENT_SYSTEM_MOVESIZESTART = 10, EVENT_SYSTEM_MINIMIZESTART = 22, WINEVENT_OUTOFCONTEXT = 0;
    public const int OBJID_WINDOW = 0;
    public static readonly nint HWND_MESSAGE = new(-3);
    public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPSIBLINGS = 0x04000000, WS_CLIPCHILDREN = 0x02000000;
    public const int GWL_EXSTYLE = -20, WS_EX_LAYERED = 0x00080000;
    public const int WM_HOTKEY = 0x0312, WM_TRIGGER = 0x8001, WM_DISMISS = 0x8002, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;
    public const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 1, SHGFI_USEFILEATTRIBUTES = 0x10, FILE_ATTRIBUTE_DIRECTORY = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint SPI_SETDESKWALLPAPER = 20, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public nuint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct MSG { public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public POINT pt; public uint lPrivate; }
    [StructLayout(LayoutKind.Sequential)] public struct GUITHREADINFO { public uint cbSize, flags; public nint hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public nint hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    public delegate nint HookProc(int code, nint wParam, nint lParam);
    public delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] public static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint FindWindow(string? className, string? title);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out RECT bounds);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] public static extern nint ChildWindowFromPointEx(nint parent, POINT point, uint flags);
    public const uint CWP_SKIPINVISIBLE = 1, CWP_SKIPTRANSPARENT = 4;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] public static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(nint hwnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(nint hwnd, ref POINT point);
    public const int WM_SIZE = 0x0005;
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern nint SendRenderSizeMessage(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    public const int SM_CYMINTRACK = 35;
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern nint DispatchMessage(ref MSG msg);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostThreadMessage(uint thread, uint msg, nint wp, nint lp);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? module);
    [DllImport("user32.dll")] public static extern nint MonitorFromPoint(POINT point, uint flags);
    [DllImport("user32.dll")] public static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern nint SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern nint CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] public static extern nint LocalFree(nint memory);
    [DllImport("imm32.dll")] public static extern nint ImmAssociateContext(nint hwnd, nint context);
    [DllImport("imm32.dll")] public static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll")] public static extern bool ImmReleaseContext(nint hwnd, nint context);
    public static string ClassName(nint hwnd) { var name = new StringBuilder(128); GetClassName(hwnd, name, name.Capacity); return name.ToString(); }
}
