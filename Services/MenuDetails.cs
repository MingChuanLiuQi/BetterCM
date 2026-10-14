using System.Windows.Media;
namespace BetterCM.Services;

// A timed-out native read cannot be interrupted safely. Keep at most one pending
// read per kind, rather than enqueueing unlimited work on repeated right-clicks.
internal sealed class OptionalProbe<T>
{
    private readonly object _sync = new();
    private Task<T?>? _inFlight;
    private string? _identity;
    // Start only when idle; never queue behind an existing read. The returned task
    // still represents the newly accepted read, rather than its scheduling alone.
    internal Task<T?> TryStartNoWait(Func<Task<T?>> read) => ReadAsync(read, TimeSpan.Zero);
    internal async Task<T?> ReadAsync(Func<Task<T?>> read, TimeSpan timeout, CancellationToken cancellation = default, string? identity = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            Task<T?> prior;
            Task<T?>? current = null;
            bool share;
            lock (_sync)
            {
                if (_inFlight == null || _inFlight.IsCompleted)
                {
                    current = RunAsync(read);
                    _inFlight = current;
                    _identity = identity;
                }
                prior = _inFlight;
                share = identity != null && string.Equals(identity, _identity, StringComparison.OrdinalIgnoreCase);
            }
            if (current != null)
            {
                var remaining = timeout <= TimeSpan.Zero ? Timeout.InfiniteTimeSpan : timeout - watch.Elapsed;
                if (timeout > TimeSpan.Zero && remaining <= TimeSpan.Zero) return default;
                try { return await current.WaitAsync(remaining, cancellation).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch { return default; }
            }
            var wait = timeout - watch.Elapsed;
            if (wait <= TimeSpan.Zero) return default;
            try
            {
                var result = await prior.WaitAsync(wait, cancellation).ConfigureAwait(false);
                if (share) return result; // Same target only; different files need their own read.
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch { return default; }
        }
    }
    private static async Task<T?> RunAsync(Func<Task<T?>> read)
    {
        try { return await read().ConfigureAwait(false); }
        catch { return default; }
    }
}
internal static class MenuDetails
{
    private static readonly OptionalProbe<ImageSource> Icons = new();
    private static readonly OptionalProbe<long?> Sizes = new();
    internal static Task<ImageSource?> IconAsync(string path, bool directory, TimeSpan? timeout = null, CancellationToken cancellation = default)
        => Icons.ReadAsync(() => IconCache.GetAsync(path, directory), timeout ?? TimeSpan.FromSeconds(2), cancellation, IconCache.KeyForPath(path, directory));
    internal static Task<long?> SizeAsync(string path, bool file, TimeSpan? timeout = null, CancellationToken cancellation = default)
        => !file ? Task.FromResult<long?>(null) : Sizes.ReadAsync(() => Task.Run<long?>(() => new FileInfo(path).Length), timeout ?? TimeSpan.FromSeconds(2), cancellation, IconCache.KeyForPath(path, false));
    internal static async Task LoadAsync(Task<ImageSource?> icon, Task<long?> size, Action<ImageSource?, long?> publish, CancellationToken cancellation, TimeSpan? timeout = null)
    {
        var deadline = timeout ?? TimeSpan.FromSeconds(2);
        var boundedIcon = ReadAsync(icon, deadline, cancellation);
        var boundedSize = ReadAsync(size, deadline, cancellation);
        ImageSource? image = null; long? length = null;
        if (await Task.WhenAny(boundedIcon, boundedSize) == boundedIcon)
        {
            image = await boundedIcon; cancellation.ThrowIfCancellationRequested(); publish(image, length);
            length = await boundedSize;
        }
        else
        {
            length = await boundedSize; cancellation.ThrowIfCancellationRequested(); publish(image, length);
            image = await boundedIcon;
        }
        cancellation.ThrowIfCancellationRequested(); publish(image, length);
    }
    private static async Task<T?> ReadAsync<T>(Task<T?> task, TimeSpan timeout, CancellationToken cancellation)
    {
        try { return await task.WaitAsync(timeout, cancellation); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch { return default; }
    }
}
