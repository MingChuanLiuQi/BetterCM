using System.Windows.Threading;
namespace BetterCM.Services;

public sealed class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    public ConfigurationWatcher(string path, Dispatcher dispatcher, Action reload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += (_, _) => { _timer.Stop(); if (!_disposed) reload(); };
        _watcher = new(Path.GetDirectoryName(path)!, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        void Changed() => dispatcher.BeginInvoke(() => { if (!_disposed) { _timer.Stop(); _timer.Start(); } });
        _watcher.Changed += (_, _) => Changed(); _watcher.Created += (_, _) => Changed();
        _watcher.Deleted += (_, _) => Changed(); _watcher.Renamed += (_, _) => Changed();
        _watcher.EnableRaisingEvents = true;
    }
    public void Dispose() { _disposed = true; _watcher.Dispose(); _timer.Stop(); }
}
