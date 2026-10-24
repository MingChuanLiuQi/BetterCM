using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BetterCM.Models;
namespace BetterCM.Services;

public sealed class RenameOptions
{
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string Find { get; set; } = "";
    public string Replace { get; set; } = "";
    public string LetterCase { get; set; } = "keep";
    public bool Numbered { get; set; }
    public int Start { get; set; } = 1;
    public int Digits { get; set; } = 3;
    public string SortOrder { get; set; } = "selection";
    public int RemoveStart { get; set; }
    public int RemoveEnd { get; set; }
    public bool TrimSpaces { get; set; }
    public RenameOptions Clone() => (RenameOptions)MemberwiseClone();
}

public sealed record RenameRow(string Source, string Target, bool IsDirectory, string Error)
{
    public string OriginalName => Path.GetFileName(Source);
    public string NewName => Error.Length > 0 && ProposedName.Length > 0 ? ProposedName : Path.GetFileName(Target);
    public bool Changed => !Source.Equals(Target, StringComparison.Ordinal);
    internal string SourceStamp { get; init; } = "";
    internal string ProposedName { get; init; } = "";
}

public sealed partial class BatchRename
{
    private sealed class Change(RenameRow row, FileIdentity identity, string hash)
    {
        public RenameRow Row { get; } = row;
        public FileIdentity Identity { get; set; } = identity;
        public string Hash { get; } = hash;
        public string Current { get; set; } = row.Source;
    }
    private readonly List<Change> _changes;
    private bool _undone;
    private BatchRename(List<Change> changes) => _changes = changes;
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedName();
    internal static string NameError(string name)
        => name.Length is 0 or > 255 || name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || ReservedName().IsMatch(name) ? "名称无效或属于 Windows 保留名称" : "";
    public static void ValidateOptions(RenameOptions options)
    {
        if (options.Prefix == null || options.Suffix == null || options.Find == null || options.Replace == null || options.Prefix.Length + options.Suffix.Length + options.Find.Length + options.Replace.Length > 4096) throw new InvalidDataException("重命名规则文本无效或过长");
        if (options.Start < 0 || options.Digits is < 1 or > 9 || options.RemoveStart is < 0 or > 255 || options.RemoveEnd is < 0 or > 255 || options.LetterCase is not ("keep" or "lower" or "upper") || options.SortOrder is not ("selection" or "name" or "name-desc" or "modified" or "modified-desc")) throw new InvalidDataException("编号、排序或名称处理选项无效");
    }
    public static IReadOnlyList<RenameRow> Preview(IReadOnlyList<SelectedItem> items, RenameOptions options, CancellationToken cancellation = default, IProgress<(double Value, string Text)>? progress = null)
    {
        if (items.Count is 0 or > 1000) throw new InvalidOperationException("每次请选择 1–1000 个文件或文件夹");
        ValidateOptions(options);
        items = RenamePresets.Sort(items, options.SortOrder, cancellation);
        var rows = new List<RenameRow>();
        for (int i = 0; i < items.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var item = items[i];
            if (!Path.IsPathFullyQualified(item.Path) || string.IsNullOrEmpty(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(item.Path)))) throw new InvalidDataException("不能重命名磁盘根目录或非绝对路径");
            string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path));
            string name = Path.GetFileName(source), extension = item.IsDirectory ? "" : Path.GetExtension(name);
            string stem = item.IsDirectory ? name : Path.GetFileNameWithoutExtension(name);
            var textElements = StringInfo.ParseCombiningCharacters(stem);
            int from = Math.Min(options.RemoveStart, textElements.Length), to = Math.Max(from, textElements.Length - options.RemoveEnd);
            stem = stem[(from == textElements.Length ? stem.Length : textElements[from])..(to == textElements.Length ? stem.Length : textElements[to])];
            if (options.TrimSpaces) stem = stem.Trim();
            if (options.Find.Length > 0) stem = stem.Replace(options.Find, options.Replace, StringComparison.Ordinal);
            stem = options.LetterCase switch { "lower" => stem.ToLowerInvariant(), "upper" => stem.ToUpperInvariant(), _ => stem };
            string numbering = options.Numbered ? checked((long)options.Start + i).ToString("D" + options.Digits, CultureInfo.InvariantCulture) : "";
            name = options.Prefix + stem + options.Suffix + numbering + extension;
            string error = NameError(name);
            string target = error.Length > 0 ? source : Path.Combine(Path.GetDirectoryName(source)!, name);
            try
            {
                var attributes = File.GetAttributes(source);
                if ((attributes & FileAttributes.ReparsePoint) != 0) error = "暂不支持符号链接和目录联接";
                if (((attributes & FileAttributes.Directory) != 0) != item.IsDirectory) error = "目标类型已变化，请重新选择";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = "无法读取原项目：" + ex.Message; }
            string stamp = "";
            if (error.Length == 0)
            {
                try { stamp = FileIdentity.Read(source, item.IsDirectory).ToString(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = "无法检查项目：" + ex.Message; }
            }
            rows.Add(new(source, target, item.IsDirectory, error) { SourceStamp = stamp, ProposedName = name });
            progress?.Report(((i + 1.0) / items.Count, $"已预览 {i + 1} / {items.Count} 项"));
        }
        var sources = rows.Select(x => x.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicates = rows.GroupBy(x => x.Target, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool repeatedSource = sources.Count != rows.Count;
        return rows.Select(row => row with
        {
            Error = row.Error.Length > 0 ? row.Error : repeatedSource ? "选区包含重复路径" : duplicates.Contains(row.Target) ? "多个项目会使用相同名称" :
                rows.Any(other => other.Source != row.Source && other.IsDirectory && ActionRules.IsWithin(row.Source, other.Source)) ? "不能同时选择文件夹及其内部项目" :
                !sources.Contains(row.Target) && Exists(row.Target) ? "目标名称已被其他项目占用" : ""
        }).ToArray();
    }
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string Hash(FileStream stream, CancellationToken cancellation, Action<int> readProgress)
    {
        using var algorithm = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024]; int read;
        while ((read = stream.Read(buffer)) > 0) { cancellation.ThrowIfCancellationRequested(); algorithm.AppendData(buffer, 0, read); readProgress(read); }
        return Convert.ToHexString(algorithm.GetHashAndReset());
    }
    private static void Move(Change change, string target, bool verify = true)
    {
        var before = FileIdentity.Read(change.Current, change.Row.IsDirectory);
        if (!change.Identity.SameObject(before) || verify && change.Identity != before) throw new IOException("项目在处理期间已变化：" + change.Current);
        if (change.Row.IsDirectory) Directory.Move(change.Current, target); else File.Move(change.Current, target, overwrite: false);
        change.Current = target;
        var after = FileIdentity.Read(target, change.Row.IsDirectory);
        // NTFS name tunneling can change CreationTime when reusing another item's former name.
        if (!before.SameObject(after) || !change.Row.IsDirectory && (before.Length != after.Length || before.Modified != after.Modified)) throw new IOException("项目在改名期间已变化：" + target);
        change.Identity = after;
    }
    private static string Temporary(Change change) => Path.Combine(Path.GetDirectoryName(change.Current)!, ".BetterCM-rename-" + Guid.NewGuid().ToString("N"));
    public static BatchRename Execute(IReadOnlyList<RenameRow> preview, CancellationToken cancellation = default, IProgress<(double Value, string Text)>? progress = null)
    {
        if (preview.Count is 0 or > 1000 || preview.Any(x => x.Error.Length > 0)) throw new InvalidOperationException("请先生成没有冲突的重命名预览");
        // Reject stale destinations before changing anything; stage all names to support swaps and case-only changes.
        var sources = preview.Select(x => x.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sources.Count != preview.Count || preview.Select(x => x.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != preview.Count) throw new IOException("重命名计划包含重复项目");
        foreach (var row in preview)
        {
            if (Path.GetDirectoryName(row.Source) != Path.GetDirectoryName(row.Target) || NameError(Path.GetFileName(row.Target)).Length > 0) throw new IOException("重命名不能改变所在目录");
            if (!sources.Contains(row.Target) && Exists(row.Target)) throw new IOException("目标名称已被占用，请重新预览：" + row.Target);
        }
        var changes = new List<Change>(); var handles = new List<FileStream>();
        try
        {
            long total = preview.Where(x => x.Changed && !x.IsDirectory).Sum(x => new FileInfo(x.Source).Length), completed = 0;
            foreach (var row in preview.Where(x => x.Changed))
            {
                cancellation.ThrowIfCancellationRequested(); var identity = FileIdentity.Read(row.Source, row.IsDirectory);
                if (row.SourceStamp != identity.ToString()) throw new IOException("项目在预览后已变化，请重新预览：" + row.Source);
                string hash = "";
                if (!row.IsDirectory)
                {
                    var stream = new FileStream(row.Source, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); handles.Add(stream);
                    hash = Hash(stream, cancellation, read => { completed += read; progress?.Report((total == 0 ? 0.8 : 0.8 * completed / total, $"内容校验 {completed / 1048576.0:F1} / {total / 1048576.0:F1} MB")); });
                    if (FileIdentity.Read(row.Source, false) != identity) throw new IOException("项目在校验期间已变化：" + row.Source);
                }
                changes.Add(new(row, identity, hash));
            }
            if (changes.Count == 0) throw new InvalidOperationException("名称没有变化");
            var transaction = new BatchRename(changes);
            try
            {
                foreach (var change in changes) { cancellation.ThrowIfCancellationRequested(); Move(change, Temporary(change)); }
                int renamed = 0;
                foreach (var change in changes) { cancellation.ThrowIfCancellationRequested(); Move(change, change.Row.Target); progress?.Report((0.8 + 0.2 * ++renamed / changes.Count, $"已重命名 {renamed} / {changes.Count} 项")); }
                return transaction;
            }
            catch (Exception failure)
            {
                try { transaction.Restore(); }
                catch (Exception rollback) { throw new IOException("重命名未完成，恢复也遇到冲突。保留的文件位置：" + string.Join("；", changes.Select(x => x.Current)) + "。" + rollback.Message, failure); }
                throw;
            }
        }
        finally { foreach (var handle in handles) handle.Dispose(); }
    }
    private void Restore()
    {
        foreach (var change in _changes.Where(x => x.Current != x.Row.Source)) Move(change, Temporary(change), verify: false);
        foreach (var change in _changes.Where(x => x.Current != x.Row.Source)) Move(change, change.Row.Source, verify: false);
    }
    public void Undo(CancellationToken cancellation = default, IProgress<(double Value, string Text)>? progress = null)
    {
        if (_undone) throw new InvalidOperationException("本次重命名已经撤销");
        var currentPaths = _changes.Select(x => x.Current).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var handles = new List<FileStream>();
        try
        {
            long total = _changes.Where(x => !x.Row.IsDirectory).Sum(x => x.Identity.Length), completed = 0;
            foreach (var change in _changes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (FileIdentity.Read(change.Current, change.Row.IsDirectory) != change.Identity) throw new IOException("项目已被修改，不能自动撤销：" + change.Current);
                if (!change.Row.IsDirectory)
                {
                    var stream = new FileStream(change.Current, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); handles.Add(stream);
                    string hash = Hash(stream, cancellation, read => { completed += read; progress?.Report((total == 0 ? 0 : (double)completed / total, $"撤销校验 {completed / 1048576.0:F1} / {total / 1048576.0:F1} MB")); });
                    if (hash != change.Hash || FileIdentity.Read(change.Current, false) != change.Identity) throw new IOException("文件内容已变化，不能撤销：" + change.Current);
                }
                if (!currentPaths.Contains(change.Row.Source) && Exists(change.Row.Source)) throw new IOException("原名称已被占用，不能撤销：" + change.Row.Source);
            }
            cancellation.ThrowIfCancellationRequested();
            var before = _changes.Select(x => x.Current).ToArray();
            try { Restore(); _undone = true; }
            catch (Exception failure)
            {
                try
                {
                    foreach (var change in _changes) Move(change, Temporary(change));
                    for (int i = 0; i < _changes.Count; i++) Move(_changes[i], before[i]);
                }
                catch (Exception recovery) { throw new IOException("撤销中断，保留的文件位置：" + string.Join("；", _changes.Select(x => x.Current)) + "。" + recovery.Message, failure); }
                throw;
            }
        }
        finally { foreach (var handle in handles) handle.Dispose(); }
    }
}
