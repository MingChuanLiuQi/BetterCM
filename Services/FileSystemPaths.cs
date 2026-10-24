namespace BetterCM.Services;

internal static class FileSystemPaths
{
    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("路径不能为空");
        path = path.Replace('/', '\\');
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path[8..];
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            path = path[4..];
            if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\') throw new InvalidDataException("仅支持文件系统的盘符和 UNC 路径");
        }
        if (path.StartsWith(@"\\.\", StringComparison.Ordinal) || !Path.IsPathFullyQualified(path) || path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0 || path.Any(char.IsControl)) throw new InvalidDataException("需要不含通配符的文件系统绝对路径");
        if (path.Split('\\').Any(x => x.Length > 0 && x is not ("." or "..") && (x.EndsWith(' ') || x.EndsWith('.')))) throw new InvalidDataException("暂不支持以空格或句点结尾的路径组件");
        if (path.IndexOf(':', path.Length >= 2 && path[1] == ':' ? 2 : 0) >= 0) throw new InvalidDataException("不支持设备路径或备用数据流");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
