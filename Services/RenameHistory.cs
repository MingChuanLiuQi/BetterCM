using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterCM.Config;
namespace BetterCM.Services;

internal sealed record RenameHistoryChange(string Source, string Target, string Current, bool IsDirectory, FileIdentity Identity, string Hash)
{
    [JsonIgnore] public string OriginalName => Path.GetFileName(Source);
    [JsonIgnore] public string NewName => Path.GetFileName(Target);
    [JsonIgnore] public string CurrentName => Path.GetFileName(Current);
    [JsonIgnore] public string ParentDirectory => Path.GetDirectoryName(Source) ?? "";
}
internal sealed class RenameHistoryEntry
{
    [JsonRequired] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonRequired] public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.Now;
    [JsonRequired] public string Status { get; set; } = "completed";
    [JsonRequired] public List<RenameHistoryChange> Changes { get; set; } = [];
    [JsonIgnore] public string Time => CompletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    [JsonIgnore] public int Count => Changes.Count;
    [JsonIgnore] public string State => Status switch { "completed" => "可校验撤销", "undone" => "已撤销", _ => "中断，需人工检查" };
    [JsonIgnore] public string Summary => string.Join("；", Changes.Take(3).Select(x => Path.GetFileName(x.Source) + " → " + Path.GetFileName(x.Target)));
}
internal sealed class RenameHistoryDocument
{
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public List<RenameHistoryEntry> Entries { get; set; } = [];
}
internal sealed class RenameHistoryStore(string? path = null)
{
    internal const int MaximumBytes = 8 * 1024 * 1024;
    internal static string DefaultPath => Path.Combine(ConfigStore.DataDirectory, "rename-history.json");
    internal string FilePath { get; } = path ?? DefaultPath;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    internal static void ValidateChanges(IReadOnlyList<RenameHistoryChange> changes)
    {
        if (changes.Count is < 1 or > 1000) throw new InvalidDataException("历史项目数量无效");
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var currents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            if (change == null || change.Identity == null || change.Hash == null) throw new InvalidDataException("历史项目不完整");
            string source = FileSystemPaths.Normalize(change.Source), target = FileSystemPaths.Normalize(change.Target), current = FileSystemPaths.Normalize(change.Current);
            if (!source.Equals(change.Source, StringComparison.OrdinalIgnoreCase) || !target.Equals(change.Target, StringComparison.OrdinalIgnoreCase) || !current.Equals(change.Current, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(current), StringComparison.OrdinalIgnoreCase) ||
                source == target || BatchRename.NameError(Path.GetFileName(source)).Length > 0 || BatchRename.NameError(Path.GetFileName(target)).Length > 0 ||
                !sources.Add(source) || !targets.Add(target) || !currents.Add(current) || change.IsDirectory != change.Identity.Directory || change.Identity.Length < 0 ||
                (change.IsDirectory ? change.Hash.Length != 0 : change.Hash.Length != 64 || change.Hash.Any(x => !Uri.IsHexDigit(x)))) throw new InvalidDataException("历史路径或校验信息无效");
        }
        if (changes.Any(x => changes.Any(y => x != y && y.IsDirectory && ActionRules.IsWithin(x.Source, y.Source)))) throw new InvalidDataException("历史不能同时包含文件夹和内部项目");
    }
    private RenameHistoryDocument Read()
    {
        if (!File.Exists(FilePath)) { if (Directory.Exists(FilePath)) throw new IOException("历史路径被文件夹占用"); return new(); }
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("历史文件不能超过 8 MiB");
        using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = stream.Read(buffer)) > 0) { if (bytes.Length + read > MaximumBytes) throw new InvalidDataException("历史文件不能超过 8 MiB"); bytes.Write(buffer, 0, read); }
        var document = JsonSerializer.Deserialize<RenameHistoryDocument>(new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF'), Options) ?? throw new InvalidDataException("历史为空");
        if (document.Version != 1 || document.Entries == null || document.Entries.Count > 20) throw new InvalidDataException("历史格式或数量无效");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
        {
            if (entry == null || !Guid.TryParseExact(entry.Id, "N", out _) || !ids.Add(entry.Id) || entry.CompletedAt == default || entry.Status is not ("completed" or "undoing" or "undone" or "interrupted") || entry.Changes == null) throw new InvalidDataException("历史记录无效");
            ValidateChanges(entry.Changes);
            if (entry.Status == "completed" && entry.Changes.Any(x => x.Current != x.Target) || entry.Status == "undone" && entry.Changes.Any(x => x.Current != x.Source)) throw new InvalidDataException("历史状态与路径不一致");
        }
        return document;
    }
    private void Write(RenameHistoryDocument document)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("历史文件不能超过 8 MiB，请先移除旧记录");
        string target = Path.GetFullPath(FilePath); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal IReadOnlyList<RenameHistoryEntry> Load() { lock (Gate) return Read().Entries; }
    internal string Add(BatchRename transaction)
    {
        lock (Gate)
        {
            var document = Read(); var entry = new RenameHistoryEntry { Changes = transaction.Snapshot().ToList() }; ValidateChanges(entry.Changes);
            if (entry.Changes.Any(x => x.Current != x.Target)) throw new InvalidDataException("只能保存已完成的重命名任务");
            document.Entries.Insert(0, entry); if (document.Entries.Count > 20) document.Entries.RemoveRange(20, document.Entries.Count - 20);
            Write(document); return entry.Id;
        }
    }
    internal void Remove(string id) { lock (Gate) { var document = Read(); document.Entries.RemoveAll(x => x.Id == id); Write(document); } }
    internal void ClearUndone() { lock (Gate) { var document = Read(); document.Entries.RemoveAll(x => x.Status == "undone"); Write(document); } }
    // The durable undoing state prevents replay after a process interruption. File moves remain
    // protected by BatchRename's identity/hash checks; this is not a crash recovery journal.
    internal string? Undo(string id, CancellationToken cancellation = default, IProgress<(double Value, string Text)>? progress = null, BatchRename? transaction = null)
    {
        lock (Gate)
        {
            var document = Read(); var entry = document.Entries.SingleOrDefault(x => x.Id == id) ?? throw new IOException("历史记录已被移除");
            if (entry.Status != "completed") throw new IOException("该记录已撤销或处理中断，请检查文件当前位置");
            transaction ??= BatchRename.FromSnapshot(entry.Changes);
            cancellation.ThrowIfCancellationRequested(); entry.Status = "undoing"; Write(document);
            try { transaction.Undo(cancellation, progress); }
            catch (Exception failure)
            {
                entry.Changes = transaction.Snapshot().ToList(); entry.Status = entry.Changes.All(x => x.Current == x.Target) ? "completed" : "interrupted";
                try { Write(document); } catch (Exception save) { throw new IOException(failure.Message + "；历史状态保存失败，请检查文件位置：" + save.Message, failure); }
                throw;
            }
            entry.Changes = transaction.Snapshot().ToList(); entry.Status = "undone";
            try { Write(document); return null; }
            catch (Exception ex) { return "文件已撤销，但历史状态保存失败，记录将显示中断，请勿重复撤销：" + ex.Message; }
        }
    }
}
