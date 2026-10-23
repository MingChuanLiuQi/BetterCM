using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class CustomActionPresentation
{
    private static readonly ProgramAvailability Availability = new();
    public static Task<MenuAction> CreateAsync(CustomActionItem action, Func<Task> execute, CancellationToken cancellation = default)
        => CreateAsync(action, execute, Availability.CheckAsync, cancellation);
    internal static async Task<MenuAction> CreateAsync(CustomActionItem action, Func<Task> execute,
        Func<string, CancellationToken, Task<bool?>> checkAvailability, CancellationToken cancellation = default)
    {
        bool? available = action.Enabled && Path.IsPathFullyQualified(action.Executable) && ExecutableResolver.IsProgram(action.Executable)
            ? await checkAvailability(action.Executable, cancellation) : false;
        return new()
        {
            Id = action.Id,
            Title = action.Title,
            Glyph = "\uE713",
            Enabled = available == true,
            Detail = available == true ? action.Executable : available == null ? "程序可用性检查超时或繁忙，请检查网络或介质后重新打开菜单。\n" + action.Executable : "程序不可用：可能已移动、卸载或介质未连接。请编辑程序路径后重试。\n" + action.Executable,
            Execute = available == true ? execute : null
        };
    }
}
