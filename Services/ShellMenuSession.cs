using System.Runtime.InteropServices;
using System.Text;

namespace BetterCM.Services;

public sealed record ShellItem(int Command, string Title, string Group, string Key, string Verb, bool Enabled, string Shortcut = "", bool Checked = false, bool IsDefault = false);
// Preserve HMENU structure separately from the flat command index used by IPC and settings.
public sealed record ShellMenuNode(string Title, int Command = -1, bool Enabled = true, bool IsSeparator = false, bool Checked = false, bool IsDefault = false, string Shortcut = "", IReadOnlyList<ShellMenuNode>? Children = null, string Mnemonic = "", int? DeferredSubmenu = null);
public sealed record ShellBuildTimings(double BindMs, double QueryMs, double ReadMs, double PasteMs);
public sealed class ShellMenuSession : IDisposable
{
    private IContextMenu? _context;
    private nint _menu, _owner;
    private object? _folder;
    private ShellViewBinding? _view;
    private ShellMenuSession? _pasteSession;
    private ShellMenuSite? _site;
    private readonly string[] _paths;
    private readonly bool _background;
    private readonly bool _deferSubmenus;
    private readonly Dictionary<int, DeferredMenu> _deferred = [];
    private readonly Dictionary<nint, int> _submenuHandles = [];
    private readonly List<ShellItem> _commands = [];
    private sealed record DeferredMenu(nint Handle, int Index, string Group, bool Enabled)
    {
        public IReadOnlyList<ShellMenuNode>? Rows { get; set; }
    }
    private const int FolderCommandBase = 0x10000;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<ShellItem> Items { get; private set; } = [];
    public IReadOnlyList<ShellMenuNode> Menu { get; private set; } = [];
    public ShellBuildTimings? BuildTimings { get; private set; }
    public ShellMenuSession(string[] paths, nint owner, bool background = false, string viewPath = "", bool pinnedNavigation = false, bool deferSubmenus = false, bool capabilitiesOnly = false)
    {
        _owner = owner;
        _paths = paths;
        _background = background;
        _deferSubmenus = deferSubmenus;
        if (paths.Length == 0) throw new ArgumentException("没有有效的 Shell 目标");
        var parentPath = Path.GetDirectoryName(paths[0].TrimEnd('\\'));
        if (paths.Length > 1 && paths.Any(x => !string.Equals(Path.GetDirectoryName(x.TrimEnd('\\')), parentPath, StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("跨目录选区暂不支持系统菜单；内置和自定义批量动作仍可使用。");
        var pidls = new List<nint>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            foreach (var path in paths) { Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var pidl, 0, out _)); pidls.Add(pidl); }
            var folderId = typeof(IShellFolder).GUID;
            var contextId = typeof(IContextMenu).GUID;
            if (pinnedNavigation && paths.Length == 1)
            {
                // A pinned navigation item belongs to Home, not to its physical parent.
                // Keep that namespace parent so Unpin and other Home verbs remain native.
                Marshal.ThrowExceptionForHR(SHParseDisplayName("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", 0, out var homePidl, 0, out _));
                pidls.Add(homePidl);
                Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out var desktop));
                try
                {
                    Marshal.ThrowExceptionForHR(desktop.BindToObject(homePidl, 0, ref folderId, out var homeFolder));
                    try { _folder = Marshal.GetObjectForIUnknown(homeFolder); } finally { Marshal.Release(homeFolder); }
                    var child = FindHomeChild((IShellFolder)_folder, homePidl, paths[0]);
                    pidls.Add(child);
                    Marshal.ThrowExceptionForHR(((IShellFolder)_folder).GetUIObjectOf(owner, 1, [child], ref contextId, 0, out var pointer));
                    try { _context = (IContextMenu)Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
                }
                finally { Marshal.ReleaseComObject(desktop); }
            }
            if (!background && owner != 0 && viewPath.Length > 0)
            {
                // Bind to the real selection: Explorer supplies the site needed for rename and namespace verbs.
                // Selection/location mismatches must fail instead of silently running on another target.
                _view = ShellViewBinding.Open(owner, viewPath, paths);
                // Explorer's cross-process selection wrapper does not expose canonical verbs.
                // Use the local folder's IContextMenu below, retaining this exact view for rename/validation.
            }
            if (background && owner != 0)
            {
                try
                {
                    _view = ShellViewBinding.Open(owner, paths[0]);
                    var pointer = _view.GetBackgroundContext(contextId);
                    try { _context = (IContextMenu)Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
                }
                catch (Exception ex) { _view?.Dispose(); _view = null; AppLog.Write("shell-background-view", ex); }
            }
            if (_context == null && background && paths.Length == 1)
            {
                Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out var desktop));
                try
                {
                    Marshal.ThrowExceptionForHR(desktop.BindToObject(pidls[0], 0, ref folderId, out var backgroundFolder));
                    try { _folder = Marshal.GetObjectForIUnknown(backgroundFolder); } finally { Marshal.Release(backgroundFolder); }
                    if (((IShellFolder)_folder).CreateViewObject(owner, ref contextId, out var backgroundContext) >= 0)
                    {
                        try { _context = (IContextMenu)Marshal.GetObjectForIUnknown(backgroundContext); } finally { Marshal.Release(backgroundContext); }
                    }
                    else { Marshal.ReleaseComObject(_folder); _folder = null; }
                }
                finally { Marshal.ReleaseComObject(desktop); }
            }
            if (_context == null)
            {
                Marshal.ThrowExceptionForHR(SHBindToParent(pidls[0], ref folderId, out var folderPointer, out _));
                try { _folder = Marshal.GetObjectForIUnknown(folderPointer); } finally { Marshal.Release(folderPointer); }
                var children = pidls.Select(ILFindLastID).ToArray();
                Marshal.ThrowExceptionForHR(((IShellFolder)_folder).GetUIObjectOf(owner, (uint)children.Length, children, ref contextId, 0, out var pointer));
                try { _context = (IContextMenu)Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
            }
            if (_view != null && background) _site = ShellMenuSite.Attach(_context, _view);
            double bound = watch.Elapsed.TotalMilliseconds;
            _menu = CreatePopupMenu();
            // Packaged Explorer commands can populate the root asynchronously without this
            // flag. Keep every root verb; defer only our WM_INITMENUPOPUP/read traversal.
            uint flags = NativeMethods.CMF_NORMAL | NativeMethods.CMF_SYNCCASCADEMENU;
            if (capabilitiesOnly) flags |= 0x800; // CMF_OPTIMIZEFORINVOKE: only canonical capabilities are required.
            if (!background) flags |= NativeMethods.CMF_ITEMMENU;
            if (!background && _view != null && paths.Length == 1) flags |= NativeMethods.CMF_CANRENAME;
            Marshal.ThrowExceptionForHR(_context.QueryContextMenu(_menu, 0, NativeMethods.SHELL_COMMAND_FIRST, NativeMethods.SHELL_COMMAND_LAST, flags));
            double queried = watch.Elapsed.TotalMilliseconds;
            var items = _commands;
            var nodes = ReadMenu(_menu, "", true, items, new HashSet<nint>());
            double read = watch.Elapsed.TotalMilliseconds;
            if (background && paths.Length == 1 && ShellClipboard.HasTransferData && !items.Any(x => x.Verb.Equals("paste", StringComparison.OrdinalIgnoreCase)))
            {
                // An unsited folder background menu omits paste; Explorer's remote background
                // wrapper can omit its canonical verb. The folder item's native menu supplies
                // the actual paste handler and clipboard-dependent state for the same target.
                try
                {
                    _pasteSession = new(paths, owner, capabilitiesOnly: true);
                    NativePasteMenu.Merge(nodes, items, _pasteSession.Items, _pasteSession.Menu, FolderCommandBase);
                }
                catch (Exception ex) { _pasteSession?.Dispose(); _pasteSession = null; AppLog.Write("shell-folder-paste", ex); }
            }
            Items = items;
            Menu = nodes;
#if QA
            if (Environment.GetEnvironmentVariable("BETTERCM_PERF") == "1") BuildTimings = new(bound, queried - bound, read - queried, watch.Elapsed.TotalMilliseconds - read);
#endif
        }
        catch { Dispose(); throw; }
        finally { foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl); }
    }
    private List<ShellMenuNode> ReadMenu(nint menu, string group, bool parentEnabled, List<ShellItem> output, HashSet<nint> visited, HashSet<string>? knownKeys = null)
    {
        var nodes = new List<ShellMenuNode>();
        if (!visited.Add(menu)) return nodes;
        var keys = knownKeys ?? output.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var buffer = Marshal.AllocHGlobal(2048);
        try
        {
            for (int index = 0, count = GetMenuItemCount(menu); index < count; index++)
            {
                var item = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(), fMask = NativeMethods.MIIM_STATE | NativeMethods.MIIM_ID | NativeMethods.MIIM_SUBMENU | NativeMethods.MIIM_STRING | NativeMethods.MIIM_FTYPE, dwTypeData = buffer, cch = 1023 };
                if (!GetMenuItemInfo(menu, (uint)index, true, ref item)) continue;
                if ((item.fType & NativeMethods.MFT_SEPARATOR) != 0) { nodes.Add(new("", IsSeparator: true)); continue; }
                var rawCaption = Marshal.PtrToStringUni(buffer) ?? "";
                var mnemonic = ReadMnemonic(rawCaption.Split('\t')[0]);
                var caption = rawCaption.Replace("&&", "\0").Replace("&", "").Replace("\0", "&").Split('\t');
                var title = caption[0].Trim();
                var shortcut = caption.Length > 1 ? caption[1].Trim() : "";
                if (title.Length == 0) continue; // Owner-drawn entries without text cannot be represented faithfully.
                var enabled = parentEnabled && (item.fState & NativeMethods.MFS_DISABLED) == 0;
                bool isChecked = (item.fState & NativeMethods.MFS_CHECKED) != 0, isDefault = (item.fState & NativeMethods.MFS_DEFAULT) != 0;
                if (item.hSubMenu != 0)
                {
                    string childGroup = group.Length == 0 ? title : group + " · " + title;
                    if (_deferSubmenus)
                    {
                        if (!_submenuHandles.TryGetValue(item.hSubMenu, out int token))
                        {
                            token = _deferred.Count; _submenuHandles.Add(item.hSubMenu, token);
                            _deferred.Add(token, new(item.hSubMenu, index, childGroup, enabled));
                        }
                        nodes.Add(new(title, Enabled: enabled, Checked: isChecked, IsDefault: isDefault, Shortcut: shortcut, Children: [], Mnemonic: mnemonic, DeferredSubmenu: token));
                    }
                    else
                    {
                        InitializeSubmenu(item.hSubMenu, index);
                        var children = ReadMenu(item.hSubMenu, childGroup, enabled, output, visited, keys);
                        nodes.Add(new(title, Enabled: enabled, Checked: isChecked, IsDefault: isDefault, Shortcut: shortcut, Children: children, Mnemonic: mnemonic));
                    }
                }
                else if (item.wID >= NativeMethods.SHELL_COMMAND_FIRST && item.wID <= NativeMethods.SHELL_COMMAND_LAST)
                {
                    string verb = "";
                    var command = (int)(item.wID - NativeMethods.SHELL_COMMAND_FIRST);
                    try { var builder = new StringBuilder(512); if (_context!.GetCommandString((nuint)command, NativeMethods.GCS_VERBW, 0, builder, 512) >= 0) verb = builder.ToString(); } catch { }
                    // Apply at the shared reader, including deferred submenus/search.
                    // Keep the remaining original command offsets for InvokeCommand.
                    if (UnsupportedShellCommand.IsProtectedPin(verb)) continue;
                    // Include the group: different extensions sometimes share a canonical verb.
                    var key = group + "|" + (verb.Length > 0 ? verb : title);
                    var baseKey = key; int duplicate = 1;
                    while (!keys.Add(key)) key = baseKey + "|#" + ++duplicate;
                    output.Add(new(command, title, group, key, verb, enabled, shortcut, isChecked, isDefault));
                    nodes.Add(new(title, command, enabled, Checked: isChecked, IsDefault: isDefault, Shortcut: shortcut, Mnemonic: mnemonic));
                }
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return nodes;
    }
    private void InitializeSubmenu(nint menu, int index)
    {
        // E_NOTIMPL is valid for handlers with an already populated static submenu.
        if (_context is IContextMenu3 three) three.HandleMenuMsg2(NativeMethods.WM_INITMENUPOPUP, menu, (nint)index, out _);
        else if (_context is IContextMenu2 two) two.HandleMenuMsg(NativeMethods.WM_INITMENUPOPUP, menu, (nint)index);
    }
    public IReadOnlyList<ShellMenuNode> ExpandSubmenu(int token)
    {
        _view?.ValidateLocation();
        if (_context == null || !_deferred.TryGetValue(token, out var submenu)) throw new InvalidOperationException("子菜单上下文已失效");
        if (submenu.Rows != null) return submenu.Rows;
        InitializeSubmenu(submenu.Handle, submenu.Index);
        submenu.Rows = ReadMenu(submenu.Handle, submenu.Group, submenu.Enabled, _commands, []);
        return submenu.Rows;
    }
    public IReadOnlyList<ShellMenuNode> CompleteMenu()
    {
        IReadOnlyList<ShellMenuNode> Complete(IReadOnlyList<ShellMenuNode> nodes, HashSet<int> ancestors)
            => nodes.Select(node =>
            {
                if (node.DeferredSubmenu is int token)
                {
                    if (!ancestors.Add(token)) return node with { Children = [], DeferredSubmenu = null };
                    var rows = Complete(ExpandSubmenu(token), ancestors); ancestors.Remove(token);
                    return node with { Children = rows, DeferredSubmenu = null };
                }
                return node.Children == null ? node : node with { Children = Complete(node.Children, ancestors) };
            }).ToArray();
        return Menu = Complete(Menu, []);
    }
    public static string ReadMnemonic(string caption)
    {
        for (int i = 0; i < caption.Length - 1; i++)
            if (caption[i] == '&')
            {
                if (caption[i + 1] == '&') { i++; continue; }
                return caption[i + 1].ToString().ToUpperInvariant();
            }
        var suffix = System.Text.RegularExpressions.Regex.Match(caption, @"[（(]([A-Za-z0-9])[）)]\s*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        // Explorer's remote context wrapper may already have stripped the ampersand while
        // preserving its printed access-key suffix.
        return suffix.Success ? suffix.Groups[1].Value.ToUpperInvariant() : "";
    }
    public void Invoke(int command)
    {
        _view?.ValidateLocation();
        if (!Items.Any(x => x.Command == command && x.Enabled)) throw new InvalidOperationException("动作已失效或不可用");
        _site?.BeginInvocation();
        if (_owner != 0) NativeMethods.SetForegroundWindow(_owner);
        if (command >= FolderCommandBase && _pasteSession != null) { _pasteSession.Invoke(command - FolderCommandBase); return; }
        if (_view != null && Items.Any(x => x.Command == command && x.Verb.Equals("rename", StringComparison.OrdinalIgnoreCase)))
        {
            if (_paths.Length != 1) throw new InvalidOperationException("请选择一个项目后重命名");
            _view.BeginRename(_paths[0]); return;
        }
        var info = new CMINVOKECOMMANDINFOEX { cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(), fMask = NativeMethods.CMIC_MASK_UNICODE | NativeMethods.CMIC_MASK_NOASYNC, hwnd = _owner, lpVerb = (nint)command, lpVerbW = (nint)command, nShow = NativeMethods.SW_SHOWNORMAL };
        // Background static verbs expand %V using the invocation directory. A selection
        // IDataObject is absent here, so leaving this empty loses their physical target.
        nint directory = 0, directoryW = 0;
        try
        {
            if (_background && _paths.Length == 1 && Directory.Exists(_paths[0]))
            {
                directory = Marshal.StringToCoTaskMemAnsi(_paths[0]);
                directoryW = Marshal.StringToCoTaskMemUni(_paths[0]);
                info.lpDirectory = directory; info.lpDirectoryW = directoryW;
            }
            Marshal.ThrowExceptionForHR(_context!.InvokeCommand(ref info));
        }
        finally { if (directory != 0) Marshal.FreeCoTaskMem(directory); if (directoryW != 0) Marshal.FreeCoTaskMem(directoryW); }
        if (Items.Any(x => x.Command == command && x.Verb is "cut" or "copy"))
        {
            // The Shell command has already succeeded. Clipboard contention while
            // persisting its IDataObject must not turn that success into a failure.
            try { System.Windows.Clipboard.Flush(); }
            catch (Exception ex) { AppLog.Write("clipboard-flush", ex); }
        }
    }
    public void Dispose()
    {
        ReleaseCommands();
        _site?.Dispose(); _site = null;
        _view?.Dispose(); _view = null;
    }
    internal void ReleaseCommands()
    {
        if (_menu != 0) { DestroyMenu(_menu); _menu = 0; }
        if (_context != null)
        {
            try { _site?.RestoreSite(_context); } catch (Exception ex) { AppLog.Write("shell-site-restore", ex); }
            Marshal.ReleaseComObject(_context); _context = null;
        }
        if (_folder != null) { Marshal.ReleaseComObject(_folder); _folder = null; }
        _pasteSession?.Dispose(); _pasteSession = null;
    }
    internal Task DrainCallbacksAsync() => _site?.DrainCallbacksAsync() ?? Task.CompletedTask;
    internal Task WaitForNewCallbackAsync() => _site?.WaitForNewCallbackAsync() ?? Task.CompletedTask;
    private static nint FindHomeChild(IShellFolder folder, nint homePidl, string path)
    {
        Marshal.ThrowExceptionForHR(folder.EnumObjects(0, 0x60 /* SHCONTF_FOLDERS | SHCONTF_NONFOLDERS */, out var pointer));
        IEnumIDList enumerator;
        try { enumerator = (IEnumIDList)Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
        try
        {
            while (enumerator.Next(1, out var child, out var fetched) == 0 && fetched == 1)
            {
                bool matches = false;
                var absolute = ILCombine(homePidl, child); nint name = 0;
                try
                {
                    if (absolute != 0 && SHGetNameFromIDList(absolute, 0x80058000 /* SIGDN_FILESYSPATH */, out name) >= 0)
                        matches = string.Equals(Marshal.PtrToStringUni(name)?.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
                finally { if (name != 0) Marshal.FreeCoTaskMem(name); if (absolute != 0) Marshal.FreeCoTaskMem(absolute); }
                if (matches) return child;
                Marshal.FreeCoTaskMem(child);
            }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
        throw new InvalidOperationException("此固定目录已不在主文件夹中，请重新呼出菜单");
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint bind, out nint pidl, uint attr, out uint attrs);
    [DllImport("shell32.dll")] private static extern int SHBindToParent(nint pidl, ref Guid iid, out nint parent, out nint child);
    [DllImport("shell32.dll")] private static extern nint ILFindLastID(nint pidl);
    [DllImport("shell32.dll")] private static extern nint ILCombine(nint parent, nint child);
    [DllImport("shell32.dll")] private static extern int SHGetNameFromIDList(nint pidl, uint kind, out nint name);
    [DllImport("shell32.dll")] private static extern int SHGetDesktopFolder(out IShellFolder folder);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMenuItemInfo(nint menu, uint item, bool byPosition, ref MENUITEMINFO info);
    [StructLayout(LayoutKind.Sequential)] private struct MENUITEMINFO { public uint cbSize, fMask, fType, fState, wID; public nint hSubMenu, hbmpChecked, hbmpUnchecked; public nuint dwItemData; public nint dwTypeData; public uint cch; public nint hbmpItem; }
    [StructLayout(LayoutKind.Sequential)] public struct CMINVOKECOMMANDINFOEX { public int cbSize; public uint fMask; public nint hwnd, lpVerb, lpParameters, lpDirectory; public int nShow; public uint dwHotKey; public nint hIcon, lpTitle, lpVerbW, lpParametersW, lpDirectoryW, lpTitleW; public NativeMethods.POINT ptInvoke; }
    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(nint hwnd, nint bind, [MarshalAs(UnmanagedType.LPWStr)] string name, ref uint eaten, out nint pidl, ref uint attrs);
        [PreserveSig] int EnumObjects(nint hwnd, uint flags, out nint enumerator);
        [PreserveSig] int BindToObject(nint pidl, nint bind, ref Guid iid, out nint result);
        [PreserveSig] int BindToStorage(nint pidl, nint bind, ref Guid iid, out nint result);
        [PreserveSig] int CompareIDs(nint param, nint first, nint second);
        [PreserveSig] int CreateViewObject(nint hwnd, ref Guid iid, out nint result);
        [PreserveSig] int GetAttributesOf(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] pidls, ref uint attrs);
        [PreserveSig] int GetUIObjectOf(nint hwnd, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] pidls, ref Guid iid, nint reserved, out nint result);
        [PreserveSig] int GetDisplayNameOf(nint pidl, uint flags, nint name);
        [PreserveSig] int SetNameOf(nint hwnd, nint pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out nint renamed);
    }
    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, uint max);
    }
    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, uint max);
        [PreserveSig] int HandleMenuMsg(uint message, nint wp, nint lp);
    }
    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, uint max);
        [PreserveSig] int HandleMenuMsg(uint message, nint wp, nint lp);
        [PreserveSig] int HandleMenuMsg2(uint message, nint wp, nint lp, out nint result);
    }
    [ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIDList
    {
        [PreserveSig] int Next(uint count, out nint pidl, out uint fetched);
        [PreserveSig] int Skip(uint count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumIDList copy);
    }
}
