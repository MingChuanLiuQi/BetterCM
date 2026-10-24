namespace BetterCM.Services;

internal static class BackupDestinationGuard
{
    internal static void Check(string path, IEnumerable<string>? protectedPaths)
    {
        string target = FileSystemPaths.Normalize(path);
        foreach (string protectedPath in protectedPaths ?? [])
        {
            string reserved = FileSystemPaths.Normalize(protectedPath);
            if (target.Equals(reserved, StringComparison.OrdinalIgnoreCase) || File.Exists(target) && File.Exists(reserved) && FileIdentity.Read(target, false).SameObject(FileIdentity.Read(reserved, false)))
                throw new IOException("导出位置不能覆盖正在使用的配置、配置备份或重命名历史，请选择其他文件名。");
        }
    }
}
