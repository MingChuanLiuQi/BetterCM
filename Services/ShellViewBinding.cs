using System.Runtime.InteropServices;
namespace BetterCM.Services;

// Bind commands to Explorer's existing view, including the virtual desktop namespace.
// Interface slots follow the SDK IShellBrowser/IShellView/IFolderView definitions.
public sealed class ShellViewBinding : IDisposable
{
    private nint _view, _provider;
    private ComServiceReference? _services;
    private readonly string? _expectedPath;
    private readonly bool _desktop;
    private readonly nint _window, _viewWindow;
    private object? _document;
    private string[]? _selection;
    private const int ShellBrowserActiveViewSlot = 15, ShellViewRefreshSlot = 8, ShellViewItemObjectSlot = 15;
    private const int FolderViewGetFolderSlot = 5, PersistFolderGetCurrentSlot = 5;
    private static readonly Guid ServiceProviderId = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid TopLevelBrowserService = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid ShellBrowserId = new("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid FolderViewId = new("CDE725B0-CCC9-4519-917E-325D72FAB4CE");
    private static readonly Guid PersistFolderId = new("1AC3D9F0-175C-11D1-95BE-00609797EA4F");
    private static readonly Guid DispatchId = new("00020400-0000-0000-C000-000000000046");
    private ShellViewBinding(nint view, string? path, bool desktop, nint window, nint viewWindow) { _view = view; _expectedPath = path; _desktop = desktop; _window = window; _viewWindow = viewWindow; }
    internal static ShellViewBinding ForCapture(nint window) => OpenCore(window, null, null);
    public static ShellViewBinding Open(nint window, string expectedPath, string[]? expectedSelection = null)
        => OpenCore(window, expectedPath, expectedSelection);
    private static ShellViewBinding OpenCore(nint window, string? expectedPath, string[]? expectedSelection)
    {
        var kind = NativeMethods.ClassName(window);
        bool desktop = kind is "Progman" or "WorkerW";
        if (!desktop && kind != "CabinetWClass") throw new InvalidOperationException("没有可用的资源管理器视图");
        var owned = new List<object>();
        object Own(object value) { owned.Add(value); return value; }
        nint view = 0;
        try
        {
            dynamic shell = Own(Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!);
            dynamic windows = Own(shell.Windows());
            var candidates = new List<object>();
            if (desktop)
            {
                object location = 0, root = 0; int handle;
                candidates.Add(Own(windows.FindWindowSW(ref location, ref root, NativeMethods.SWC_DESKTOP, out handle, NativeMethods.SWFO_NEEDDISPATCH)));
            }
            else
                for (int i = 0; i < (int)windows.Count; i++)
                {
                    dynamic item = Own(windows.Item(i));
                    nint itemWindow = (nint)(long)item.HWND;
                    if (itemWindow == window || NativeMethods.GetAncestor(itemWindow, 2) == window) candidates.Add((object)item);
                }
            ShellViewBinding? binding = null;
            foreach (var candidate in candidates)
            {
                nint unknown = Marshal.GetIUnknownForObject(candidate), provider = 0, shellBrowser = 0;
                try
                {
                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in ServiceProviderId, out provider));
                    Marshal.ThrowExceptionForHR(Method<QueryService>(provider, 3)(provider, in TopLevelBrowserService, in ShellBrowserId, out shellBrowser));
                    Marshal.ThrowExceptionForHR(Method<GetInterface>(shellBrowser, ShellBrowserActiveViewSlot)(shellBrowser, out view));
                    Marshal.ThrowExceptionForHR(Method<GetInterface>(view, 3)(view, out var viewWindow));
                    // Inactive tabs can retain WS_VISIBLE. Resolve the frontmost child
                    // inside this frame, even when another application obscures it.
                    if (!IsActiveView(window, viewWindow, desktop)) continue;
                    binding = new(view, expectedPath, desktop, window, viewWindow) { _provider = provider, _services = new(provider) }; view = 0; provider = 0;
                    break;
                }
                catch (COMException) { /* A closed/hidden ShellWindows entry is not the active view. */ }
                finally { if (view != 0) { Marshal.Release(view); view = 0; } if (shellBrowser != 0) Marshal.Release(shellBrowser); if (provider != 0) Marshal.Release(provider); Marshal.Release(unknown); }
            }
            if (binding == null) throw new InvalidOperationException("没有可用的资源管理器视图");
            try
            {
                if (expectedSelection != null) { binding._document = binding.GetDocument(); binding._selection = expectedSelection; }
                binding.ValidateLocation(); return binding;
            }
            catch { binding.Dispose(); throw; }
        }
        finally { if (view != 0) Marshal.Release(view); for (int i = owned.Count - 1; i >= 0; i--) if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]); }
    }
    internal object GetDocument()
    {
        ValidateActiveView();
        Marshal.ThrowExceptionForHR(Method<GetItemObject>(_view, ShellViewItemObjectSlot)(_view, NativeMethods.SVGIO_BACKGROUND, in DispatchId, out var document));
        try { return Marshal.GetObjectForIUnknown(document); } finally { Marshal.Release(document); }
    }
    private void ValidateActiveView()
    {
        if (_view == 0) throw new ObjectDisposedException(nameof(ShellViewBinding));
        if (!IsActiveView(_window, _viewWindow, _desktop))
            throw new InvalidOperationException("资源管理器位置或标签已变化，请重新呼出菜单");
        nint browser = 0, active = 0;
        try
        {
            Marshal.ThrowExceptionForHR(Method<QueryService>(_provider, 3)(_provider, in TopLevelBrowserService, in ShellBrowserId, out browser));
            Marshal.ThrowExceptionForHR(Method<GetInterface>(browser, ShellBrowserActiveViewSlot)(browser, out active));
            Marshal.ThrowExceptionForHR(Method<GetInterface>(active, 3)(active, out var activeWindow));
            if (activeWindow != _viewWindow) throw new InvalidOperationException("资源管理器位置或标签已变化，请重新呼出菜单");
        }
        finally { if (active != 0) Marshal.Release(active); if (browser != 0) Marshal.Release(browser); }
    }
    private static bool IsActiveView(nint window, nint viewWindow, bool desktop)
    {
        if (!NativeMethods.IsWindowVisible(viewWindow)) return false;
        if (desktop) return true;
        if (NativeMethods.GetAncestor(viewWindow, 2) != window || !NativeMethods.GetWindowRect(viewWindow, out var bounds)) return false;
        return IsViewHit(bounds, screen =>
        {
            nint hit = window;
            for (int depth = 0; depth < 32; depth++)
            {
                var point = screen;
                if (!NativeMethods.ScreenToClient(hit, ref point)) return false;
                var child = NativeMethods.ChildWindowFromPointEx(hit, point, NativeMethods.CWP_SKIPINVISIBLE | NativeMethods.CWP_SKIPTRANSPARENT);
                if (child == 0 || child == hit) break;
                hit = child;
            }
            return hit == viewWindow || NativeMethods.IsChild(viewWindow, hit);
        });
    }
    internal static bool IsViewHit(NativeMethods.RECT bounds, Func<NativeMethods.POINT, bool> hitsView)
    {
        int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0) return false;
        // Sample center first, then lower quadrants so an upper address/search flyout
        // cannot mask the active tab while an inactive tab remains covered everywhere.
        foreach (var (nx, ny) in new[] { (2, 2), (3, 3), (1, 3) })
            if (hitsView(new() { X = bounds.Left + width * nx / 4, Y = bounds.Top + height * ny / 4 })) return true;
        return false;
    }
    public nint GetBackgroundContext(Guid interfaceId)
    {
        ValidateLocation();
        Marshal.ThrowExceptionForHR(Method<GetItemObject>(_view, ShellViewItemObjectSlot)(_view, NativeMethods.SVGIO_BACKGROUND, in interfaceId, out var context));
        return context;
    }
    public void Refresh() { ValidateLocation(); Marshal.ThrowExceptionForHR(Method<RefreshView>(_view, ShellViewRefreshSlot)(_view)); }
    public void BeginRename(string path)
    {
        ValidateLocation();
        Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var pidl, 0, out _));
        try
        {
            // SVSI_EDIT | SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED.
            // IShellView owns inline editing; no command line, simulated keys or filesystem rename.
            SelectCreatedItem(pidl, true);
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
    }
    public void SelectCreatedItem(nint absolutePidl, bool edit)
    {
        ValidateLocation();
        Marshal.ThrowExceptionForHR(Method<ActivateView>(_view, 7)(_view, 2)); // IShellView.UIActivate / SVUIA_ACTIVATE_FOCUS.
        Marshal.ThrowExceptionForHR(Method<SelectItem>(_view, 14)(_view, ILFindLastID(absolutePidl), edit ? 0x1Fu : 0x1Du));
    }
    internal int QueryNativeService(ref Guid service, ref Guid iid, out nint result)
    {
        var services = Volatile.Read(ref _services);
        if (services != null) return services.QueryService(ref service, ref iid, out result);
        result = 0; return unchecked((int)0x800401FD); // CO_E_OBJNOTCONNECTED after view disposal.
    }
    public void ValidateLocation()
    {
        ValidateActiveView();
        if (_selection != null && _document != null)
        {
            dynamic document = _document;
            dynamic items = document.SelectedItems();
            try
            {
                var paths = new List<string>();
                for (int i = 0; i < (int)items.Count; i++)
                {
                    dynamic item = items.Item(i);
                    try { paths.Add((string)item.Path); } finally { Marshal.ReleaseComObject(item); }
                }
                if (!paths.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(_selection.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("资源管理器选区已变化，请重新呼出菜单");
            }
            finally { Marshal.ReleaseComObject(items); }
        }
        // Desktop is the namespace root, not the physical Desktop directory captured for path actions.
        if (_desktop || _expectedPath == null) return;
        nint folderView = 0, folder = 0, pidl = 0, name = 0;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(_view, in FolderViewId, out folderView));
            Marshal.ThrowExceptionForHR(Method<GetFolder>(folderView, FolderViewGetFolderSlot)(folderView, in PersistFolderId, out folder));
            Marshal.ThrowExceptionForHR(Method<GetInterface>(folder, PersistFolderGetCurrentSlot)(folder, out pidl));
            Marshal.ThrowExceptionForHR(SHGetNameFromIDList(pidl, NativeMethods.SIGDN_DESKTOPABSOLUTEPARSING, out name));
            string current = Marshal.PtrToStringUni(name) ?? "";
            if (!string.Equals(current.TrimEnd('\\'), _expectedPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("资源管理器位置或标签已变化，请重新呼出菜单");
        }
        finally { if (name != 0) Marshal.FreeCoTaskMem(name); if (pidl != 0) Marshal.FreeCoTaskMem(pidl); if (folder != 0) Marshal.Release(folder); if (folderView != 0) Marshal.Release(folderView); }
    }
    public void Dispose() { _services?.Dispose(); _services = null; if (_document != null) { Marshal.ReleaseComObject(_document); _document = null; } if (_provider != 0) { Marshal.Release(_provider); _provider = 0; } if (_view != 0) { Marshal.Release(_view); _view = 0; } }
    private static T Method<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryService(nint self, in Guid service, in Guid iid, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetInterface(nint self, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetItemObject(nint self, uint item, in Guid iid, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetFolder(nint self, in Guid iid, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int RefreshView(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SelectItem(nint self, nint child, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ActivateView(nint self, uint state);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint bind, out nint pidl, uint attr, out uint attrs);
    [DllImport("shell32.dll")] private static extern nint ILFindLastID(nint pidl);
    [DllImport("shell32.dll")] private static extern int SHGetNameFromIDList(nint pidl, uint kind, out nint name);
}
