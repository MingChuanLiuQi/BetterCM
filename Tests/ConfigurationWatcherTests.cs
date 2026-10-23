using System.Windows.Threading;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class ConfigurationWatcherTests
{
    [Fact]
    public async Task InitialCreationFailureRecoversAndSubsequentEditsAreWatched()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string blocked = Path.Combine(root, "blocked"), path = Path.Combine(blocked, "config.json");
        File.WriteAllText(blocked, "A file blocks initial directory creation.");
        using var worker = new StaWorker("Watcher recovery regression");
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reloads = 0;
        ConfigurationWatcher? watcher = null;
        try
        {
            watcher = await worker.Run(() => new ConfigurationWatcher(path, Dispatcher.CurrentDispatcher, () =>
            {
                if (++reloads == 1) recovered.TrySetResult(); else changed.TrySetResult();
            }));
            Assert.False(recovered.Task.IsCompleted);
            File.Delete(blocked); Directory.CreateDirectory(blocked); File.WriteAllText(path, "{}");
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            File.WriteAllText(path, "{\"Theme\":\"dark\"}");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (watcher != null) await worker.Run(() => { watcher.Dispose(); return true; });
            Directory.Delete(root, true);
        }
    }
}
