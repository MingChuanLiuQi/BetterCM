using System.Text;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

internal sealed record CustomActionLaunch(string[] Arguments, string TargetDirectory, string WorkingDirectory);

// Preview and execution share this immutable, filesystem-free argument plan.
internal sealed class CustomActionPlan
{
    private readonly CustomActionItem _action;
    private readonly SelectedItem[] _targets;
    private readonly string[] _paths, _tokens;
    internal string Executable => _action.Executable;
    internal bool RunAsAdmin => _action.RunAsAdmin;
    internal int Count => _action.SelectionMode == "each" ? _targets.Length : 1;
    internal CustomActionPlan(CustomActionItem action, SelectedContext context)
    {
        ConfigStore.Validate(new() { CustomActions = [action] });
        if (!Path.IsPathFullyQualified(action.Executable) || !ExecutableResolver.IsProgram(action.Executable))
            throw new InvalidOperationException("请先选择 .exe 或 .com 的完整路径，再预览参数。可点击“浏览…”选择程序。");
        if (!ActionCatalog.Matches(action, context)) throw new InvalidOperationException("动作未启用或不适用于当前选区，请检查文件类型和多选方式。");
        _action = new AppConfig { CustomActions = [action] }.Clone().CustomActions[0];
        _targets = context.HasSelection ? context.Items.Select(x => new SelectedItem(x.Path, x.IsDirectory)).ToArray() : [new(context.CurrentDirectory, true)];
        _paths = _targets.Select(x => x.Path).ToArray();
        _tokens = ActionExecutor.ParseArguments(action.Arguments);
    }
    internal CustomActionLaunch Launch(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        var target = _targets[_action.SelectionMode == "each" ? index : 0];
        string directory = target.IsDirectory ? target.Path : Path.GetDirectoryName(target.Path) ?? "";
        var arguments = _tokens.SelectMany(token => token == "{paths}" ? _paths : [PathFormatter.Expand(token, target.Path, directory)]).ToArray();
        return new(arguments, directory, _action.UseTargetWorkingDirectory ? directory : Path.GetDirectoryName(Executable)!);
    }
    internal string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine("程序：" + Short(Executable));
        text.AppendLine($"预计启动：{Count} 次；管理员运行：{(RunAsAdmin ? "是" : "否")}");
        text.AppendLine("每一行 [序号] 都是一项独立参数。这里只预览，不会启动程序。");
        for (int index = 0; index < Math.Min(Count, 3); index++)
        {
            var launch = Launch(index);
            text.AppendLine($"\n第 {index + 1} 次 · 工作目录：{Short(launch.WorkingDirectory)}");
            if (launch.Arguments.Length == 0) text.AppendLine("  （不传入参数）");
            for (int arg = 0; arg < Math.Min(launch.Arguments.Length, 40); arg++)
                text.AppendLine($"  [{arg + 1}] {Short(launch.Arguments[arg])}");
            if (launch.Arguments.Length > 40) text.AppendLine($"  …另有 {launch.Arguments.Length - 40} 项参数未展示。");
        }
        if (Count > 3) text.AppendLine($"\n…另有 {Count - 3} 次启动未展示。");
        return text.ToString();
    }
    private static string Short(string value)
    {
        value = value.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
        return value.Length <= 512 ? value : value[..512] + "…（仅显示前 512 字符）";
    }
}
