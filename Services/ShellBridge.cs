using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using BetterCM.Models;
namespace BetterCM.Services;

public sealed record ShellRequest(string Kind, string[]? Paths = null, long Owner = 0, string? Session = null, int Command = -1, bool Background = false, string? RequestId = null, string ViewPath = "", bool PinnedNavigation = false, bool DeferSubmenus = false);
public sealed record ShellResponse(bool Success, string? Error = null, string? Session = null, IReadOnlyList<ShellItem>? Items = null, string? RequestId = null, IReadOnlyList<ShellMenuNode>? Menu = null, ShellBuildTimings? BuildTimings = null, bool Superseded = false);
public sealed class ShellBridge : IDisposable
{
    private readonly object _sync = new();
    private ShellConnection? _current, _spare;
    private readonly HashSet<ShellConnection> _invocations = [];
    private bool _disposed;
    private int _opening;
    internal int OpenCount { get; private set; }
    private readonly bool _testHost;
    private readonly TimeSpan _invokeTimeout;
    public ShellBridge() : this(Timeout.InfiniteTimeSpan, false) { }
    internal ShellBridge(TimeSpan invokeTimeout, bool testHost) { _invokeTimeout = invokeTimeout; _testHost = testHost; }
    private ShellConnection NewConnection() => new(_invokeTimeout, _testHost);
    public Task PrepareAsync()
    {
        lock (_sync)
        {
            if (_disposed) return Task.CompletedTask;
            if (_current is { Closed: true }) _current = null;
            _current ??= NewConnection(); return _current.WarmAsync();
        }
    }
    public async Task<ShellResponse> RefreshAsync(SelectedContext context)
    {
        using var connection = NewConnection();
        return await connection.SendAsync(new("refresh", context.ShellTargets.ToArray(), (long)context.SourceWindow));
    }
    public async Task<ShellResponse> OpenAsync(SelectedContext ctx, CancellationToken cancellation = default, bool deferSubmenus = false)
    {
        ShellConnection connection; int generation;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            generation = ++_opening;
            OpenCount++;
            if (_current is { } previous && (previous.Busy || previous.Closed)) { previous.Dispose(); _current = null; }
            if (_spare is { Closed: true }) { _spare.Dispose(); _spare = null; }
            if (_current == null) { _current = _spare ?? NewConnection(); _spare = null; }
            connection = _current;
        }
        var response = await connection.SendAsync(new("open", ctx.ShellTargets.ToArray(), (long)ctx.SourceWindow, Background: ctx.ShellBackground, ViewPath: ctx.ShellViewPath, PinnedNavigation: ctx.PinnedNavigation, DeferSubmenus: deferSubmenus), cancellation);
        lock (_sync)
        {
            if (_disposed || generation != _opening || _current != connection) return new(false, "菜单上下文已改变");
            if (response.Success)
            {
                connection.Session = response.Session;
                // Keep one ready transport for the next menu while the current host invokes a slow verb.
                _spare ??= NewConnection(); _ = _spare.WarmAsync();
            }
            else { connection.Dispose(); _current = null; }
        }
        return response;
    }
    public async Task<ShellResponse> InvokeAsync(string session, int command)
    {
        ShellConnection connection;
        lock (_sync)
        {
            if (_disposed || _current?.Session != session) return new(false, "此菜单已结束，请重新右键继续操作");
            connection = _current; connection.InvocationReserved = true;
            _current = _spare; _spare = null;
            _invocations.Add(connection);
        }
        try { return await connection.SendAsync(new("invoke", Session: session, Command: command)); }
        finally { lock (_sync) _invocations.Remove(connection); connection.Dispose(); }
    }
    public async Task<ShellResponse> ExpandAsync(string session, int? submenu)
    {
        ShellConnection connection;
        lock (_sync)
        {
            if (_disposed || _current?.Session != session) return new(false, "菜单上下文已改变，请重新呼出", Superseded: true);
            connection = _current;
        }
        var response = await connection.SendAsync(new("expand", Session: session, Command: submenu ?? -1));
        lock (_sync)
        {
            if (_disposed || _current != connection || connection.Session != session) return new(false, "菜单上下文已改变", Superseded: true);
            // A genuine transport failure must remain visible, but the next right-click
            // must not reuse the dead connection and fail a second time.
            if (connection.Closed) _current = null;
            return response;
        }
    }
    internal int? InvokingProcessId { get { lock (_sync) return _invocations.FirstOrDefault()?.ProcessId; } }
    internal int? CurrentProcessId { get { lock (_sync) return _current?.ProcessId; } }
