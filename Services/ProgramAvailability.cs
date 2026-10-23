using System.Diagnostics;
namespace BetterCM.Services;

// File.Exists can block indefinitely on an offline network share. Bound both
// the menu's wait and the number of outstanding filesystem probes.
internal sealed class ProgramAvailability(Func<string, bool>? probe = null, Func<string, bool>? isRemote = null, TimeProvider? time = null)
{
    private sealed class Pool
    {
        internal readonly Dictionary<string, Task<bool>> Pending = new(StringComparer.OrdinalIgnoreCase);
        internal readonly BoundedCache<string, DateTimeOffset> RetryAfter = new(128, StringComparer.OrdinalIgnoreCase);
    }
    private readonly Pool _local = new(), _remote = new();
    private readonly Func<string, bool> _probe = probe ?? File.Exists;
    private readonly Func<string, bool> _isRemote = isRemote ?? IsRemotePath;
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    internal static bool IsRemotePath(string path)
    {
        // Extended drive paths still need drive classification (including mapped drives).
        string candidate = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
        if (candidate.StartsWith(@"\\", StringComparison.Ordinal) || candidate.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase)) return true;
        string? root = Path.GetPathRoot(candidate);
        return string.IsNullOrEmpty(root) || NativeMethods.GetDriveType(root) is 0 or 1 or 4;
    }
    internal async Task<bool?> CheckAsync(string path, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        bool remote = _isRemote(path);
        var pool = remote ? _remote : _local;
        var budget = TimeSpan.FromMilliseconds(250);
        var started = Stopwatch.StartNew();
        while (true)
        {
            Task<bool>? pending = null;
            Task[]? running = null;
            lock (pool)
            {
                if (remote && pool.RetryAfter.TryGet(path, _time.GetUtcNow(), out _)) return null;
                if (pool.Pending.TryGetValue(path, out var existing))
                    pending = existing;
                else if (pool.Pending.Count < 4)
                {
                    bool Probe()
                    {
                        try { return _probe(path); }
                        catch (Exception ex) { AppLog.Write("program-availability", ex); return false; }
                        finally { lock (pool) pool.Pending.Remove(path); }
                    }
                    // A blocked network syscall must not occupy the thread pool used by local actions.
                    pending = remote
                        ? Task.Factory.StartNew(Probe, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                        : Task.Run(Probe);
                    pool.Pending.Add(path, pending);
                }
                else
                    running = [.. pool.Pending.Values];
            }
            if (pending != null)
            {
                var remaining = budget - started.Elapsed;
                if (remaining <= TimeSpan.Zero) return null;
                try
                {
                    bool result = await pending.WaitAsync(remaining, cancellation);
                    if (remote && !result) Backoff();
                    return result;
                }
                catch (TimeoutException) { Backoff(); return null; }
            }
            var wait = budget - started.Elapsed;
            if (wait <= TimeSpan.Zero) return null;
            try { await Task.WhenAny(running!).WaitAsync(wait, cancellation); }
            catch (TimeoutException) { Backoff(); return null; }
        }
        void Backoff()
        {
            if (!remote) return;
            lock (pool)
            {
                var retry = _time.GetUtcNow().AddSeconds(2);
                pool.RetryAfter.Set(path, retry, retry);
            }
        }
    }
}
