namespace BetterCM.Config;

public sealed class ActionPreference
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
    public string Group { get; set; } = "";
    public ActionRule Rule { get; set; } = new();
}
public sealed class CustomActionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "\"{path}\"";
    public bool RunAsAdmin { get; set; }
    public bool UseTargetWorkingDirectory { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsPrimary { get; set; } = true;
    public string SelectionMode { get; set; } = "single";
    public List<string> MatchExtensions { get; set; } = ["*"];
    public string Group { get; set; } = "";
    public ActionRule Rule { get; set; } = new();
    public List<RuntimeParameter> Parameters { get; set; } = [];
    public bool PreserveInputLiterals { get; set; }
}
public sealed class AppConfig
{
    public const int CurrentSchemaVersion = 4;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
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
    public List<FavoriteDestination> FavoriteDestinations { get; set; } = [];
    public List<MenuProfile> MenuProfiles { get; set; } = [];
    public List<RenamePreset> RenamePresets { get; set; } = [];
    public string ActiveProfileId { get; set; } = "";
    public bool AutoSelectProfile { get; set; }
    public HashSet<string> PinnedShellActions { get; set; } = [];
    public HashSet<string> HiddenShellActions { get; set; } = [];
    public AppConfig Clone()
    {
        var copy = (AppConfig)MemberwiseClone();
        copy.Actions = Actions.Select(x => new ActionPreference { Id = x.Id, Enabled = x.Enabled, IsPrimary = x.IsPrimary, Order = x.Order, Group = x.Group, Rule = x.Rule.Clone() }).ToList();
        copy.CustomActions = CustomActions.Select(x => new CustomActionItem { Id = x.Id, Title = x.Title, Executable = x.Executable, Arguments = x.Arguments, RunAsAdmin = x.RunAsAdmin, UseTargetWorkingDirectory = x.UseTargetWorkingDirectory, Enabled = x.Enabled, IsPrimary = x.IsPrimary, SelectionMode = x.SelectionMode, MatchExtensions = [.. x.MatchExtensions], Group = x.Group, Rule = x.Rule.Clone(), Parameters = x.Parameters.Select(p => p.Clone()).ToList(), PreserveInputLiterals = x.PreserveInputLiterals }).ToList();
        copy.FavoriteDestinations = FavoriteDestinations.Select(x => x.Clone()).ToList();
        copy.MenuProfiles = MenuProfiles.Select(x => x.Clone()).ToList();
        copy.RenamePresets = RenamePresets.Select(x => x.Clone()).ToList();
        copy.PinnedShellActions = new(PinnedShellActions, PinnedShellActions.Comparer);
        copy.HiddenShellActions = new(HiddenShellActions, HiddenShellActions.Comparer);
        return copy;
    }
}
public sealed class ConfigStore
{
    public static string DataDirectory => Environment.GetEnvironmentVariable("BETTERCM_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterCM");
    public string FilePath { get; }
    public AppConfig Current { get; private set; } = new();
    public string? LastError { get; private set; }
    public bool CanSave { get; private set; } = true;
    public string BackupPath => FilePath + ".bak";
    public string? InvalidCopyPath { get; private set; }
    private string? _invalidHash;
    private bool _hasDiskSnapshot;
    private string? _diskText;
    internal bool HasDiskChanges
    {
        get
        {
            if (!CanSave || !_hasDiskSnapshot) return true;
            try { return (File.Exists(FilePath) ? File.ReadAllText(FilePath) : null) != _diskText || Directory.Exists(FilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
        }
    }
    public ConfigStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(DataDirectory, "config.json");
    public bool Load()
    {
        if (!File.Exists(FilePath) && !Directory.Exists(FilePath))
        {
            _hasDiskSnapshot = true; _diskText = null;
            var legacy = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(legacy))
            {
                try
                {
                    Current = ConfigJson.Deserialize(ConfigMigrations.Migrate(File.ReadAllText(legacy), removeLegacyDefaults: true)) ?? new();
                }
                catch (Exception ex) { CanSave = false; LastError = "默认配置读取失败，已暂停保存：" + ex.Message; return false; }
            }
            CanSave = true;
            return Save(Current);
        }
        string? original = null;
        try
        {
            original = File.ReadAllText(FilePath);
            _hasDiskSnapshot = true; _diskText = original;
            var next = ConfigJson.Deserialize(ConfigMigrations.Migrate(original)) ?? throw new InvalidDataException("配置为空");
            Validate(next);
            Current = next;
            CanSave = true; LastError = null; InvalidCopyPath = null; _invalidHash = null;
            return true;
        }
        catch (Exception ex)
        {
            CanSave = false;
            string backupError = "";
            if (original != null)
            {
                try
                {
                    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original)));
                    if (hash != _invalidHash)
                    {
                        string copy = FilePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N");
                        File.WriteAllText(copy, original); InvalidCopyPath = copy; _invalidHash = hash;
                    }
                }
                catch (Exception copyError) { backupError = "；另存副本失败：" + copyError.Message; }
            }
            LastError = "配置读取失败，已暂停保存以保护原文件。请修复配置后重试，或恢复上次备份。" + ex.Message + backupError;
            return false;
        }
    }
    public bool Save(AppConfig next)
    {
        if (!CanSave) return false;
        try
        {
            if (!_hasDiskSnapshot && File.Exists(FilePath) && !Load()) return false;
            Validate(next);
            string text = ConfigJson.Serialize(next);
            WriteAtomic(text, true);
            _hasDiskSnapshot = true; _diskText = text;
            Current = next.Clone();
            LastError = null;
            return true;
        }
        catch (Exception ex) { LastError = "配置保存失败：" + ex.Message; return false; }
    }
    internal void Restore(AppConfig previous, string? originalText)
    {
        if (originalText == null)
        {
            if (File.Exists(FilePath) && File.ReadAllText(FilePath) != _diskText) throw new IOException("配置已在外部变化，未删除外部写入的文件。");
            File.Delete(FilePath);
        }
        else
        {
            WriteAtomic(originalText, false);
        }
        _hasDiskSnapshot = true; _diskText = originalText;
        Current = previous.Clone(); LastError = null;
    }
    public bool RestoreBackup()
    {
        try
        {
            string text = File.ReadAllText(BackupPath);
            var restored = ConfigJson.Deserialize(ConfigMigrations.Migrate(text)) ?? throw new InvalidDataException("备份为空");
            Validate(restored);
            // Preserve an unreadable original before an explicit recovery, including
            // recovery requested before Load has run.
            if (File.Exists(FilePath) && !Load() && InvalidCopyPath == null)
                throw new IOException("无法保留原配置副本，请先手动备份原文件。");
            text = ConfigJson.Serialize(restored);
            WriteAtomic(text, false);
            _hasDiskSnapshot = true; _diskText = text;
            Current = restored; CanSave = true; LastError = null; InvalidCopyPath = null; _invalidHash = null;
            return true;
        }
        catch (Exception ex) { LastError = "配置备份恢复失败：" + ex.Message; return false; }
    }
    private void WriteAtomic(string text, bool backup)
    {
        string destination = Path.GetFullPath(FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true)) { writer.Write(text); writer.Flush(); }
                stream.Flush(true);
            }
            string? actual = File.Exists(destination) ? File.ReadAllText(destination) : null;
            if (_hasDiskSnapshot && actual != _diskText)
                throw new IOException("配置文件已在外部变化，保存已取消。请重新读取后再编辑。");
            if (File.Exists(destination)) File.Replace(temporary, destination, backup ? BackupPath : null);
            else File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Validate(AppConfig config)
    {
        if (config.SchemaVersion != AppConfig.CurrentSchemaVersion) throw new InvalidDataException("此版本不支持该配置格式，请使用兼容的 BetterCM 版本。");
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
        BetterCM.Services.ActionRules.ValidateConfiguration(config);
        BetterCM.Services.RenamePresets.Validate(config.RenamePresets);
        var customIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in config.CustomActions)
        {
            if (string.IsNullOrWhiteSpace(action.Id) || !customIds.Add(action.Id)) throw new InvalidDataException("自定义动作 ID 不能为空或重复");
            if (BetterCM.Models.ActionCatalog.ById.ContainsKey(action.Id) || action.Id.StartsWith("shell:", StringComparison.Ordinal) || action.Id.StartsWith("native:", StringComparison.Ordinal) || action.Id.StartsWith("favorite:", StringComparison.Ordinal)) throw new InvalidDataException("自定义动作 ID 不能与内置或系统动作冲突");
            if (action is null || string.IsNullOrWhiteSpace(action.Title) || string.IsNullOrWhiteSpace(action.Executable)) throw new InvalidDataException("自定义动作需要名称和程序路径");
            if (action.Enabled && (!Path.IsPathFullyQualified(action.Executable) || !BetterCM.Services.ExecutableResolver.IsProgram(action.Executable))) throw new InvalidDataException("自定义动作需要 .exe 或 .com 的完整路径，请重新编辑该动作。");
            if (action.Arguments is null || action.MatchExtensions is null || action.MatchExtensions.Any(x => x is null)) throw new InvalidDataException("动作参数和扩展名列表不能为 null");
            BetterCM.Services.RuntimeInputs.Validate(action);
            if (action.SelectionMode is not ("single" or "each" or "batch")) throw new InvalidDataException("多选模式应为 single、each 或 batch");
            if (action.SelectionMode == "batch" && !BetterCM.Services.ActionExecutor.ParseArguments(action.Arguments).Contains("{paths}", StringComparer.Ordinal))
                throw new InvalidDataException("一次传入全部项目模式需要独立的 {paths} 参数，例如 \"{paths}\"");
        }
    }
}
