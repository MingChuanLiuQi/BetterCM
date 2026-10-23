using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class AppLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "BetterCM.LogTests-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void RotationKeepsPriorLogAndAppendsNewRecordToCurrent()
    {
        Directory.CreateDirectory(_directory); var current = Path.Combine(_directory, "BetterCM.log");
        File.WriteAllText(current, new string('x', (int)AppLog.MaximumBytes)); AppLog.WriteAt(_directory, "rotation", new IOException("新记录"));
        Assert.Equal(AppLog.MaximumBytes, new FileInfo(Path.Combine(_directory, "BetterCM.prev.log")).Length);
        Assert.Contains("新记录", File.ReadAllText(current)); Assert.True(new FileInfo(current).Length < AppLog.MaximumBytes);
    }
    [Fact]
    public void ConcurrentWritersAcrossRotationDoNotLoseRecords()
    {
        Directory.CreateDirectory(_directory); File.WriteAllText(Path.Combine(_directory, "BetterCM.log"), new string('x', (int)AppLog.MaximumBytes - 10));
        Parallel.For(0, 20, i => AppLog.WriteAt(_directory, "writer-" + i + ":", new IOException("记录")));
        var lines = File.ReadAllLines(Path.Combine(_directory, "BetterCM.log")); Assert.Equal(20, lines.Count(x => x.Contains("writer-")));
        for (int i = 0; i < 20; i++) Assert.Single(lines, x => x.Contains("writer-" + i + ":"));
    }
    [Fact]
    public void OversizedSingleEntryIsBounded()
    {
        AppLog.WriteAt(_directory, "large", new IOException(new string('大', 1000000)));
        Assert.True(new FileInfo(Path.Combine(_directory, "BetterCM.log")).Length < AppLog.MaximumBytes);
        Assert.Contains("已截断", File.ReadAllText(Path.Combine(_directory, "BetterCM.log")));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
