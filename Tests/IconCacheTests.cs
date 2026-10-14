using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;
public class IconCacheTests
{
    [Fact] public void SameExtensionAndDirectoriesHaveIndependentPathKeys()
    {
        Assert.NotEqual(IconCache.KeyForPath(@"C:\one\a.txt", false), IconCache.KeyForPath(@"C:\two\b.txt", false));
        Assert.NotEqual(IconCache.KeyForPath(@"C:\one", true), IconCache.KeyForPath(@"C:\two", true));
    }
    [Fact] public void FullCacheEvictsOnlyLeastRecentlyUsedItem()
    {
        var cache = new BoundedCache<string, string>(2); var now = DateTimeOffset.UtcNow;
        cache.Set("one", "1", now.AddMinutes(1)); cache.Set("two", "2", now.AddMinutes(1));
        Assert.True(cache.TryGet("one", now, out _)); cache.Set("three", "3", now.AddMinutes(1));
        Assert.False(cache.TryGet("two", now, out _)); Assert.True(cache.TryGet("one", now, out _)); Assert.True(cache.TryGet("three", now, out _)); Assert.Equal(2, cache.Count);
    }
    [Fact] public void NegativeResultExpiresAndCanBeRetried()
    {
        var cache = new BoundedCache<string, string?>(2); var now = DateTimeOffset.UtcNow;
        cache.Set("slow", null, now.AddSeconds(5)); Assert.True(cache.TryGet("slow", now.AddSeconds(4), out var value)); Assert.Null(value);
        Assert.False(cache.TryGet("slow", now.AddSeconds(5), out _));
        cache.Set("slow", "ready", now.AddMinutes(1)); Assert.True(cache.TryGet("slow", now.AddSeconds(6), out value)); Assert.Equal("ready", value);
    }
}
