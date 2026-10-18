using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public sealed class ActionExecutor(ShellBridge shell)
{
    public async Task<string?> ExecuteAsync(string id, SelectedContext ctx, CancellationToken cancellation = default, IProgress<(double Value, string Text)>? progress = null, ShellResponse? existingShell = null)
    {
        if (!ActionCatalog.All.First(x => x.Id == id).Supports(ctx)) throw new InvalidOperationException("此动作不适用于当前选区");
        if (ShellCommandMap.IsNative(id))
        {
            var menu = existingShell ?? await shell.OpenAsync(ctx, cancellation);
            var command = menu.Items?.FirstOrDefault(x => x.Group.Length == 0 && x.Verb.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (!menu.Success || command is not { Enabled: true } || menu.Session == null)
                throw new InvalidOperationException(menu.Error ?? "Windows 当前未提供可用的此项操作，请重新呼出菜单");
            var result = await shell.InvokeAsync(menu.Session, command.Command);
            if (!result.Success) throw new InvalidOperationException(result.Error);
            return null;
        }
        switch (id)
        {
            case "refresh":
                var refreshed = await shell.RefreshAsync(ctx);
                if (!refreshed.Success) throw new InvalidOperationException(refreshed.Error);
                return null;
            case "path": case "slash": case "wsl": case "name":
                var text = string.Join(Environment.NewLine, ctx.TargetPaths.Select(x => id switch { "slash" => x.Replace('\\', '/'), "wsl" => PathFormatter.ToWsl(x), "name" => PathFormatter.Name(x), _ => x }));
                await ClipboardRetryAsync(() => Clipboard.SetText(text));
                return $"已复制 {ctx.TargetPaths.Count()} 个{(id == "name" ? "名称" : "路径")}";
            case "content":
                if (new FileInfo(ctx.PrimaryPath).Length > 2 * 1024 * 1024) throw new InvalidOperationException("文件超过 2 MB，请使用编辑器复制内容");
                var content = await File.ReadAllTextAsync(ctx.PrimaryPath, cancellation);
                await ClipboardRetryAsync(() => Clipboard.SetText(content));
                return "文件全文已复制";
            case "hash":
                var hash = await Task.Run(() => HashAsync(ctx.PrimaryPath, cancellation, progress), cancellation);
                await ClipboardRetryAsync(() => Clipboard.SetText(hash));
                return "SHA256 已复制：" + hash;
            case "terminal":
                var directory = ctx.IsDirectory ? ctx.PrimaryPath : ctx.CurrentDirectory;
                try { var terminal = ExecutableResolver.Resolve("wt.exe"); if (terminal == null) throw new Win32Exception(2); Start(terminal, ["-d", directory]); }
                catch (Win32Exception) { Start(ExecutableResolver.WindowsPowerShell, directory: directory); }
                return null;
            case "code": Start(ExecutableResolver.VSCode ?? throw new InvalidOperationException("未找到 VS Code，请安装后重启 BetterCM。"), ctx.TargetPaths.ToArray()); return null;
            case "notepad": Start(ExecutableResolver.SystemProgram("notepad.exe"), [ctx.PrimaryPath]); return null;
            case "reveal":
                foreach (var path in ctx.TargetPaths)
                    if (Directory.Exists(path)) Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), [path]); else RevealFile(path);
                return null;
            case "wallpaper":
                if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, ctx.PrimaryPath, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE)) throw new Win32Exception(Marshal.GetLastWin32Error());
                return "已设置桌面壁纸";
            case "admin":
                if (ctx.Extension == ".ps1") Start(ExecutableResolver.WindowsPowerShell, ["-NoProfile", "-File", ctx.PrimaryPath], admin: true);
                else if (ctx.Extension is ".bat" or ".cmd")
                {
                    string command = "& '" + ctx.PrimaryPath.Replace("'", "''") + "'";
                    Start(ExecutableResolver.WindowsPowerShell, ["-NoProfile", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command))], admin: true);
                }
                else if (ctx.Extension == ".msi") Start(ExecutableResolver.SystemProgram("msiexec.exe"), ["/i", ctx.PrimaryPath], admin: true);
                else Start(ctx.PrimaryPath, admin: true);
                return null;
            default: throw new InvalidOperationException("未知动作：" + id);
        }
    }
    public Task ExecuteCustomAsync(CustomActionItem action, SelectedContext ctx)
    {
        if (!ActionCatalog.Matches(action, ctx)) throw new InvalidOperationException("自定义动作不适用于当前选区");
        var tokens = ParseArguments(action.Arguments);
        var targets = action.SelectionMode == "each" ? ctx.TargetPaths : [ctx.PrimaryPath];
        foreach (var path in targets)
        {
            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? ctx.CurrentDirectory;
            var args = tokens.SelectMany(token => token == "{paths}" ? ctx.TargetPaths : [PathFormatter.Expand(token, path, directory)]).ToArray();
            Process.Start(CreateCustomStartInfo(action, args, directory))?.Dispose();
        }
        return Task.CompletedTask;
    }
    public static string[] ParseArguments(string arguments)
    {
        var memory = NativeMethods.CommandLineToArgvW("BetterCM " + arguments, out int count);
        if (memory == 0) throw new Win32Exception();
        try { return Enumerable.Range(1, count - 1).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size))!).ToArray(); }
        finally { NativeMethods.LocalFree(memory); }
    }
    private static void Start(string executable, string[]? arguments = null, string? directory = null, bool admin = false)
    {
        Process.Start(CreateStartInfo(executable, arguments, directory, admin))?.Dispose();
    }
    internal static ProcessStartInfo CreateCustomStartInfo(CustomActionItem action, string[] arguments, string targetDirectory)
        => CreateStartInfo(action.Executable, arguments, action.UseTargetWorkingDirectory ? targetDirectory : null, action.RunAsAdmin);
    internal static ProcessStartInfo CreateStartInfo(string executable, string[]? arguments = null, string? directory = null, bool admin = false)
    {
        ExecutableResolver.RequireAbsolute(executable);
        var start = new ProcessStartInfo(executable) { UseShellExecute = true };
        foreach (var arg in arguments ?? []) start.ArgumentList.Add(arg);
        start.WorkingDirectory = string.IsNullOrEmpty(directory) ? Path.GetDirectoryName(executable)! : directory;
        if (admin) start.Verb = "runas";
        return start;
    }
    internal static void RevealFile(string path)
    {
        Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var item, 0, out _));
        try { Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, 0, 0)); }
        finally { Marshal.FreeCoTaskMem(item); }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, nint binding, out nint item, uint attributes, out uint resultAttributes);
    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(nint item, uint count, nint children, uint flags);
    private static async Task ClipboardRetryAsync(Action action)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (ExternalException) when (attempt < 3) { await Task.Delay(35 * (attempt + 1)); }
        }
    }
    internal static async Task<string> HashAsync(string path, CancellationToken cancellation, IProgress<(double, string)>? progress)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long readTotal = 0; int read; long lastUpdate = 0;
        while ((read = await file.ReadAsync(buffer, cancellation)) > 0)
        {
            hash.AppendData(buffer, 0, read); readTotal += read;
            if (Environment.TickCount64 - lastUpdate >= 80)
            {
                lastUpdate = Environment.TickCount64;
                progress?.Report((file.Length == 0 ? 1 : (double)readTotal / file.Length, $"已读取 {readTotal / 1048576.0:F1} / {file.Length / 1048576.0:F1} MB"));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
