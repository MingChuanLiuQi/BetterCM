using Microsoft.Win32;
namespace BetterCM.Services;

// Resolve once without consulting the selected folder, current directory or relative PATH entries.
public static class ExecutableResolver
{
    public static string? Resolve(string executable) => Resolve(executable, Environment.GetEnvironmentVariable("PATH"), true);
    internal static string? Resolve(string executable, string? searchPath, bool appPaths = false)
    {
        executable = Environment.ExpandEnvironmentVariables(executable.Trim().Trim('"'));
        if (Path.IsPathFullyQualified(executable)) return IsProgram(executable) && File.Exists(executable) ? Path.GetFullPath(executable) : null;
        if (executable.Length == 0 || executable.IndexOfAny(['\\', '/', ':']) >= 0) return null;
        string name = Path.HasExtension(executable) ? executable : executable + ".exe";
        if (!IsProgram(name)) return null;
        if (appPaths)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + name);
                if (key?.GetValue(null) is string value)
                {
                    value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                    if (Path.IsPathFullyQualified(value) && IsProgram(value) && File.Exists(value)) return Path.GetFullPath(value);
                }
            }
        }
        foreach (string item in (searchPath ?? "").Split(';'))
        {
            string folder = Environment.ExpandEnvironmentVariables(item.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(folder)) continue;
            string candidate = Path.Combine(folder, name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }
    public static string Require(string executable) => Resolve(executable) ?? throw new InvalidOperationException("找不到程序，请选择现有 .exe 或 .com 的完整路径：" + executable);
    internal static bool IsProgram(string path) => Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".com", StringComparison.OrdinalIgnoreCase);
    internal static string RequireAbsolute(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || !IsProgram(executable)) throw new InvalidOperationException("程序必须使用 .exe 或 .com 的完整路径，请重新编辑该动作。");
        if (!File.Exists(executable)) throw new FileNotFoundException("程序已移动或卸载，请重新编辑该动作。", executable);
        return executable;
    }
    public static string WindowsPowerShell => Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
    public static string SystemProgram(string name) => Path.Combine(Environment.SystemDirectory, name);
    private static readonly Lazy<string?> CodePath = new(FindCode);
    public static string? VSCode => CodePath.Value;
    private static string? FindCode()
    {
        var resolved = Resolve("Code.exe");
        if (resolved != null) return resolved;
        // The official CLI is bin/code.cmd. Launch Code.exe directly so file arguments never pass through cmd.exe.
        foreach (var path in new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe") })
        {
            if (File.Exists(path)) return path;
        }
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            var bin = Environment.ExpandEnvironmentVariables(folder.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(bin) || !File.Exists(Path.Combine(bin, "code.cmd"))) continue;
            var path = Path.GetFullPath(Path.Combine(bin, "..", "Code.exe"));
            if (File.Exists(path)) return path;
        }
        return null;
    }
}
