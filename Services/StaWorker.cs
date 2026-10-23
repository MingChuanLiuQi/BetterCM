using System.Windows.Threading;
namespace BetterCM.Services;

public sealed class StaWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    internal Task Completion => _stopped.Task;
    public StaWorker(string name)
    {
        _thread = new Thread(() =>
        {
            try
            {
                _ready.SetResult(Dispatcher.CurrentDispatcher);
                // Dispose can arrive before the dispatcher is ready.
                if (Volatile.Read(ref _disposed) == 0) Dispatcher.Run();
            }
            finally { _stopped.TrySetResult(); }
        })
        { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }
    public async Task<T> Run<T>(Func<T> callback)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var dispatcher = await _ready.Task;
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return await dispatcher.InvokeAsync(callback).Task;
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _ = _ready.Task.ContinueWith(static t => t.Result.BeginInvokeShutdown(DispatcherPriority.Background), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
