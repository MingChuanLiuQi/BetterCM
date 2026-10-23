using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace BetterCM.Services;

public readonly record struct TriggerRequest(nint Window, int X, int Y, bool PointerActivation = false, bool NavigationActivation = false, long ReceivedTimestamp = 0);
public readonly record struct Hotkey(uint Modifiers, uint Key)
{
    public static Hotkey Parse(string text)
    {
        uint mods = NativeMethods.MOD_NOREPEAT;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) throw new ArgumentException("快捷键需要 Ctrl、Alt 或 Shift 修饰键");
        if (parts.Any(string.IsNullOrEmpty)) throw new ArgumentException("快捷键的修饰键和按键不能为空");
        foreach (var part in parts[..^1])
        {
            uint modifier = part.ToLowerInvariant() switch { "alt" => NativeMethods.MOD_ALT, "ctrl" or "control" => NativeMethods.MOD_CONTROL, "shift" => NativeMethods.MOD_SHIFT, _ => throw new ArgumentException("快捷键修饰键无效：" + part) };
            if ((mods & modifier) != 0) throw new ArgumentException("快捷键修饰键不能重复：" + part);
            mods |= modifier;
        }
        var key = parts[^1].ToUpperInvariant();
        uint code = key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]) ? key[0] : key.StartsWith('F') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24 ? (uint)(NativeMethods.VK_F1 + f - 1) : throw new ArgumentException("支持 A-Z、0-9 和 F1-F24");
        return new(mods, code);
    }
}
public sealed class InputService : IDisposable
{
    private readonly HwndSource _receiver;
    private readonly nint _handle;
    private readonly ConcurrentQueue<TriggerRequest> _requests = new();
    private readonly NativeMethods.HookProc _callback;
    private readonly NativeMethods.WinEventProc _windowCallback;
    private MenuObservation? _menu;
    private MenuObservation? _preparing;
    private Thread? _mouseThread;
    private nint _mouseHook;
    private uint _mouseThreadId;
    private volatile bool _enabled;
    private readonly RightClickGesture _rightGesture = new();
    private nint _popupHandle;
    private MenuObservation? _popupFamily;
    private int _acceptedClicks, _nativePopups;
    private uint _explorerProcess;
    public int AcceptedClicks => Volatile.Read(ref _acceptedClicks);
    public int NativePopups => Volatile.Read(ref _nativePopups);
    public event Action<TriggerRequest>? Triggered;
    public event Action<int, MenuDismissReason>? MenuDismissRequested;
    public void WatchPreparation(MenuObservation? preparation) => Volatile.Write(ref _preparing, preparation);
    public void WatchMenu(MenuObservation? menu)
    {
        if (menu != null) { Volatile.Write(ref _popupHandle, menu.Handle); Volatile.Write(ref _popupFamily, menu); }
        Volatile.Write(ref _menu, menu);
    }
    public string Status { get; private set; } = "";
    public bool PrimaryHotkeyAvailable { get; private set; }
    public bool InterceptionActive => _enabled && Volatile.Read(ref _mouseHook) != 0;
    public nint Handle => _handle;
    public InputService()
    {
        _receiver = new HwndSource(new HwndSourceParameters("BetterCM Input") { ParentWindow = NativeMethods.HWND_MESSAGE, Width = 0, Height = 0 });
        _handle = _receiver.Handle;
        _receiver.AddHook(Message);
        _callback = MouseCallback;
        _windowCallback = WindowEvent;
        NativeMethods.GetWindowThreadProcessId(NativeMethods.FindWindow("Progman", null), out _explorerProcess);
    }
    public void Apply(string hotkey, bool replace)
    {
        var key = Hotkey.Parse(hotkey);
        NativeMethods.UnregisterHotKey(Handle, 1); NativeMethods.UnregisterHotKey(Handle, 2);
        var primary = NativeMethods.RegisterHotKey(Handle, 1, key.Modifiers, key.Key);
        PrimaryHotkeyAvailable = primary;
        var fallback = key != Hotkey.Parse("Ctrl+Shift+X") && NativeMethods.RegisterHotKey(Handle, 2, NativeMethods.MOD_NOREPEAT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, 'X');
        _enabled = replace;
        if (_mouseThread != null && Volatile.Read(ref _mouseHook) == 0)
        {
            if (Volatile.Read(ref _mouseThreadId) is var threadId && threadId != 0) NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, 0, 0);
            if (_mouseThread.Join(1000)) { _mouseThread = null; Volatile.Write(ref _mouseThreadId, 0); }
        }
        if (_mouseThread == null) StartMouseThread();
        string fallbackStatus = key == Hotkey.Parse("Ctrl+Shift+X") ? "与主快捷键相同" : fallback ? "可用" : "不可用";
        Status = $"快捷键 {hotkey}：{(primary ? "可用" : "已被占用")}\n备用快捷键 Ctrl+Shift+X：{fallbackStatus}\n右键菜单：{(replace && Volatile.Read(ref _mouseHook) != 0 ? "使用 BetterCM" : replace ? "启用失败，重新保存设置可重试" : "使用 Windows 菜单")}";
    }
    private void StartMouseThread()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mouseThread = new Thread(() =>
        {
            Volatile.Write(ref _mouseThreadId, NativeMethods.GetCurrentThreadId());
            Volatile.Write(ref _mouseHook, NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _callback, NativeMethods.GetModuleHandle(null), 0));
            var eventHooks = new[] { NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_MENUPOPUPSTART, NativeMethods.EVENT_SYSTEM_MOVESIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZESTART }.Select(e => NativeMethods.SetWinEventHook(e, e, 0, _windowCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT)).Where(h => h != 0).ToArray();
            ready.SetResult();
            try { while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0) { NativeMethods.TranslateMessage(ref message); NativeMethods.DispatchMessage(ref message); } }
            finally { foreach (var hook in eventHooks) NativeMethods.UnhookWinEvent(hook); if (Volatile.Read(ref _mouseHook) is var mouseHook && mouseHook != 0) NativeMethods.UnhookWindowsHookEx(mouseHook); Volatile.Write(ref _mouseHook, 0); }
        })
        { IsBackground = true, Name = "BetterCM mouse input" };
        _mouseThread.Start();
        ready.Task.Wait(TimeSpan.FromSeconds(2));
    }
    private nint MouseCallback(int code, nint wp, nint lp)
    {
        if (code < 0) return Next(code, wp, lp);
        int message = (int)wp;
        // Resolve an owned release before any hit testing or popup state changes can fail.
        if (message == NativeMethods.WM_RBUTTONUP && _rightGesture.Owned)
        {
            var request = _rightGesture.End();
            try
            {
                if (request != null)
                {
                    _requests.Enqueue(request.Value with { ReceivedTimestamp = Stopwatch.GetTimestamp() });
                    Interlocked.Increment(ref _acceptedClicks);
                    NativeMethods.PostMessage(Handle, NativeMethods.WM_TRIGGER, 0, 0);
                }
            }
            catch { /* A failed notification must not leak an owned release to Explorer. */ }
            return 1;
        }
        // No COM, disk access, logging or UI calls on the input callback.
        bool owned = false;
        try
        {
            var menu = Volatile.Read(ref _menu);
            var preparing = Volatile.Read(ref _preparing);
            if (preparing != null && MenuDismissPolicy.IsDismissPointerMessage(message)) QueueDismiss(preparing, MenuDismissReason.Pointer);
            if (menu != null && MenuDismissPolicy.IsDismissPointerMessage(message))
            {
                var point = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lp).pt;
                if (!menu.ContainsPoint(point)) QueueDismiss(menu, MenuDismissReason.Pointer);
            }
            if (message == NativeMethods.WM_RBUTTONDOWN)
            {
                _rightGesture.Begin(false);
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lp);
                bool native = !_enabled || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & NativeMethods.KEY_PRESSED_MASK) != 0;
                if (native) return Next(code, wp, lp);
                var child = NativeMethods.WindowFromPoint(data.pt);
                var root = NativeMethods.GetAncestor(child, NativeMethods.GA_ROOT);
                // Keep the HWND after WatchMenu(null): a closing animation is still a visible
                // input surface. Letting down hit it and up hit Explorer creates WM_CONTEXTMENU.
                if (root != 0 && (root == Volatile.Read(ref _popupHandle) || Volatile.Read(ref _popupFamily)?.ContainsHandle(root) == true))
                {
                    _rightGesture.Begin(true); return 1;
                }
                var rootClass = NativeMethods.ClassName(root);
                if (rootClass is "Progman" or "WorkerW")
                {
                    owned = true;
                    _rightGesture.Begin(true, new(root, data.pt.X, data.pt.Y, true));
                    NativeMethods.GetWindowThreadProcessId(root, out _explorerProcess);
                    return 1;
                }
                if (rootClass is "CabinetWClass" or "Progman" or "WorkerW")
                {
                    for (var current = child; current != 0; current = NativeMethods.GetParent(current))
                    {
                        var className = NativeMethods.ClassName(current);
                        if (className == "SHELLDLL_DefView" || rootClass == "CabinetWClass" && className == "SysTreeView32")
                        {
                            owned = true;
                            _rightGesture.Begin(true, new(root, data.pt.X, data.pt.Y, true, className == "SysTreeView32"));
                            NativeMethods.GetWindowThreadProcessId(root, out _explorerProcess);
                            return 1;
                        }
                    }
                }
            }
        }
        catch { /* Input must always continue if interception fails. */ }
        return owned ? 1 : Next(code, wp, lp);
    }
    private nint Next(int code, nint wp, nint lp) => NativeMethods.CallNextHookEx(Volatile.Read(ref _mouseHook), code, wp, lp);
    private void QueueDismiss(MenuObservation menu, MenuDismissReason reason) => NativeMethods.PostMessage(Handle, NativeMethods.WM_DISMISS, menu.Generation, (int)reason);
    private void WindowEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_MENUPOPUPSTART)
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var process);
            if (_explorerProcess != 0 && process == _explorerProcess) Interlocked.Increment(ref _nativePopups);
            return;
        }
        var menu = Volatile.Read(ref _menu);
        var preparing = Volatile.Read(ref _preparing);
        if (preparing != null && eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != 0 && !preparing.ContainsHandle(hwnd) && MenuDismissPolicy.IsNewEvent(preparing.StartedAt, time)) QueueDismiss(preparing, MenuDismissReason.Foreground);
        if (menu != null && hwnd != 0 && !menu.ContainsHandle(hwnd) && objectId == NativeMethods.OBJID_WINDOW && MenuDismissPolicy.IsNewEvent(menu.StartedAt, time) &&
            (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND || !menu.ContainsHandle(NativeMethods.GetForegroundWindow()))) QueueDismiss(menu, MenuDismissReason.Foreground);
    }
    private nint Message(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            NativeMethods.GetCursorPos(out var point);
            Triggered?.Invoke(new(NativeMethods.GetForegroundWindow(), point.X, point.Y, ReceivedTimestamp: Stopwatch.GetTimestamp())); handled = true;
        }
        else if (msg == NativeMethods.WM_TRIGGER)
        {
            TriggerRequest? latest = null;
            while (_requests.TryDequeue(out var request)) latest = request;
            if (latest is { } trigger) Triggered?.Invoke(trigger);
            handled = true;
        }
        else if (msg == NativeMethods.WM_DISMISS) { MenuDismissRequested?.Invoke((int)wp, (MenuDismissReason)(int)lp); handled = true; }
        return 0;
    }
    public void Dispose()
    {
        _enabled = false;
        WatchMenu(null);
        WatchPreparation(null);
        NativeMethods.UnregisterHotKey(Handle, 1); NativeMethods.UnregisterHotKey(Handle, 2);
        if (Volatile.Read(ref _mouseThreadId) is var threadId && threadId != 0) NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, 0, 0);
        _mouseThread?.Join(1000);
        _receiver.Dispose();
    }
}
