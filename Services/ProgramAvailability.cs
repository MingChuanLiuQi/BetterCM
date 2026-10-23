using System.Diagnostics;
namespace BetterCM.Services;

// File.Exists can block indefinitely on an offline network share. Bound both
// the menu's wait and the number of outstanding filesystem probes.
internal sealed class ProgramAvailability(Func<string, bool>? probe = null)
{
    private readonly Dictionary<string, Task<bool>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, bool> _probe = probe ?? File.Exists;
    internal async Task<bool?> CheckAsync(string path, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var budget = TimeSpan.FromMilliseconds(250);
        var started = Stopwatch.StartNew();
        while (true)
        {
            Task<bool>? pending = null;
            Task[]? running = null;
            lock (_pending)
            {
                if (_pending.TryGetValue(path, out var existing))
                    pending = existing;
                else if (_pending.Count < 4)
                {
                    pending = Task.Run(() =>
                    {
                        try { return _probe(path); }
                        catch (Exception ex) { AppLog.Write("program-availability", ex); return false; }
                        finally { lock (_pending) _pending.Remove(path); }
                    });
                    _pending.Add(path, pending);
                }
                else
                    running = [.. _pending.Values];
            }
            if (pending != null)
            {
                var remaining = budget - started.Elapsed;
                if (remaining <= TimeSpan.Zero) return null;
                try { return await pending.WaitAsync(remaining, cancellation); }
                catch (TimeoutException) { return null; }
            }
            var wait = budget - started.Elapsed;
            if (wait <= TimeSpan.Zero) return null;
            try { await Task.WhenAny(running!).WaitAsync(wait, cancellation); }
            catch (TimeoutException) { return null; }
        }
    }
}
