using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class CustomActionPresentation
{
    public static MenuAction Create(CustomActionItem action, Func<Task> execute)
    {
        bool available = action.Enabled && Path.IsPathFullyQualified(action.Executable) &&
            ExecutableResolver.IsProgram(action.Executable) && File.Exists(action.Executable);
        return new()
        {
            Id = action.Id, Title = action.Title, Glyph = "\uE713", Enabled = available,
            Detail = available ? action.Executable : "程序不可用：可能已移动、卸载或介质未连接。请编辑程序路径后重试。\n" + action.Executable,
            Execute = available ? execute : null
        };
    }
}
