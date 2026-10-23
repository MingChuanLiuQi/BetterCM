using System.Windows.Threading;
namespace BetterCM.Services;

public sealed class ConfigurationWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private readonly string _path;
    private bool _restart;
    private bool _disposed;
    public ConfigurationWatcher(string path, Dispatcher dispatcher, Action reload)
    {
        _path = Path.GetFullPath(path);
        _dispatcher = dispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            if (_disposed) return;
            if (_restart)
            {
                try { StartWatcher(); _restart = false; _timer.Interval = TimeSpan.FromMilliseconds(350); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { AppLog.Write("config-watch-retry", ex); _timer.Interval = TimeSpan.FromSeconds(2); _timer.Start(); return; }
            }
            reload();
        };
        try { StartWatcher(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write("config-watch-start", ex);
            _restart = true; _timer.Interval = TimeSpan.FromSeconds(2); _timer.Start();
        }
    }
    private void StartWatcher()
    {
        _watcher?.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _watcher = new(Path.GetDirectoryName(_path)!, Path.GetFileName(_path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        _watcher.Changed += (_, _) => QueueChange(); _watcher.Created += (_, _) => QueueChange();
        _watcher.Deleted += (_, _) => QueueChange(); _watcher.Renamed += (_, _) => QueueChange();
        _watcher.Error += (_, args) => { AppLog.Write("config-watch", args.GetException()); QueueChange(restart: true); };
        _watcher.EnableRaisingEvents = true;
    }
    private void QueueChange(bool restart = false)
    {
        if (_dispatcher.HasShutdownStarted) return;
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;
                _restart |= restart;
                _timer.Stop();
                _timer.Interval = _restart ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(350);
                _timer.Start();
            });
        }
        catch (InvalidOperationException) when (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        { /* A final watcher callback may race dispatcher shutdown. */ }
    }
    public void Dispose() { _disposed = true; _watcher?.Dispose(); _timer.Stop(); }
}
