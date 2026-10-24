using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterCM.Config;
using BetterCM.Models;
namespace BetterCM.Services;

internal sealed class WorkspaceDocument
{
    [JsonRequired] public string Format { get; set; } = "BetterCM.Workspace";
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public JsonElement Configuration { get; set; }
}
internal sealed class WorkspaceImportOptions
{
    public bool Restore { get; set; }
    public bool Actions { get; set; } = true;
    public bool Profiles { get; set; } = true;
    public bool Favorites { get; set; } = true;
    public bool Presets { get; set; } = true;
    public bool Settings { get; set; }
    public CustomActionDuplicatePolicy Policy { get; set; } = CustomActionDuplicatePolicy.KeepBoth;
    public string FromRoot { get; set; } = "";
    public string ToRoot { get; set; } = "";
}
internal sealed record WorkspaceMerge(AppConfig Configuration, int Added, int Replaced, int Skipped, int MappedPaths, int UnresolvedReferences);

internal static class WorkspaceTransfer
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    private static void Validate(AppConfig config)
    {
        ConfigStore.Validate(config);
        if (config.CustomActions.Count > 500 || config.Actions.Count > 1000 || config.PinnedShellActions.Count > 5000 || config.HiddenShellActions.Count > 5000) throw new InvalidDataException("备份中的动作或系统偏好数量超过限制");
        foreach (var action in config.CustomActions) ActionExecutor.ParseArguments(action.Arguments);
    }
    internal static string Serialize(AppConfig config)
    {
        Validate(config);
        string json = JsonSerializer.Serialize(new WorkspaceDocument { Configuration = JsonSerializer.SerializeToElement(config, Options) }, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("工作区备份不能超过 4 MiB");
        return json;
    }
    internal static AppConfig Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("工作区备份不能超过 4 MiB");
        var document = JsonSerializer.Deserialize<WorkspaceDocument>(json, Options) ?? throw new InvalidDataException("备份不能为空");
        if (document.Format != "BetterCM.Workspace" || document.Version != 1 || document.Configuration.ValueKind != JsonValueKind.Object) throw new InvalidDataException("不支持的工作区备份格式或版本");
        string raw = document.Configuration.GetRawText();
        _ = JsonSerializer.Deserialize<AppConfig>(raw, Options); // Reject unknown fields before migration can discard them.
        var config = JsonSerializer.Deserialize<AppConfig>(ConfigMigrations.Migrate(raw), Options) ?? throw new InvalidDataException("配置为空");
        Validate(config); return config.Clone();
    }
    internal static async Task<AppConfig> ReadAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("工作区备份不能超过 4 MiB");
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new InvalidDataException("工作区备份不能超过 4 MiB");
            buffer.Write(bytes, 0, read);
        }
        return Deserialize(new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF'));
    }
    internal static async Task WriteAsync(string path, AppConfig config, IEnumerable<string>? protectedPaths = null)
    {
        BackupDestinationGuard.Check(path, protectedPaths);
        string json = Serialize(config), target = Path.GetFullPath(path), temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            { await stream.WriteAsync(Encoding.UTF8.GetBytes(json)); await stream.FlushAsync(); stream.Flush(true); }
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static WorkspaceMerge Merge(AppConfig current, AppConfig incoming, WorkspaceImportOptions options)
    {
        Validate(incoming); if (!options.Restore) Validate(current);
        if (!Enum.IsDefined(options.Policy)) throw new InvalidDataException("重复处理方式无效");
        var source = incoming.Clone(); int mapped = 0, added = 0, replaced = 0, skipped = 0;
        if (options.FromRoot.Length > 0 || options.ToRoot.Length > 0)
        {
            string from = FileSystemPaths.Normalize(options.FromRoot), to = FileSystemPaths.Normalize(options.ToRoot);
            string Map(string path)
            {
                if (path.Length == 0 || !Path.IsPathFullyQualified(path) || !ActionRules.IsWithin(path, from)) return path;
                string suffix = Path.GetRelativePath(from, FileSystemPaths.Normalize(path)); mapped++; return suffix == "." ? to : Path.Combine(to, suffix);
            }
            if (options.Restore || options.Actions)
            {
                foreach (var action in source.CustomActions) { action.Executable = Map(action.Executable); action.Rule.DirectoryRoot = Map(action.Rule.DirectoryRoot); }
                foreach (var action in source.Actions) action.Rule.DirectoryRoot = Map(action.Rule.DirectoryRoot);
            }
            if (options.Restore || options.Profiles) foreach (var profile in source.MenuProfiles) profile.DirectoryRoot = Map(profile.DirectoryRoot);
            if (options.Restore || options.Favorites) foreach (var favorite in source.FavoriteDestinations) favorite.Path = Map(favorite.Path);
        }
        int Unresolved(AppConfig config)
        {
            var known = ActionCatalog.ById.Keys.Concat(config.CustomActions.Select(x => x.Id)).Concat(["favorite:copy", "favorite:move"]).ToHashSet(StringComparer.Ordinal);
            return config.MenuProfiles.Sum(x => x.ActionIds.Count(id => !known.Contains(id)));
        }
        if (options.Restore) { Validate(source); return new(source, source.CustomActions.Count + source.MenuProfiles.Count + source.FavoriteDestinations.Count + source.RenamePresets.Count, 0, 0, mapped, Unresolved(source)); }
        var result = current.Clone();
        if (options.Settings)
        {
            var settings = source.Clone(); settings.Actions = result.Actions; settings.CustomActions = result.CustomActions; settings.MenuProfiles = result.MenuProfiles; settings.FavoriteDestinations = result.FavoriteDestinations; settings.RenamePresets = result.RenamePresets;
            settings.ActiveProfileId = result.ActiveProfileId; settings.AutoSelectProfile = result.AutoSelectProfile; settings.PinnedShellActions = result.PinnedShellActions; settings.HiddenShellActions = result.HiddenShellActions; result = settings;
        }
        Dictionary<string, string> MergeList<T>(List<T> target, IEnumerable<T> values, Func<T, string> id, Func<T, T> copy, Action<T, string> assign)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                string original = id(value); int index = target.FindIndex(x => id(x) == original); var next = copy(value);
                if (index >= 0 && options.Policy == CustomActionDuplicatePolicy.Skip) { map[original] = original; skipped++; continue; }
                if (index >= 0 && options.Policy == CustomActionDuplicatePolicy.Replace) { target[index] = next; map[original] = original; replaced++; continue; }
                if (index >= 0) assign(next, Guid.NewGuid().ToString("N"));
                map[original] = id(next); target.Add(next); added++;
            }
            return map;
        }
        var actionMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (options.Actions)
        {
            actionMap = MergeList(result.CustomActions, source.CustomActions, x => x.Id, x => new AppConfig { CustomActions = [x] }.Clone().CustomActions[0], (x, id) => x.Id = id);
            foreach (var preference in source.Actions)
            {
                int index = result.Actions.FindIndex(x => x.Id == preference.Id);
                var copy = new AppConfig { Actions = [preference] }.Clone().Actions[0];
                if (index < 0) result.Actions.Add(copy); else if (options.Policy == CustomActionDuplicatePolicy.Replace) result.Actions[index] = copy;
            }
            if (options.Policy == CustomActionDuplicatePolicy.Replace) { result.PinnedShellActions = new(source.PinnedShellActions); result.HiddenShellActions = new(source.HiddenShellActions); }
            else { result.PinnedShellActions.UnionWith(source.PinnedShellActions); result.HiddenShellActions.UnionWith(source.HiddenShellActions); }
        }
        if (options.Profiles)
        {
            var profiles = source.MenuProfiles.Select(x => { var copy = x.Clone(); copy.ActionIds = copy.ActionIds.Select(id => actionMap.GetValueOrDefault(id, id)).ToList(); return copy; }).ToArray();
            var profileMap = MergeList(result.MenuProfiles, profiles, x => x.Id, x => x.Clone(), (x, id) => x.Id = id);
            if (source.ActiveProfileId.Length > 0) result.ActiveProfileId = profileMap[source.ActiveProfileId];
            result.AutoSelectProfile = source.AutoSelectProfile;
        }
        if (options.Favorites) MergeList(result.FavoriteDestinations, source.FavoriteDestinations, x => x.Id, x => x.Clone(), (x, id) => x.Id = id);
        if (options.Presets) MergeList(result.RenamePresets, source.RenamePresets, x => x.Id, x => x.Clone(), (x, id) => x.Id = id);
        Validate(result); return new(result, added, replaced, skipped, mapped, Unresolved(result));
    }
}
