namespace BetterCM.Config;

public sealed class ActionPreference
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}
public sealed class CustomActionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "\"{path}\"";
    public bool RunAsAdmin { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsPrimary { get; set; } = true;
    public string SelectionMode { get; set; } = "single";
    public List<string> MatchExtensions { get; set; } = ["*"];
}
public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string GlobalHotkey { get; set; } = "Alt+X";
    public bool ReplaceExplorerMenu { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool ShowFileDetails { get; set; } = true;
    public bool IncludeShellActions { get; set; } = true;
    public bool ClassicMenu { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public string Theme { get; set; } = "system";
    public string Material { get; set; } = "mica";
    public bool ShowMoreItems { get; set; } = true;
    public bool CompactMenu { get; set; }
    public int MoreHoverDelayMs { get; set; } = 180;
    public int SubmenuHoverDelayMs { get; set; } = 180;
    public string OpenAnimation { get; set; } = "fade-slide";
    public string CloseAnimation { get; set; } = "fade";
    public int OpenAnimationMs { get; set; } = 160;
    public int CloseAnimationMs { get; set; } = 70;
    public int AnimationDistance { get; set; } = 10;
    public int AnimationScalePercent { get; set; } = 96;
    public string AnimationEasing { get; set; } = "cubic";
    public bool EnableExpansionAnimations { get; set; } = true;
    public string MoreExpansionAnimation { get; set; } = "fade-slide";
    public string GroupExpansionAnimation { get; set; } = "fade-slide";
    public int MoreExpansionMs { get; set; } = 220;
    public int GroupExpansionMs { get; set; } = 180;
    public List<ActionPreference> Actions { get; set; } = [];
    public List<CustomActionItem> CustomActions { get; set; } = [];
    public HashSet<string> PinnedShellActions { get; set; } = [];
    public HashSet<string> HiddenShellActions { get; set; } = [];
    public AppConfig Clone() => ConfigJson.Deserialize(ConfigJson.Serialize(this))!;
}
public sealed class ConfigStore
{
    public static string DataDirectory => Environment.GetEnvironmentVariable("BETTERCM_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterCM");
    public string FilePath { get; }
    public AppConfig Current { get; private set; } = new();
    public string? LastError { get; private set; }
    public ConfigStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(DataDirectory, "config.json");
    public bool Load()
    {
        if (!File.Exists(FilePath))
        {
            var legacy = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(legacy))
            {
                try
                {
                    Current = ConfigJson.Deserialize(File.ReadAllText(legacy)) ?? new();
                    Current.CustomActions.RemoveAll(x => (x.Executable is "code" or "wt.exe" or "notepad.exe") && (x.Arguments is "\"{path}\"" or "-d \"{dir}\""));
                }
                catch (Exception ex) { LastError = "默认配置读取失败：" + ex.Message; return false; }
            }
            return Save(Current);
        }
        try
        {
            var next = ConfigJson.Deserialize(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("配置为空");
            Validate(next);
            Current = next;
            LastError = null;
            return true;
        }
        catch (Exception ex) { LastError = "保留当前有效配置。配置读取失败：" + ex.Message; return false; }
    }
    public bool Save(AppConfig next)
    {
        var temporary = FilePath + ".tmp";
        try
        {
            Validate(next);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, ConfigJson.Serialize(next));
            File.Move(temporary, FilePath, true);
            Current = next.Clone();
            LastError = null;
            return true;
        }
        catch (Exception ex) { LastError = "配置保存失败：" + ex.Message; return false; }
    }
    internal void Restore(AppConfig previous, string? originalText)
    {
        if (originalText == null) File.Delete(FilePath);
        else
        {
            File.WriteAllText(FilePath + ".tmp", originalText);
            File.Move(FilePath + ".tmp", FilePath, true);
        }
        Current = previous.Clone(); LastError = null;
    }
    public static void Validate(AppConfig config)
    {
        BetterCM.Services.Hotkey.Parse(config.GlobalHotkey);
        if (config.Theme is not ("system" or "dark" or "light")) throw new InvalidDataException("主题应为 system、dark 或 light");
        if (config.Material is not ("mica" or "acrylic" or "solid")) throw new InvalidDataException("材质应为 mica、acrylic 或 solid");
        if (config.MoreHoverDelayMs is < 80 or > 600) throw new InvalidDataException("更多项目悬停延时应为 80–600 毫秒");
        if (config.SubmenuHoverDelayMs is < 80 or > 600) throw new InvalidDataException("侧边子菜单悬停延时应为 80–600 毫秒");
        foreach (var style in new[] { config.OpenAnimation, config.CloseAnimation })
            if (style is not ("none" or "fade" or "slide" or "zoom" or "fade-slide" or "fade-zoom")) throw new InvalidDataException("无效的菜单动画样式");
        if (config.OpenAnimationMs is < 60 or > 400 || config.CloseAnimationMs is < 60 or > 400) throw new InvalidDataException("动画时长应为 60–400 毫秒");
        if (config.AnimationDistance is < 2 or > 32 || config.AnimationScalePercent is < 90 or > 99) throw new InvalidDataException("动画位移应为 2–32，缩放起点应为 90–99%");
        if (config.AnimationEasing is not ("cubic" or "quintic" or "sine" or "linear")) throw new InvalidDataException("无效的动画曲线");
        if (config.MoreExpansionAnimation is not ("none" or "fade" or "slide" or "fade-slide") || config.GroupExpansionAnimation is not ("none" or "fade" or "slide" or "fade-slide")) throw new InvalidDataException("无效的展开动画样式");
        if (config.MoreExpansionMs is < 60 or > 400 || config.GroupExpansionMs is < 60 or > 400) throw new InvalidDataException("展开动画时长应为 60–400 毫秒");
        if (config.Actions is null || config.CustomActions is null || config.PinnedShellActions is null || config.HiddenShellActions is null) throw new InvalidDataException("配置列表不能为 null");
        if (config.Actions.Any(x => x is null) || config.CustomActions.Any(x => x is null)) throw new InvalidDataException("动作列表不能包含 null");
        foreach (var action in config.CustomActions)
        {
            if (action is null || string.IsNullOrWhiteSpace(action.Title) || string.IsNullOrWhiteSpace(action.Executable)) throw new InvalidDataException("自定义动作需要名称和程序路径");
            if (action.Arguments is null || action.MatchExtensions is null || action.MatchExtensions.Any(x => x is null)) throw new InvalidDataException("动作参数和扩展名列表不能为 null");
            if (action.SelectionMode is not ("single" or "each" or "batch")) throw new InvalidDataException("多选模式应为 single、each 或 batch");
            if (action.SelectionMode == "batch" && !BetterCM.Services.ActionExecutor.ParseArguments(action.Arguments).Contains("{paths}", StringComparer.Ordinal))
                throw new InvalidDataException("一次传入全部项目模式需要独立的 {paths} 参数，例如 \"{paths}\"");
        }
    }
}
