using System.Windows.Threading;
namespace BetterCM.Services;

public sealed class StaWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public StaWorker(string name)
    {
        _thread = new Thread(() => { _ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }
    public async Task<T> Run<T>(Func<T> callback) => await (await _ready.Task).InvokeAsync(callback).Task;
    public void Dispose()
    {
        if (_ready.Task.IsCompletedSuccessfully) _ready.Task.Result.BeginInvokeShutdown(DispatcherPriority.Background);
    }
}
