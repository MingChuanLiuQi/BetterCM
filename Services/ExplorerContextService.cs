using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Threading;
using BetterCM.Models;
namespace BetterCM.Services;

public sealed record ContextRequest(long Window, string[] ClipboardPaths, int? X, int? Y, bool Navigation, string Id);
public sealed class ExplorerContextService : IDisposable
{
    private readonly bool _testHost;
    public ExplorerContextService() { }
    internal ExplorerContextService(bool testHost) => _testHost = testHost;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private NamedPipeServerStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;
    internal uint LastStartupFlags { get; private set; }
    public async Task PrepareAsync()
    {
        try { await CaptureAsync(NativeMethods.FindWindow("Progman", null), []); }
        catch (Exception ex) { if (!_disposed) AppLog.Write("context-prepare", ex); }
    }
    public async Task<SelectedContext> CaptureAsync(nint window, IReadOnlyList<string> clipboardPaths, NativeMethods.POINT? click = null, CancellationToken cancellation = default, bool navigation = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(1800));
        await _gate.WaitAsync(deadline.Token);
        try
        {
            if (_pipe?.IsConnected != true || _process?.HasExited != false)
            {
                Stop();
                var name = "BetterCM.Context." + Guid.NewGuid().ToString("N");
                _pipe = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                _process = BackgroundProcess.Start(Environment.ProcessPath!, _testHost ? "--context-test-host" : "--context-host", name);
                await _pipe.WaitForConnectionAsync(deadline.Token);
                _reader = new(_pipe, new UTF8Encoding(false), false, 4096, true);
                _writer = new(_pipe, new UTF8Encoding(false), 4096, true);
            }
            var request = new ContextRequest((long)window, clipboardPaths.ToArray(), click?.X, click?.Y, navigation, Guid.NewGuid().ToString("N"));
            await _writer!.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), deadline.Token);
            await _writer.FlushAsync(deadline.Token);
            var response = JsonSerializer.Deserialize<ContextResponse>(await _reader!.ReadLineAsync(deadline.Token) ?? throw new IOException("上下文进程已退出"))!;
            deadline.Token.ThrowIfCancellationRequested();
            if (response.Id != request.Id) throw new IOException("上下文响应身份不匹配");
            if (response.Error != null) throw new InvalidOperationException(response.Error);
            LastStartupFlags = response.StartupFlags;
            return response.Context! with { SourceWindow = window };
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            Stop(); AppLog.Write("context-timeout", new TimeoutException("上下文捕获超过 1800 ms，已废弃该捕获进程"));
            return new() { SourceWindow = window, Source = "上下文读取超时 · 请重试或使用 Shift+右键" };
        }
        catch { Stop(); throw; }
        finally { _gate.Release(); }
    }
    private void Stop()
    {
        _pipe?.Dispose();
        try { _reader?.Dispose(); _writer?.Dispose(); } catch (IOException) { } catch (ObjectDisposedException) { }
        _reader = null; _writer = null; _pipe = null;
        if (_process is { HasExited: false }) { try { _process.Kill(); } catch { } }
        _process?.Dispose(); _process = null;
    }
    public void Dispose() { _disposed = true; Stop(); }
    internal sealed record ContextResponse(string Id, SelectedContext? Context = null, string? Error = null, uint StartupFlags = 0);
    public static async Task RunHostAsync(string name, Dispatcher dispatcher, bool test = false)
    {
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(6000);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true);
            while (await reader.ReadLineAsync() is { } line)
            {
                var request = JsonSerializer.Deserialize<ContextRequest>(line)!;
                var response = await dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (test && request.ClipboardPaths.Contains("stall")) Thread.Sleep(40000);
                        return new ContextResponse(request.Id, test ? Build([], AppContext.BaseDirectory, "Test capture", "测试捕获", true) : Capture(request), StartupFlags: BackgroundProcess.StartupFlags());
                    }
                    catch (Exception ex) { AppLog.Write("context", ex); return new ContextResponse(request.Id, Error: ex.Message); }
                });
                await writer.WriteLineAsync(JsonSerializer.Serialize(response)); await writer.FlushAsync();
            }
        }
        catch (IOException) { }
    }
    private static (string[] Names, string Value, bool Pinned)? SelectClickedItem(nint window, NativeMethods.POINT point, bool navigation)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var process);
        var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
        for (int depth = 0; element != null && depth < 24; depth++, element = TreeWalker.ControlViewWalker.GetParent(element))
        {
            if (element.Current.ProcessId != process) throw new InvalidOperationException("点击目标窗口已变化");
            // The clicked UIA TreeItem is authoritative even if an Explorer build changes
            // the native child-window class. Do not substitute the file view's selection.
            if (element.Current.ControlType == ControlType.TreeItem)
            {
                var names = new List<string>(); string value = "";
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var v)) value = ((ValuePattern)v).Current.Value;
                for (var node = element; node != null && names.Count < 16; node = TreeWalker.ControlViewWalker.GetParent(node))
                    if (node.Current.ControlType == ControlType.TreeItem) names.Add(NavigationName(node.Current.Name));
                string rawName = element.Current.Name;
                bool pinned = rawName.Contains("已固定", StringComparison.Ordinal) || rawName.Contains("pinned", StringComparison.OrdinalIgnoreCase) || value.Contains("已固定", StringComparison.Ordinal) || value.Contains("pinned", StringComparison.OrdinalIgnoreCase);
                return (names.ToArray(), value, pinned);
            }
            if (!navigation && element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selected))
            {
                var item = (SelectionItemPattern)selected;
                if (!item.Current.IsSelected) item.Select();
                return null;
            }
            if (!navigation && element.Current.ControlType == ControlType.List && element.TryGetCurrentPattern(SelectionPattern.Pattern, out var container))
            {
                foreach (var item in ((SelectionPattern)container).Current.GetSelection())
                    if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)) ((SelectionItemPattern)pattern).RemoveFromSelection();
                return null;
            }
        }
        if (navigation) throw new InvalidOperationException("无法确认导航栏节点");
        return null;
    }
    private static SelectedContext Capture(ContextRequest request)
    {
        nint window = (nint)request.Window;
        var kind = NativeMethods.ClassName(window);
        if (kind is not ("CabinetWClass" or "Progman" or "WorkerW"))
            return Build(request.ClipboardPaths.Where(x => File.Exists(x) || Directory.Exists(x)).ToArray(), "", "", "剪贴板路径", false);
        var com = new List<object>();
        object Own(object value) { com.Add(value); return value; }
        try
        {
            var clicked = request.X is int x && request.Y is int y ? SelectClickedItem(window, new() { X = x, Y = y }, request.Navigation) : null;
            dynamic shell = Own(Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!);
            if (clicked is { } node)
            {
                string path = ResolveNavigation((object)shell, node.Names, node.Value, Own);
                if (path.Length == 0) throw new InvalidOperationException("无法可靠解析导航栏目标，请使用 Shift+右键");
                return Build([path], "", node.Names[0], "资源管理器导航栏", false) with { PinnedNavigation = node.Pinned || node.Names.Length == 2 && IsHomePinned((object)shell, path, Own) };
            }
            using var view = ShellViewBinding.ForCapture(window);
            dynamic document = Own(view.GetDocument()); dynamic folder = Own(document.Folder); dynamic self = Own(folder.Self);
            string directory = self.Path, display = self.Name;
            dynamic selectedItems = Own(document.SelectedItems()); var selectedPaths = new List<string>(); string selectedName = "";
            for (int i = 0; i < (int)selectedItems.Count; i++) { dynamic item = Own(selectedItems.Item(i)); selectedPaths.Add((string)item.Path); selectedName = item.Name; }
            view.ValidateLocation();
            return Build(selectedPaths.ToArray(), directory, selectedPaths.Count == 0 ? display : selectedPaths.Count == 1 ? selectedName : $"已选中 {selectedPaths.Count} 个项目", kind == "CabinetWClass" ? "资源管理器选区" : "桌面选区", selectedPaths.Count == 0) with { ShellViewPath = directory };
        }
        finally { for (int i = com.Count - 1; i >= 0; i--) try { if (Marshal.IsComObject(com[i])) Marshal.ReleaseComObject(com[i]); } catch { } }
    }
    internal static SelectedContext Build(string[] selected, string folder, string display, string source, bool background)
    {
        var all = selected.Length > 0 ? selected : folder.Length > 0 ? [folder] : Array.Empty<string>();
        var physical = selected.Where(x => File.Exists(x) || Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string directory = Directory.Exists(folder) ? folder : physical.Length > 0 ? Directory.Exists(physical[0]) ? physical[0] : Path.GetDirectoryName(physical[0]) ?? "" : "";
        return new() { Source = source, CurrentDirectory = directory, Items = physical.Select(x => new SelectedItem(x, Directory.Exists(x))).ToArray(), ShellParsingPaths = all.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), DisplayName = display, ShellBackground = background };
    }
    internal static string NavigationName(string text)
    {
        text = new string(text.Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).ToArray()).Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^(?:快速访问(?:开始|结束)|Quick access(?: start| end))\s*[-–]\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s*[（(](?:已固定|pinned)[）)]$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
    }
    internal static string ResolveNavigation(object shellObject, string[] names, string value, Func<object, object> own)
    {
        dynamic shell = shellObject;
        if (names.Length == 0) return "";
        names = names.Select(NavigationName).ToArray();
        if (File.Exists(value) || Directory.Exists(value) || value.StartsWith("::{", StringComparison.Ordinal)) return value;
        // Folder automation accepts parsing paths/CSIDLs, not the shell: protocol used by Explorer.
        // Read Home afresh so unpin/repin/reordering never depends on a cached tree position.
        const string home = "::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}";
        bool shortcutLevel = names.Length <= 2 || names[1] is "快速访问" or "Quick access" or "主文件夹" or "Home";
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            dynamic homeFolder = own(shell.NameSpace(home)); dynamic homeItems = own(homeFolder.Items());
            for (int i = 0; i < (int)homeItems.Count; i++)
            {
                dynamic item = own(homeItems.Item(i));
                if (shortcutLevel && (bool)item.IsFolder && string.Equals(NavigationName((string)item.Name), names[0], StringComparison.OrdinalIgnoreCase)) pinned.Add((string)item.Path);
            }
        }
        catch { }
        if (pinned.Count > 0) return pinned.Count == 1 ? pinned.Single() : "";
        var roots = new List<string>();
        foreach (object ns in new object[] { 0, 17, 5, 16, 39, "::{374DE290-123F-4565-9164-39C4925E467B}", "::{031E4825-7B94-4DC3-B131-E946B44C8DD5}", home, "::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", "::{018D5C66-4533-4307-9B53-224DE2ED1FE6}" })
            try
            {
                dynamic folder = own(shell.NameSpace(ns)); dynamic self = own(folder.Self);
                string path = self.Path;
                if (shortcutLevel && string.Equals((string)self.Name, names[0], StringComparison.OrdinalIgnoreCase)) return path;
                roots.Add(path);
            }
            catch { }
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            try
            {
                dynamic folder = own(shell.NameSpace(root)); dynamic entries = own(folder.Items());
                for (int i = 0; i < (int)entries.Count; i++)
                {
                    dynamic item = own(entries.Item(i)); string name = item.Name, path = item.Path;
                    if (string.Equals(name, names[0], StringComparison.OrdinalIgnoreCase)) matches.Add(path);
                    if (names.Length > 1 && string.Equals(name, names[^1], StringComparison.OrdinalIgnoreCase))
                    {
                        string current = path;
                        for (int depth = names.Length - 2; depth >= 0 && current.Length > 0; depth--)
                        {
                            dynamic nested = own(shell.NameSpace(current)); dynamic children = own(nested.Items()); string next = "";
                            for (int j = 0; j < (int)children.Count; j++) { dynamic child = own(children.Item(j)); if (string.Equals((string)child.Name, names[depth], StringComparison.OrdinalIgnoreCase)) { next = child.Path; break; } }
                            current = next;
                        }
                        if (current.Length > 0) return current;
                    }
                }
            }
            catch { }
        return matches.Count == 1 ? matches.Single() : "";
    }
    internal static bool IsHomePinned(object shellObject, string path, Func<object, object> own)
    {
        // Some Explorer UIA providers omit the visual "pinned" annotation. Use the actual
        // Home property instead of inferring state from a display name or cached position.
        dynamic shell = shellObject;
        try
        {
            dynamic home = own(shell.NameSpace("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}"));
            dynamic items = own(home.Items());
            for (int i = 0; i < (int)items.Count; i++)
            {
                dynamic item = own(items.Item(i));
                if ((bool)item.IsFolder && string.Equals((string)item.Path, path, StringComparison.OrdinalIgnoreCase))
                    return item.ExtendedProperty("System.Home.IsPinned") is bool pinned && pinned;
            }
        }
        catch (Exception ex) { AppLog.Write("navigation-pin-state", ex); }
        return false;
    }
}
