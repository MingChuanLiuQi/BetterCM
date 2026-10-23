using BetterCM.Config;
namespace BetterCM.Models;

public sealed record ActionDefinition(string Id, string Title, string Glyph, string Shortcut, bool Primary, bool Toolbar = false)
{
    public bool Supports(SelectedContext ctx) => Id == "code" && Services.ExecutableResolver.VSCode == null ? false : Id == "refresh" ? ctx.ShellBackground && ctx.SourceWindow != 0 : Id is "cut" or "copy" or "delete" ? ctx.HasSelection : Id == "rename" ? ctx.IsSingleItem : Services.ShellCommandMap.IsNative(Id) ? ctx.HasTarget : ctx.HasFileTarget && (Id switch
    {
        "content" or "notepad" => ctx.IsText,
        "hash" => ctx.IsSingleItem && !ctx.IsDirectory,
        "wallpaper" => ctx.IsWallpaper,
        "admin" => ctx.IsExecutable,
        "wsl" => ctx.HasTarget && ctx.TargetPaths.All(PathFormatter.CanConvertToWsl),
        _ => ctx.HasTarget
    });
}
public static class ActionCatalog
{
    public static readonly HashSet<string> TextExtensions = [".txt", ".md", ".json", ".xml", ".html", ".css", ".js", ".ts", ".tsx", ".jsx", ".cs", ".py", ".cpp", ".h", ".c", ".rs", ".go", ".java", ".log", ".ini", ".yaml", ".yml", ".toml", ".ps1", ".bat", ".cmd", ".sh", ".sql", ".csv"];
    public static readonly IReadOnlyList<ActionDefinition> All = [
        new("refresh", "刷新", "\uE72C", "F5", true),
        new("cut", "剪切", "\uE8C6", "Ctrl+X", true, true),
        new("copy", "复制", "\uE8C8", "Ctrl+C", true, true),
        new("paste", "粘贴", "\uE77F", "Ctrl+V", true, true),
        new("rename", "重命名", "\uE8AC", "F2", true),
        new("delete", "删除", "\uE74D", "Delete", false),
        new("path", "复制完整路径", "\uE71B", "P", true, true),
        new("name", "复制文件名", "\uE8D2", "", true, true),
        new("properties", "属性", "\uE946", "Alt+Enter", true, true),
        new("open", "打开", "\uE8A7", "Enter", true),
        new("terminal", "在终端中打开", "\uE756", "T", true),
        new("code", "用 VS Code 打开", "\uE943", "C", true),
        new("notepad", "用记事本编辑", "\uE70F", "N", true),
        new("content", "复制文件全文", "\uE8A5", "", false),
        new("slash", "复制正斜杠路径", "\uE8AB", "U", false),
        new("wsl", "复制 WSL 路径", "\uE8AB", "", false),
        new("hash", "计算 SHA256", "\uE72E", "H", false),
        new("wallpaper", "设为桌面壁纸", "\uE91B", "", true),
        new("admin", "以管理员身份运行", "\uEA18", "", true),
        new("reveal", "在资源管理器中定位", "\uE8B7", "", false)
    ];
    public static readonly IReadOnlyDictionary<string, ActionDefinition> ById = All.ToDictionary(x => x.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, int> DefaultOrder = All.Select((action, index) => (action.Id, index)).ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);
    public static ActionPreference Preference(AppConfig config, ActionDefinition action) => config.Actions.FirstOrDefault(x => x.Id == action.Id) ?? DefaultPreference(action);
    private static ActionPreference DefaultPreference(ActionDefinition action) => new() { Id = action.Id, Enabled = true, IsPrimary = action.Primary, Order = DefaultOrder.GetValueOrDefault(action.Id) };
    public static IEnumerable<(ActionDefinition Definition, ActionPreference Preference)> Ordered(AppConfig config)
    {
        // Preserve the historical first-entry-wins behavior for duplicate preference IDs.
        var preferences = new Dictionary<string, ActionPreference>(StringComparer.Ordinal);
        foreach (var preference in config.Actions) preferences.TryAdd(preference.Id, preference);
        return All.Select(action => (Definition: action, Preference: preferences.GetValueOrDefault(action.Id) ?? DefaultPreference(action))).OrderBy(x => x.Preference.Order);
    }
    public static bool Matches(CustomActionItem action, SelectedContext ctx)
    {
        if (!action.Enabled || !ctx.HasFileTarget || (ctx.Items.Count > 1 && action.SelectionMode == "single")) return false;
        var extensions = action.MatchExtensions.Select(x => x.Trim().ToLowerInvariant()).ToHashSet();
        return extensions.Count == 0 || extensions.Contains("*") || (ctx.HasSelection ? ctx.Items.All(x => extensions.Contains(x.Extension)) : extensions.Contains("dir"));
    }
}
public static partial class PathFormatter
{
    public static bool CanConvertToWsl(string path) => path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/';
    public static string ToWsl(string path) => CanConvertToWsl(path) ? "/mnt/" + char.ToLowerInvariant(path[0]) + "/" + path[3..].Replace('\\', '/') : throw new ArgumentException("此路径无法转换为 WSL 驱动器路径");
    public static string Name(string path) => System.IO.Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : path;
    [System.Text.RegularExpressions.GeneratedRegex(@"\{(path|dir|name)\}")]
    private static partial System.Text.RegularExpressions.Regex Placeholder();
    public static string Expand(string token, string path, string directory) => Placeholder().Replace(token, match => match.Groups[1].Value switch { "path" => path, "dir" => directory, _ => Name(path) });
}
