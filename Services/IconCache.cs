using System.Windows.Media;
namespace BetterCM.Services;

public static class IconCache
{
    private static readonly StaWorker Worker = new("BetterCM icons");
    private static readonly BoundedCache<string, ImageSource?> Cache = new(256, StringComparer.OrdinalIgnoreCase);
    internal static string KeyForPath(string path, bool directory) => (directory ? "directory:" : "file:") + (Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : path);
    public static Task<ImageSource?> GetAsync(string path, bool directory) => Worker.Run(() =>
    {
        if (path.Length == 0) return null;
        var key = KeyForPath(path, directory);
        if (Cache.TryGet(key, DateTimeOffset.UtcNow, out var image)) return image;
        var result = IconHelper.GetIconForPath(path, directory);
        Cache.Set(key, result, DateTimeOffset.UtcNow.AddSeconds(result == null ? 5 : 120));
        return result;
    });
}
