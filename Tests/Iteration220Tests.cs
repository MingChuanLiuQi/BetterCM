using System.Text.Json.Nodes;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class Iteration220Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-220-" + Guid.NewGuid().ToString("N"));
    public Iteration220Tests() => Directory.CreateDirectory(_root);
    private SelectedItem Item(string name, bool directory = false)
    {
        string path = Path.Combine(_root, name); if (directory) Directory.CreateDirectory(path); else File.WriteAllText(path, "payload"); return new(path, directory);
    }
    private RenameHistoryStore Store => new(Path.Combine(_root, "history.json"));
    private (BatchRename Transaction, string Id, RenameRow Row) Completed(bool directory = false)
    {
        var row = BatchRename.Preview([Item(directory ? "folder" : "a.txt", directory)], new() { Prefix = "pre-" }).Single();
        var transaction = BatchRename.Execute([row]); return (transaction, Store.Add(transaction), row);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadedHistoryCanUndoCompletedFilesAndDirectories(bool directory)
    {
        var completed = Completed(directory); var reloaded = new RenameHistoryStore(Store.FilePath);
        Assert.Equal(completed.Id, Assert.Single(reloaded.Load()).Id); Assert.Null(reloaded.Undo(completed.Id));
        Assert.Equal("undone", Assert.Single(Store.Load()).Status); Assert.True(directory ? Directory.Exists(completed.Row.Source) : File.Exists(completed.Row.Source));
        Assert.Throws<IOException>(() => Store.Undo(completed.Id));
    }
    [Fact]
    public void ReloadedHistorySupportsSwaps()
    {
        var a = Item("a.txt"); var b = Item("b.txt");
        var rows = BatchRename.Preview([a, b], new()).Select(x => x with { Target = x.Source == a.Path ? b.Path : a.Path }).ToArray();
        var transaction = BatchRename.Execute(rows); string id = Store.Add(transaction); Assert.Null(Store.Undo(id)); Assert.True(File.Exists(a.Path)); Assert.True(File.Exists(b.Path));
    }
    [Fact]
    public void ReloadedHistorySupportsCaseOnlyRename()
    {
        var item = Item("Mixed.txt"); var rows = BatchRename.Preview([item], new() { LetterCase = "upper" });
        string id = Store.Add(BatchRename.Execute(rows)); Store.Undo(id); Assert.Equal("Mixed.txt", Path.GetFileName(Directory.GetFiles(_root, "*.txt").Single()));
    }
    [Fact]
    public void EditedContentEvenWithRestoredMetadataBlocksUndo()
    {
        var completed = Completed(); var before = FileIdentity.Read(completed.Row.Target, false);
        File.WriteAllText(completed.Row.Target, "changed"); File.SetLastWriteTimeUtc(completed.Row.Target, DateTime.FromFileTimeUtc(before.Modified));
        Assert.Throws<IOException>(() => Store.Undo(completed.Id)); Assert.Equal("changed", File.ReadAllText(completed.Row.Target)); Assert.Equal("completed", Store.Load().Single().Status);
    }
    [Fact]
    public void OccupiedOriginalNameBlocksUndoWithoutMovingFiles()
    {
        var completed = Completed(); File.WriteAllText(completed.Row.Source, "other");
        Assert.Throws<IOException>(() => Store.Undo(completed.Id)); Assert.Equal("other", File.ReadAllText(completed.Row.Source)); Assert.Equal("payload", File.ReadAllText(completed.Row.Target));
    }
    [Fact]
    public void ReplacedTargetWithMatchingMetadataIsRejected()
    {
        var completed = Completed(); var identity = FileIdentity.Read(completed.Row.Target, false);
        File.Move(completed.Row.Target, completed.Row.Target + ".old"); File.WriteAllText(completed.Row.Target, "payload");
        File.SetCreationTimeUtc(completed.Row.Target, DateTime.FromFileTimeUtc(identity.Created)); File.SetLastWriteTimeUtc(completed.Row.Target, DateTime.FromFileTimeUtc(identity.Modified));
        Assert.Throws<IOException>(() => Store.Undo(completed.Id)); Assert.False(File.Exists(completed.Row.Source));
    }
    [Theory]
    [InlineData("undoing")]
    [InlineData("interrupted")]
    [InlineData("undone")]
    public void InterruptedAndUndoneRecordsCannotBeReplayed(string status)
    {
        var completed = Completed(); var json = JsonNode.Parse(File.ReadAllText(Store.FilePath))!; json["Entries"]![0]!["Status"] = status;
        if (status == "undone") json["Entries"]![0]!["Changes"]![0]!["Current"] = completed.Row.Source;
        File.WriteAllText(Store.FilePath, json.ToJsonString()); Assert.Throws<IOException>(() => Store.Undo(completed.Id)); Assert.True(File.Exists(completed.Row.Target));
    }
    [Fact]
    public void CancellationBeforeUndoDoesNotChangeHistoryOrFiles()
    {
        var completed = Completed(); string before = File.ReadAllText(Store.FilePath);
        Assert.Throws<OperationCanceledException>(() => Store.Undo(completed.Id, new(true))); Assert.Equal(before, File.ReadAllText(Store.FilePath)); Assert.True(File.Exists(completed.Row.Target));
    }
    private sealed class CallbackProgress(Action action) : IProgress<(double Value, string Text)>
    {
        private bool _called;
        public void Report((double Value, string Text) value) { if (!_called) { _called = true; action(); } }
    }
    [Fact]
    public void CancellationDuringHashRestoresCompletedHistoryState()
    {
        var completed = Completed(); using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Store.Undo(completed.Id, cancellation.Token, new CallbackProgress(cancellation.Cancel)));
        Assert.Equal("completed", Store.Load().Single().Status); Assert.True(File.Exists(completed.Row.Target));
    }
    [Fact]
    public void FailureToPersistUndoIntentDoesNotMoveFiles()
    {
        var completed = Completed(); using var held = new FileStream(Store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var error = Assert.ThrowsAny<Exception>(() => Store.Undo(completed.Id)); Assert.True(error is IOException or UnauthorizedAccessException); Assert.True(File.Exists(completed.Row.Target)); Assert.False(File.Exists(completed.Row.Source));
    }
    [Fact]
    public void FinalHistoryWriteFailureReportsSuccessfulFileUndoWithWarning()
    {
        var completed = Completed();
        string? warning = Store.Undo(completed.Id, progress: new CallbackProgress(() => { File.Delete(Store.FilePath); Directory.CreateDirectory(Store.FilePath); }));
        Assert.Contains("文件已撤销", warning); Assert.True(File.Exists(completed.Row.Source)); Assert.False(File.Exists(completed.Row.Target));
    }
    [Fact]
    public void HistoryKeepsOnlyTwentyNewestTasksAndRemovingRecordsNeverChangesFiles()
    {
        var ids = new List<string>();
        for (int i = 0; i < 22; i++) { var rows = BatchRename.Preview([Item($"{i}.txt")], new() { Prefix = "pre-" }); ids.Add(Store.Add(BatchRename.Execute(rows))); }
        var entries = Store.Load(); Assert.Equal(20, entries.Count); Assert.Equal(ids[^1], entries[0].Id); Assert.DoesNotContain(entries, x => x.Id == ids[0]);
        Store.Remove(ids[^1]); Assert.True(File.Exists(Path.Combine(_root, "pre-21.txt"))); Store.Undo(ids[^2]); Store.ClearUndone(); Assert.Equal(18, Store.Load().Count);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("path")]
    [InlineData("hash")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    public void InvalidHistoryIsPreservedAndCannotActOnFiles(string invalid)
    {
        var completed = Completed(); var json = JsonNode.Parse(File.ReadAllText(Store.FilePath))!;
        switch (invalid)
        {
            case "version": json["Version"] = 99; break;
            case "path": json["Entries"]![0]!["Changes"]![0]!["Source"] = @"C:\outside\a.txt"; break;
            case "hash": json["Entries"]![0]!["Changes"]![0]!["Hash"] = "xyz"; break;
            case "duplicate": json["Entries"]!.AsArray().Add(json["Entries"]![0]!.DeepClone()); break;
            case "unknown": json["Unexpected"] = true; break;
        }
        string text = json.ToJsonString(); File.WriteAllText(Store.FilePath, text);
        Assert.ThrowsAny<Exception>(() => Store.Undo(completed.Id)); Assert.Equal(text, File.ReadAllText(Store.FilePath)); Assert.True(File.Exists(completed.Row.Target));
    }
    [Fact]
    public void OversizedHistoryIsRejectedBeforeDeserialization()
    {
        using (var stream = File.Create(Store.FilePath)) stream.SetLength(RenameHistoryStore.MaximumBytes + 1L);
        Assert.Throws<InvalidDataException>(() => Store.Load());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothExportFormatsProtectReservedFiles(bool actions)
    {
        string path = Path.Combine(_root, "config.json"); File.WriteAllText(path, "original");
        if (actions) await Assert.ThrowsAsync<IOException>(() => CustomActionTransfer.WriteAsync(path, [], [path.ToUpperInvariant()]));
        else await Assert.ThrowsAsync<IOException>(() => WorkspaceTransfer.WriteAsync(path, new(), [path.ToUpperInvariant()]));
        Assert.Equal("original", File.ReadAllText(path)); Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
    [Fact]
    public async Task SafeExportStillWorksAndOnlyHistoryPreferenceIsBackedUp()
    {
        Completed(); string path = Path.Combine(_root, "backup.json");
        await WorkspaceTransfer.WriteAsync(path, new() { KeepRenameHistory = false }, [Store.FilePath]); var config = await WorkspaceTransfer.ReadAsync(path);
        Assert.False(config.KeepRenameHistory); Assert.DoesNotContain("pre-a.txt", File.ReadAllText(path));
    }
    [Fact]
    public void SchemaFourMigratesHistoryPreferenceAndRetainsOtherSettings()
    {
        var config = ConfigJson.Deserialize(ConfigMigrations.Migrate("{\"SchemaVersion\":4,\"Theme\":\"dark\"}"))!;
        Assert.Equal(5, config.SchemaVersion); Assert.True(config.KeepRenameHistory); Assert.Equal("dark", config.Theme);
    }
    [Fact]
    public void ExtendedPathsAreNormalizedForPersistentUndo()
    {
        var item = Item("extended.txt"); var rows = BatchRename.Preview([new(@"\\?\" + item.Path, false)], new() { Prefix = "pre-" });
        string id = Store.Add(BatchRename.Execute(rows)); Assert.DoesNotContain(@"\\?\", Store.Load().Single().Changes[0].Source);
        Store.Undo(id); Assert.True(File.Exists(item.Path));
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, nint security);
    [Fact]
    public async Task ExportGuardRecognizesAnotherPathToTheSameProtectedFile()
    {
        string target = Path.Combine(_root, "config.json"), alias = Path.Combine(_root, "alias.json"); File.WriteAllText(target, "original");
        Assert.True(CreateHardLinkW(alias, target, 0));
        await Assert.ThrowsAsync<IOException>(() => WorkspaceTransfer.WriteAsync(alias, new(), [target]));
        Assert.Equal("original", File.ReadAllText(target)); Assert.Equal("original", File.ReadAllText(alias));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
