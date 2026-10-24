using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterCM.Config;
namespace BetterCM.Services;

internal enum CustomActionDuplicatePolicy { Skip, Replace, KeepBoth }
internal sealed record CustomActionMerge(IReadOnlyList<CustomActionItem> Actions, int Added, int Replaced, int Skipped);
internal sealed class CustomActionDocument
{
    [JsonRequired] public string Format { get; set; } = "BetterCM.CustomActions";
    [JsonRequired] public int Version { get; set; } = 3;
    [JsonRequired] public List<CustomActionItem> Actions { get; set; } = [];
}
internal static class CustomActionTransfer
{
    internal const int MaximumBytes = 1024 * 1024, MaximumActions = 500;
    private static readonly JsonSerializerOptions Options = new()
    { WriteIndented = true, PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    private static List<CustomActionItem> Copy(IEnumerable<CustomActionItem> actions) => new AppConfig { CustomActions = actions.ToList() }.Clone().CustomActions;
    private static void Validate(IReadOnlyList<CustomActionItem> actions)
    {
        if (actions.Count > MaximumActions) throw new InvalidDataException("最多支持 500 个自定义动作。");
        ConfigStore.Validate(new() { CustomActions = actions.ToList() });
        foreach (var action in actions) ActionExecutor.ParseArguments(action.Arguments);
    }
    internal static string Serialize(IEnumerable<CustomActionItem> actions)
    {
        var list = actions.ToList(); Validate(list);
        string json = JsonSerializer.Serialize(new CustomActionDocument { Actions = list }, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("动作文件不能超过 1 MiB。");
        return json;
    }
    internal static IReadOnlyList<CustomActionItem> Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("动作文件不能超过 1 MiB。");
        var document = JsonSerializer.Deserialize<CustomActionDocument>(json, Options) ?? throw new InvalidDataException("动作文件不能为空。");
        if (document.Format != "BetterCM.CustomActions" || document.Version is not (1 or 2 or 3)) throw new InvalidDataException("不支持的自定义动作文件格式或版本。");
        if (document.Actions == null || document.Actions.Any(x => x == null)) throw new InvalidDataException("动作列表不能为 null，也不能包含 null。");
        if (document.Version < 3)
            foreach (var action in document.Actions.Where(x => x.Parameters?.Count == 0 && x.Arguments?.Contains("{input:", StringComparison.Ordinal) == true)) action.PreserveInputLiterals = true;
        Validate(document.Actions);
        return Copy(document.Actions);
    }
    private static bool SameDefinition(CustomActionItem first, CustomActionItem second)
        => string.Equals(first.Title, second.Title, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(first.Executable, second.Executable, StringComparison.OrdinalIgnoreCase) && first.Arguments == second.Arguments &&
        first.SelectionMode == second.SelectionMode && first.RunAsAdmin == second.RunAsAdmin && first.UseTargetWorkingDirectory == second.UseTargetWorkingDirectory && first.PreserveInputLiterals == second.PreserveInputLiterals &&
        first.Group == second.Group && JsonSerializer.Serialize(first.Rule) == JsonSerializer.Serialize(second.Rule) &&
        JsonSerializer.Serialize(first.Parameters) == JsonSerializer.Serialize(second.Parameters) &&
        new HashSet<string>(first.MatchExtensions.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase).SetEquals(second.MatchExtensions.Select(x => x.Trim()));
    internal static CustomActionMerge Merge(IEnumerable<CustomActionItem> existing, IEnumerable<CustomActionItem> incoming, CustomActionDuplicatePolicy policy)
    {
        var before = existing.ToList(); var imported = incoming.ToList(); Validate(before); Validate(imported);
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        var result = Copy(before); int added = 0, replaced = 0, skipped = 0;
        foreach (var action in imported)
        {
            int duplicate = result.FindIndex(x => x.Id == action.Id);
            if (duplicate < 0) duplicate = result.FindIndex(x => SameDefinition(x, action));
            if (duplicate >= 0 && policy == CustomActionDuplicatePolicy.Skip) { skipped++; continue; }
            var copy = Copy([action])[0];
            if (duplicate >= 0 && policy == CustomActionDuplicatePolicy.Replace)
            {
                copy.Id = result[duplicate].Id; result[duplicate] = copy; replaced++; continue;
            }
            if (result.Any(x => x.Id == copy.Id)) copy.Id = Guid.NewGuid().ToString("N");
            result.Add(copy); added++;
        }
        Validate(result);
        return new(result, added, replaced, skipped);
    }
    internal static async Task<IReadOnlyList<CustomActionItem>> ReadAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("动作文件不能超过 1 MiB。");
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new InvalidDataException("动作文件不能超过 1 MiB。");
            buffer.Write(bytes, 0, read);
        }
        return Deserialize(new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF'));
    }
    internal static async Task WriteAsync(string path, IEnumerable<CustomActionItem> actions, IEnumerable<string>? protectedPaths = null)
    {
        BackupDestinationGuard.Check(path, protectedPaths);
        string json = Serialize(actions);
        string target = Path.GetFullPath(path), temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(json)); await stream.FlushAsync(); stream.Flush(true);
            }
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
