using System.IO.Enumeration;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

public static class ActionRules
{
    public static bool IsWithin(string path, string root)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(root)) return false;
        string parent = FileSystemPaths.Normalize(root);
        string target = FileSystemPaths.Normalize(path);
        return target.Equals(parent, StringComparison.OrdinalIgnoreCase) || target.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static string Explain(ActionRule rule, SelectedContext context)
    {
        if (!context.HasFileTarget) return "当前选区不是文件系统目标";
        int count = context.Items.Count;
        if (count < rule.MinimumCount || rule.MaximumCount > 0 && count > rule.MaximumCount) return "选中数量不符合范围";
        bool target = rule.Target switch
        {
            "files" => count > 0 && context.Items.All(x => !x.IsDirectory),
            "folders" => count > 0 && context.Items.All(x => x.IsDirectory),
            "background" => count == 0 && context.ShellBackground,
            _ => true
        };
        if (!target) return "目标类型不符合规则";
        var paths = context.TargetPaths.ToArray();
        if (rule.DirectoryRoot.Length > 0 && paths.Any(x => !IsWithin(x, rule.DirectoryRoot))) return "目标不在指定目录内";
        if (paths.Any(x => !FileSystemName.MatchesSimpleExpression(rule.NamePattern, PathFormatter.Name(x), ignoreCase: true))) return "文件名不符合通配符";
        return "";
    }
    public static bool Matches(ActionRule rule, SelectedContext context) => Explain(rule, context).Length == 0;
    public static MenuProfile? SelectProfile(AppConfig config, SelectedContext context)
    {
        if (config.AutoSelectProfile && context.HasFileTarget)
        {
            var paths = context.TargetPaths.ToArray();
            var automatic = config.MenuProfiles.Where(x => x.DirectoryRoot.Length > 0 && paths.All(p => IsWithin(p, x.DirectoryRoot)))
                .OrderByDescending(x => FileSystemPaths.Normalize(x.DirectoryRoot).Length).FirstOrDefault();
            if (automatic != null) return automatic;
        }
        return config.MenuProfiles.FirstOrDefault(x => x.Id == config.ActiveProfileId);
    }
    public static bool Allows(MenuProfile? profile, string actionId) => profile == null || profile.ActionIds.Contains(actionId, StringComparer.Ordinal);
    public static void ValidateRule(ActionRule? rule, string? group)
    {
        if (rule == null || group == null || rule.NamePattern == null || rule.DirectoryRoot == null) throw new InvalidDataException("规则和分组不能为 null");
        if (rule.Target is not ("any" or "files" or "folders" or "background")) throw new InvalidDataException("未知的目标类型");
        if (rule.MinimumCount < 0 || rule.MaximumCount < 0 || rule.MaximumCount > 0 && rule.MaximumCount < rule.MinimumCount) throw new InvalidDataException("选中数量范围无效，最大值 0 表示不限");
        if (rule.NamePattern.Length is < 1 or > 260 || rule.NamePattern.IndexOfAny(['\\', '/', '\0']) >= 0) throw new InvalidDataException("文件名规则仅支持名称通配符 * 和 ?，不能包含路径");
        ValidateDirectory(rule.DirectoryRoot, optional: true);
        if (group.Length > 60 || group.Contains(" · ", StringComparison.Ordinal) || group.Any(char.IsControl)) throw new InvalidDataException("分组最多 60 字，不能包含控制字符或多层分隔符");
    }
    internal static void ValidateDirectory(string? path, bool optional = false)
    {
        if (path == null || path.Length == 0 && !optional) throw new InvalidDataException("请选择目标目录");
        if (path.Length == 0) return;
        _ = FileSystemPaths.Normalize(path);
    }
    internal static void ValidateConfiguration(AppConfig config)
    {
        foreach (var action in config.Actions) ValidateRule(action.Rule, action.Group);
        foreach (var action in config.CustomActions) ValidateRule(action.Rule, action.Group);
        if (config.FavoriteDestinations == null || config.MenuProfiles == null || config.ActiveProfileId == null) throw new InvalidDataException("收藏目录和方案列表不能为 null");
        if (config.FavoriteDestinations.Count > 50 || config.MenuProfiles.Count > 50) throw new InvalidDataException("最多支持 50 个收藏目录和 50 个菜单方案");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var favorite in config.FavoriteDestinations)
        {
            if (favorite == null || string.IsNullOrWhiteSpace(favorite.Id) || !ids.Add(favorite.Id) || string.IsNullOrWhiteSpace(favorite.Title) || favorite.Title.Length > 100) throw new InvalidDataException("收藏目录需要唯一 ID 和名称");
            ValidateDirectory(favorite.Path);
        }
        ids.Clear();
        foreach (var profile in config.MenuProfiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id) || string.IsNullOrWhiteSpace(profile.Title) || profile.Title.Length > 100 || profile.ActionIds == null || profile.ActionIds.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("菜单方案需要唯一 ID、名称和有效动作列表");
            ValidateDirectory(profile.DirectoryRoot, optional: true);
        }
        if (config.ActiveProfileId.Length > 0 && !ids.Contains(config.ActiveProfileId)) throw new InvalidDataException("当前菜单方案不存在");
    }
}
