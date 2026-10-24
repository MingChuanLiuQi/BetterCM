using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class BatchRenameTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-rename-test-" + Guid.NewGuid().ToString("N"));
    public BatchRenameTests() => Directory.CreateDirectory(_root);
    private SelectedItem FileItem(string name, string text = "payload") { string path = Path.Combine(_root, name); File.WriteAllText(path, text); return new(path, false); }
    [Fact]
    public void PreviewCombinesRulesAndPreservesTheExtension()
    {
        var item = FileItem("Report old.TXT");
        var row = Assert.Single(BatchRename.Preview([item], new() { Find = "old", Replace = "NEW", LetterCase = "lower", Prefix = "P-", Suffix = "-S", Numbered = true, Start = 7, Digits = 3 }));
        Assert.Equal("P-report new-S007.TXT", row.NewName); Assert.Equal("", row.Error); Assert.True(File.Exists(item.Path));
    }
    [Theory]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("COM1.log")]
    [InlineData("LPT²")]
    [InlineData("bad.")]
    [InlineData("bad ")]
    [InlineData("a/b")]
    [InlineData("..")]
    public void InvalidNamesAreRejected(string name) => Assert.NotEmpty(BatchRename.NameError(name));
    [Fact]
    public void PreviewReportsCollisionsAndOverlapsBeforeAnyMutation()
    {
        var a = FileItem("a.txt"); var b = FileItem("aa.txt");
        Assert.All(BatchRename.Preview([a, b], new() { Find = "a", Replace = "" }), x => Assert.NotEmpty(x.Error));
        var existing = FileItem("pre-a.txt");
        Assert.NotEmpty(Assert.Single(BatchRename.Preview([a], new() { Prefix = "pre-" })).Error);
        var directory = new SelectedItem(Path.Combine(_root, "dir"), true); Directory.CreateDirectory(directory.Path);
        string child = Path.Combine(directory.Path, "child.txt"); File.WriteAllText(child, "inside");
        Assert.Contains(BatchRename.Preview([directory, new(child, false)], new() { Prefix = "pre-" }), x => x.Error.Length > 0);
        Assert.Equal("payload", File.ReadAllText(a.Path)); Assert.True(File.Exists(existing.Path));
    }
    [Fact]
    public void RenameAndUndoHandleUnicodeBracesAndCaseOnlyChanges()
    {
        var item = FileItem("报告 {dir} A.txt", "original");
        var rows = BatchRename.Preview([item], new() { LetterCase = "lower" });
        var transaction = BatchRename.Execute(rows);
        Assert.Equal("original", File.ReadAllText(rows[0].Target));
        transaction.Undo(); Assert.Equal("original", File.ReadAllText(item.Path));
        Assert.Throws<InvalidOperationException>(() => transaction.Undo());
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), x => Path.GetFileName(x).StartsWith(".BetterCM-", StringComparison.Ordinal));
    }
    [Fact]
    public void SwappingNamesPreservesBothPayloadsAndCanBeUndone()
    {
        var a = FileItem("A.txt", "A"); var b = FileItem("B.txt", "B");
        var rows = BatchRename.Preview([a, b], new());
        var swap = new[] { rows[0] with { Target = b.Path }, rows[1] with { Target = a.Path } };
        var transaction = BatchRename.Execute(swap);
        Assert.Equal("B", File.ReadAllText(a.Path)); Assert.Equal("A", File.ReadAllText(b.Path));
        transaction.Undo(); Assert.Equal("A", File.ReadAllText(a.Path)); Assert.Equal("B", File.ReadAllText(b.Path));
    }
    [Fact]
    public void StalePreviewAndNewDestinationRefuseWithoutOverwriting()
    {
        var item = FileItem("a.txt"); var preview = BatchRename.Preview([item], new() { Prefix = "pre-" });
        File.WriteAllText(item.Path, "modified after preview");
        Assert.Throws<IOException>(() => BatchRename.Execute(preview)); Assert.False(File.Exists(preview[0].Target));
        preview = BatchRename.Preview([item], new() { Prefix = "pre-" }); File.WriteAllText(preview[0].Target, "external");
        Assert.Throws<IOException>(() => BatchRename.Execute(preview)); Assert.Equal("external", File.ReadAllText(preview[0].Target));
    }
    [Fact]
    public void UndoRefusesModifiedFileEvenIfLengthAndTimestampAreRestored()
    {
        var item = FileItem("a.txt", "first"); var rows = BatchRename.Preview([item], new() { Prefix = "pre-" }); var transaction = BatchRename.Execute(rows);
        var time = File.GetLastWriteTimeUtc(rows[0].Target); File.WriteAllText(rows[0].Target, "other"); File.SetLastWriteTimeUtc(rows[0].Target, time);
        Assert.Throws<IOException>(() => transaction.Undo()); Assert.Equal("other", File.ReadAllText(rows[0].Target)); Assert.False(File.Exists(item.Path));
    }
    [Fact]
    public void UndoRefusesOccupiedOriginalWithoutTouchingAnyFile()
    {
        var item = FileItem("a.txt", "before"); var rows = BatchRename.Preview([item], new() { Prefix = "pre-" }); var transaction = BatchRename.Execute(rows);
        File.WriteAllText(item.Path, "external"); Assert.Throws<IOException>(() => transaction.Undo());
        Assert.Equal("external", File.ReadAllText(item.Path)); Assert.Equal("before", File.ReadAllText(rows[0].Target));
    }
    [Fact]
    public void StagingFailureRollsBackEarlierRenamesWithoutLeavingTemporaryFiles()
    {
        var first = FileItem("first.txt", "first"); var second = FileItem("second.txt", "second");
        var preview = BatchRename.Preview([first, second], new() { Prefix = "pre-" });
        using (var held = new FileStream(second.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => BatchRename.Execute(preview));
        Assert.Equal("first", File.ReadAllText(first.Path)); Assert.Equal("second", File.ReadAllText(second.Path));
        Assert.Equal(2, Directory.GetFileSystemEntries(_root).Length);
    }
    [Fact]
    public void CancellationBeforeStartKeepsOriginalNames()
    {
        var item = FileItem("a.txt"); var rows = BatchRename.Preview([item], new() { Prefix = "pre-" });
        Assert.ThrowsAny<OperationCanceledException>(() => BatchRename.Execute(rows, new CancellationToken(true))); Assert.True(File.Exists(item.Path)); Assert.False(File.Exists(rows[0].Target));
    }
    [Fact]
    public void DirectoriesKeepTheirExtensionsAndChildren()
    {
        string path = Path.Combine(_root, "folder.ext"); Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "child.txt"), "inside");
        var row = Assert.Single(BatchRename.Preview([new(path, true)], new() { Suffix = "-new" }));
        Assert.Equal("folder.ext-new", row.NewName); var transaction = BatchRename.Execute([row]);
        Assert.Equal("inside", File.ReadAllText(Path.Combine(row.Target, "child.txt"))); transaction.Undo(); Assert.True(Directory.Exists(path));
    }
    public void Dispose() => Directory.Delete(_root, true);
}
