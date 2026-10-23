using BetterCM.Config;
namespace BetterCM.Services;

internal sealed record CustomActionTemplate(string Id, string Title, string Description, Func<CustomActionItem> Create);
internal static class CustomActionTemplates
{
    internal static IReadOnlyList<CustomActionTemplate> All { get; } = [
        new("notepad", "记事本 · 分别打开文本文件", "选中多个文本文件时，每个文件启动一次记事本。", () => new()
        { Title = "使用记事本打开", Executable = ExecutableResolver.SystemProgram("notepad.exe"), Arguments = "\"{path}\"", SelectionMode = "each", MatchExtensions = [".txt", ".md", ".log", ".json", ".xml", ".yaml", ".yml", ".ini", ".csv"] }),
        new("code", "VS Code · 批量打开", "一次传入全部文件或文件夹；请浏览选择本机 Code.exe。", () => new()
        { Title = "使用 VS Code 打开", Executable = "Code.exe", Arguments = "{paths}", SelectionMode = "batch", MatchExtensions = ["*", "dir"] }),
        new("terminal", "Windows Terminal · 打开所在目录", "对一个文件或文件夹打开终端；请浏览选择本机 wt.exe。", () => new()
        { Title = "在终端中打开", Executable = "wt.exe", Arguments = "-d \"{dir}\"", SelectionMode = "single", MatchExtensions = ["*", "dir"] }),
        new("powershell", "PowerShell · 分别运行脚本", "使用系统 PowerShell 逐个运行 .ps1，不改变执行策略。", () => new()
        { Title = "使用 PowerShell 运行", Executable = ExecutableResolver.WindowsPowerShell, Arguments = "-NoProfile -File \"{path}\"", SelectionMode = "each", MatchExtensions = [".ps1"] })
    ];
}