#if QA
    internal async Task<ShellResponse> TestLoggingAsync(int count, string scope)
    {
        using var connection = NewConnection();
        return await connection.SendAsync(new("test-log", [scope], Command: count));
    }
    internal async Task<uint> TestStartupFlagsAsync()
    {
        using var connection = NewConnection();
        var response = await connection.SendAsync(new("startup-flags"));
        if (!response.Success || !uint.TryParse(response.Error, out var flags)) throw new IOException("无法读取辅助进程启动标志");
        return flags;
    }
#endif
    internal Task PrepareNextAsync() { lock (_sync) return _spare?.WarmAsync() ?? Task.CompletedTask; }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return; _disposed = true;
            _current?.Dispose(); _spare?.Dispose();
            foreach (var connection in _invocations) connection.Dispose();
            _current = _spare = null;
        }
    }
    private sealed class ShellConnection(TimeSpan invokeTimeout, bool testHost) : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _startSync = new();
        private readonly object _operationSync = new();
        private readonly CancellationTokenSource _lifetime = new();
        private Task? _start;
        private Process? _process;
        private NamedPipeServerStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private int _busy, _disposed;
        private bool _invokeSent;
        private bool _resourcesClosed;
        public bool Busy => Volatile.Read(ref _busy) != 0;
        public bool Closed => Volatile.Read(ref _disposed) != 0;
        public bool InvocationReserved { get; set; }
        public string? Session { get; set; }
        public int? ProcessId { get { lock (_operationSync) return _disposed == 0 ? _process?.Id : null; } }
        public async Task WarmAsync() { try { await EnsureStartedAsync(); } catch { Dispose(); } }
        private Task EnsureStartedAsync() { lock (_startSync) return _start ??= StartAsync(); }
        private async Task StartAsync()
        {
            var name = "BetterCM.Shell." + Guid.NewGuid().ToString("N");
            lock (_operationSync)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                _pipe = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                string hostMode = "--shell-host";
                _ = testHost;
#if QA
            if (testHost) hostMode = "--shell-test-host";
#endif
                _process = BackgroundProcess.Start(Environment.ProcessPath!, hostMode, name);
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); deadline.CancelAfter(6000);
            await _pipe.WaitForConnectionAsync(deadline.Token);
            _reader = new(_pipe, new UTF8Encoding(false), false, 4096, true);
            _writer = new(_pipe, new UTF8Encoding(false), 4096, true);
        }
        public async Task<ShellResponse> SendAsync(ShellRequest request, CancellationToken cancellation = default)
        {
            bool acquired = false;
            lock (_operationSync)
            {
                if (_disposed != 0) return new(false, "系统菜单连接已关闭，请重新呼出");
                Interlocked.Increment(ref _busy);
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
            deadline.CancelAfter(request.Kind == "invoke" ? invokeTimeout : TimeSpan.FromSeconds(6));
            try
            {
                await _gate.WaitAsync(deadline.Token); acquired = true;
                await EnsureStartedAsync().WaitAsync(deadline.Token);
                // Startup stays bounded independently. A full enhanced tree may
                // legitimately need longer than a deferred classic root menu.
                deadline.CancelAfter(request.Kind == "invoke" ? invokeTimeout : request.Kind == "open" && !request.DeferSubmenus ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(6));
                var id = Guid.NewGuid().ToString("N"); request = request with { RequestId = id };
                // From this point a verb may be executing: every failure only closes IPC, never kills the host or its children.
                if (request.Kind == "invoke")
                {
                    _invokeSent = true;
                    if (_process != null) NativeMethods.AllowSetForegroundWindow((uint)_process.Id);
                }
                await _writer!.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), deadline.Token);
                await _writer.FlushAsync(deadline.Token);
                var response = JsonSerializer.Deserialize<ShellResponse>(await _reader!.ReadLineAsync(deadline.Token) ?? throw new IOException("系统菜单进程已退出"))!;
                if (response.RequestId != id) throw new IOException("系统菜单响应身份不匹配");
                return response;
            }
            catch (Exception ex)
            {
                bool expectedCancellation = cancellation.IsCancellationRequested || _lifetime.IsCancellationRequested;
                Dispose();
                if (!expectedCancellation) AppLog.Write("shell-transport", ex);
                return new(false, request.Kind == "invoke" ? "未收到系统动作的完成结果，连接已结束；动作可能仍由 Windows 继续执行。请先确认结果后再重试。" : "系统项目读取失败，可重试或使用 Shift+右键：" + ex.Message);
            }
            finally
            {
                if (acquired) _gate.Release();
                lock (_operationSync) { if (Interlocked.Decrement(ref _busy) == 0 && _disposed != 0) CloseResources(); }
            }
        }
        public void Dispose()
        {
            lock (_operationSync)
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                _lifetime.Cancel();
                _pipe?.Dispose(); // Closing the pipe releases a pending read without racing StreamReader.Dispose.
                if (!InvocationReserved && !_invokeSent && _process is { HasExited: false }) { try { _process.Kill(); } catch { } }
                _process?.Dispose();
                if (!Busy) CloseResources();
            }
        }
        private void CloseResources()
        {
            if (_resourcesClosed) return; _resourcesClosed = true;
            try { _reader?.Dispose(); _writer?.Dispose(); } catch (IOException) { } catch (ObjectDisposedException) { }
            _gate.Dispose(); // Dispose only after every in-flight finally has released it.
        }
    }
}
public static class ShellHost
{
    public static async Task RunAsync(string pipeName, Dispatcher dispatcher, bool test = false)
    {
        using var lifetime = test ? null : new ShellHostLifetime();
        bool invoked = false;
        ShellMenuSession? session = null;
#if QA
        string? testSession = null;
#endif
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(6000);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true);
            while (await reader.ReadLineAsync() is { } line)
            {
                var request = JsonSerializer.Deserialize<ShellRequest>(line)!;
                var response = await dispatcher.InvokeAsync(() =>
                {
                    try
                    {
#if QA
                        if (test)
                        {
                            if (request.Kind == "startup-flags") return new ShellResponse(true, Error: BackgroundProcess.StartupFlags().ToString());
                            if (request.Kind == "test-log") { for (int i = 0; i < request.Command; i++) AppLog.Write(request.Paths![0], new InvalidOperationException("并发日志夹具 " + i)); return new ShellResponse(true); }
                            if (request.Kind == "open") { if (request.ViewPath == "slow-open") Thread.Sleep(6500); testSession = Guid.NewGuid().ToString("N"); return new ShellResponse(true, Session: testSession, Items: [new(0, "属性", "", "properties", "properties", true), new(40, "Slow test verb", "", "test", "test", true)]); }
                            if (request.Kind == "expand" && request.Session == testSession)
                            {
                                if (request.Command == -3) Environment.Exit(77); // Isolated failure fixture, never a real Shell host.
                                Thread.Sleep(Math.Clamp(request.Command, 0, 2000)); return new ShellResponse(true, Session: testSession, Menu: [new("延迟读取夹具", 0)]);
                            }
                            if (request.Kind == "invoke" && request.Session == testSession) { Thread.Sleep(request.Command * 1000); return new ShellResponse(true); }
                        }
                        else
#endif
                        {
                            if (request.Kind == "refresh")
                            {
                                using var view = ShellViewBinding.Open((nint)request.Owner, request.Paths?.FirstOrDefault() ?? "");
                                view.Refresh(); return new ShellResponse(true);
                            }
                            if (request.Kind == "open") { session?.Dispose(); session = new(request.Paths ?? [], (nint)request.Owner, request.Background, request.ViewPath, request.PinnedNavigation, request.DeferSubmenus); return new ShellResponse(true, Session: session.Id, Items: session.Items, Menu: session.Menu, BuildTimings: session.BuildTimings); }
                            if (request.Kind == "expand" && session != null && session.Id == request.Session)
                                return new ShellResponse(true, Session: session.Id, Items: session.Items, Menu: request.Command < 0 ? session.CompleteMenu() : session.ExpandSubmenu(request.Command));
                            if (request.Kind == "invoke" && session != null && session.Id == request.Session) { invoked = true; session.Invoke(request.Command); return new ShellResponse(true); }
                        }
                        return new ShellResponse(false, "菜单上下文已改变，请重新呼出后执行");
                    }
                    catch (Exception ex) { AppLog.Write("shell-command", ex); return new ShellResponse(false, ex.Message + $" (0x{ex.HResult:X8})"); }
                });
                await writer.WriteLineAsync(JsonSerializer.Serialize(response with { RequestId = request.RequestId })); await writer.FlushAsync();
            }
        }
        catch (IOException) { /* Client may close after a response or while a native dialog remains open. */ }
        catch (Exception ex) { AppLog.Write("shell-host", ex); }
        finally
        {
            async Task Cleanup(string scope, Func<Task> step)
            { try { await step(); } catch (Exception ex) { AppLog.Write(scope, ex); } }
            if (invoked && session != null) await Cleanup("shell-new-callback", session.WaitForNewCallbackAsync);
            await Cleanup("shell-release-commands", () => { session?.ReleaseCommands(); return Task.CompletedTask; });
            if (invoked && lifetime != null) await Cleanup("shell-drain", lifetime.DrainAsync);
            if (session != null) await Cleanup("shell-drain-callbacks", session.DrainCallbacksAsync);
            await Cleanup("shell-dispose", () => { session?.Dispose(); return Task.CompletedTask; });
        }
    }
}
