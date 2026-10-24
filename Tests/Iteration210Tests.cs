using System.Text.Json;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class Iteration210Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-210-" + Guid.NewGuid().ToString("N"));
    public Iteration210Tests() => Directory.CreateDirectory(_root);
    private SelectedItem Item(string name, string content = "payload") { string path = Path.Combine(_root, name); File.WriteAllText(path, content); return new(path, false); }
    private static AppConfig Workspace(string title = "tool")
    {
        var action = new CustomActionItem { Id = "tool", Title = title, Executable = @"C:\Old\tool.exe", Parameters = [new() { Id = "width", Title = "宽度", Kind = "number", DefaultValue = "100" }], Arguments = "\"{path}\" --width {input:width}", Rule = new() { DirectoryRoot = @"C:\Old\Data" }, Group = "工具" };
        return new() { Theme = "dark", GlobalHotkey = "Ctrl+Alt+F20", CustomActions = [action], MenuProfiles = [new() { Id = "profile", Title = "方案", DirectoryRoot = @"C:\Old", ActionIds = [action.Id, "hash"] }], ActiveProfileId = "profile", AutoSelectProfile = true, FavoriteDestinations = [new() { Id = "favorite", Title = "归档", Path = @"C:\Old\Archive" }], RenamePresets = [new() { Id = "preset", Title = "编号", Options = new() { SortOrder = "name", Numbered = true } }] };
    }
    [Theory]
    [InlineData(@"\\?\C:\Work\a.txt", @"C:\Work")]
    [InlineData(@"C:\Work\a.txt", @"\\?\C:\Work")]
    [InlineData(@"\\?\UNC\server\share\a.txt", @"\\server\share")]
    [InlineData(@"\\server\share\a.txt", @"\\?\UNC\server\share")]
    public void ExtendedAndOrdinaryFilesystemPathsHaveTheSameRuleMeaning(string path, string root)
        => Assert.True(ActionRules.IsWithin(path, root));
    [Theory]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume1")]
    [InlineData(@"C:\Work\a.txt:stream")]
    [InlineData(@"C:\Work\folder. ")]
    [InlineData(@"C:\Work\*.txt")]
    public void DeviceStreamsAndAmbiguousPathsAreRejected(string path)
        => Assert.Throws<InvalidDataException>(() => FileSystemPaths.Normalize(path));
    [Fact]
    public void ExtendedFavoritePathsAreAcceptedAndPreparedAsShellFilesystemPaths()
        => Assert.Equal(@"C:\Work\2026-10-25", FavoriteTransfer.Destination(new() { Path = @"\\?\C:\Work", DateSubfolder = true }, new(2026, 10, 25)));
    [Fact]
    public void InvalidPreviewKeepsTheActualAttemptedNameVisible()
    {
        var item = Item("report.txt"); var row = Assert.Single(BatchRename.Preview([item], new() { Prefix = "?" }));
        Assert.Equal("?report.txt", row.NewName); Assert.NotEmpty(row.Error); Assert.True(File.Exists(item.Path));
    }
    [Fact]
    public void PreviewOnlyReadsMetadataEvenWhenFileContentIsLocked()
    {
        var item = Item("a.txt");
        using var held = new FileStream(item.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var row = Assert.Single(BatchRename.Preview([item], new() { Prefix = "pre-" }));
        Assert.Empty(row.Error); Assert.Equal("pre-a.txt", row.NewName);
    }
    [Fact]
    public void ReplacedFileWithMatchingTimestampsAndLengthIsRejectedByIdentity()
    {
        var item = Item("a.txt", "same"); var plan = BatchRename.Preview([item], new() { Prefix = "pre-" });
        var created = File.GetCreationTimeUtc(item.Path); var modified = File.GetLastWriteTimeUtc(item.Path);
        string replacement = Path.Combine(_root, "replacement.txt"); File.WriteAllText(replacement, "same"); File.SetCreationTimeUtc(replacement, created); File.SetLastWriteTimeUtc(replacement, modified);
        File.Move(item.Path, item.Path + ".old"); File.Move(replacement, item.Path);
        Assert.Throws<IOException>(() => BatchRename.Execute(plan)); Assert.False(File.Exists(plan[0].Target));
    }
    private sealed class ProgressRecorder : IProgress<(double Value, string Text)>
    {
        internal List<(double Value, string Text)> Values { get; } = [];
        internal Action? OnFirstRead { get; set; }
        internal Action<(double Value, string Text)>? OnReport { get; set; }
        public void Report((double Value, string Text) value) { Values.Add(value); OnReport?.Invoke(value); if (value.Text.StartsWith("内容校验", StringComparison.Ordinal) && OnFirstRead != null) { var action = OnFirstRead; OnFirstRead = null; action(); } }
    }
    [Fact]
    public void ExecuteHashesContentOnceReportsProgressAndPinsAgainstConcurrentWrites()
    {
        var item = Item("a.txt", new string('a', 2 * 1024 * 1024)); var plan = BatchRename.Preview([item], new() { Prefix = "pre-" }); var progress = new ProgressRecorder();
        progress.OnFirstRead = () => Assert.Throws<IOException>(() => File.WriteAllText(item.Path, "concurrent overwrite"));
        var transaction = BatchRename.Execute(plan, progress: progress);
        Assert.Equal(2, progress.Values.Count(x => x.Text.StartsWith("内容校验", StringComparison.Ordinal)));
        Assert.Equal(1, progress.Values[^1].Value); transaction.Undo(); Assert.True(File.Exists(item.Path));
    }
    [Fact]
    public void CancellationAfterFirstFinalRenameRestoresBothOriginalNamesAndPayloads()
    {
        var a = Item("a.txt", "first"); var b = Item("b.txt", "second"); var rows = BatchRename.Preview([a, b], new() { Prefix = "new-" });
        using var cancellation = new CancellationTokenSource(); var progress = new ProgressRecorder { OnReport = value => { if (value.Text.StartsWith("已重命名 1", StringComparison.Ordinal)) cancellation.Cancel(); } };
        Assert.ThrowsAny<OperationCanceledException>(() => BatchRename.Execute(rows, cancellation.Token, progress));
        Assert.Equal("first", File.ReadAllText(a.Path)); Assert.Equal("second", File.ReadAllText(b.Path)); Assert.Equal(2, Directory.GetFileSystemEntries(_root).Length);
    }
    [Fact]
    public void NaturalSortDeterminesTheVisibleNumberingOrderWithoutIntegerOverflow()
    {
        var items = new[] { Item("第10集.txt"), Item("第2集.txt"), Item("第1集.txt") };
        var rows = BatchRename.Preview(items, new() { SortOrder = "name", Numbered = true, Suffix = "-", Digits = 2 });
        Assert.Equal(new[] { "第1集-01.txt", "第2集-02.txt", "第10集-03.txt" }, rows.Select(x => x.NewName));
        Assert.True(RenamePresets.CompareNames("file999999999999999999.txt", "file1000000000000000000.txt") < 0);
    }
    [Fact]
    public void ModifiedSortAndCharacterCleanupPreserveExtensions()
    {
        var a = Item("XX first ZZ.TXT"); var b = Item("XX second ZZ.TXT");
        File.SetLastWriteTimeUtc(a.Path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)); File.SetLastWriteTimeUtc(b.Path, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var rows = BatchRename.Preview([a, b], new() { SortOrder = "modified-desc", RemoveStart = 2, RemoveEnd = 2, TrimSpaces = true, Numbered = true, Digits = 1 });
        Assert.Equal(new[] { "second1.TXT", "first2.TXT" }, rows.Select(x => x.NewName));
    }
    [Fact]
    public void CharacterRemovalDoesNotSplitEmojiOrCombiningCharacters()
    {
        var item = Item("😀e\u0301file🧡.txt");
        var row = Assert.Single(BatchRename.Preview([item], new() { RemoveStart = 2, RemoveEnd = 1 }));
        Assert.Equal("file.txt", row.NewName); Assert.Empty(row.Error);
    }
    [Fact]
    public void OptionalChoiceKeepsAnEmptyDefaultAndUndoCancellationDoesNotChangeNames()
    {
        var action = new CustomActionItem { Parameters = [new() { Id = "mode", Title = "模式", Required = false, Kind = "choice", Choices = ["A", "B"] }] };
        Assert.Equal("", RuntimeInputs.Values(action, null, preview: true)["mode"]);
        var item = Item("a.txt"); var plan = BatchRename.Preview([item], new() { Prefix = "new-" }); var transaction = BatchRename.Execute(plan);
        using var cancellation = new CancellationTokenSource(); var progress = new ProgressRecorder { OnReport = _ => cancellation.Cancel() };
        Assert.ThrowsAny<OperationCanceledException>(() => transaction.Undo(cancellation.Token, progress)); Assert.True(File.Exists(plan[0].Target)); Assert.False(File.Exists(item.Path));
    }
    [Fact]
    public void WorkspaceRoundTripPreservesAllSettingsAndNestedParameterAndPresetLists()
    {
        var original = Workspace(); var copy = WorkspaceTransfer.Deserialize(WorkspaceTransfer.Serialize(original));
        Assert.Equal(ConfigJson.Snapshot(original), ConfigJson.Snapshot(copy));
        copy.CustomActions[0].Parameters[0].Choices.Add("independent"); copy.MenuProfiles[0].ActionIds.Clear(); copy.RenamePresets[0].Options.Prefix = "changed";
        Assert.Empty(original.CustomActions[0].Parameters[0].Choices); Assert.Equal(2, original.MenuProfiles[0].ActionIds.Count); Assert.Equal("", original.RenamePresets[0].Options.Prefix);
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void WorkspaceMergeRewiresTheImportedActiveProfileToTheCorrectAction(int value, int count)
    {
        var policy = (CustomActionDuplicatePolicy)value;
        var current = Workspace("existing"); var incoming = Workspace("imported"); string before = ConfigJson.Snapshot(current);
        var merged = WorkspaceTransfer.Merge(current, incoming, new() { Policy = policy });
        Assert.Equal(count, merged.Configuration.CustomActions.Count); Assert.Equal(count, merged.Configuration.MenuProfiles.Count);
        var profile = merged.Configuration.MenuProfiles.Single(x => x.Id == merged.Configuration.ActiveProfileId);
        var action = merged.Configuration.CustomActions.Single(x => x.Id == profile.ActionIds[0]);
        Assert.Equal(policy == CustomActionDuplicatePolicy.Skip ? "existing" : "imported", action.Title);
        if (policy == CustomActionDuplicatePolicy.KeepBoth) Assert.NotEqual("tool", action.Id);
        Assert.Equal(before, ConfigJson.Snapshot(current));
    }
    [Fact]
    public void SelectiveImportPreservesUnselectedSettingsAndMapsOnlySelectedPaths()
    {
        var current = new AppConfig { Theme = "light" }; var incoming = Workspace();
        var merged = WorkspaceTransfer.Merge(current, incoming, new() { Profiles = false, Favorites = false, Presets = false, Settings = false, FromRoot = @"C:\Old", ToRoot = @"D:\New" });
        Assert.Equal("light", merged.Configuration.Theme); Assert.Empty(merged.Configuration.MenuProfiles); Assert.Empty(merged.Configuration.FavoriteDestinations); Assert.Empty(merged.Configuration.RenamePresets);
        Assert.Equal(@"D:\New\tool.exe", merged.Configuration.CustomActions[0].Executable); Assert.Equal(@"D:\New\Data", merged.Configuration.CustomActions[0].Rule.DirectoryRoot); Assert.Equal(2, merged.MappedPaths);
    }
    [Fact]
    public void WholeRestoreIncludesSettingsAndPathMappingKeepsDirectoryBoundaries()
    {
        var incoming = Workspace(); incoming.FavoriteDestinations.Add(new() { Title = "outside", Path = @"C:\Older\Data" });
        var result = WorkspaceTransfer.Merge(new(), incoming, new() { Restore = true, FromRoot = @"\\?\C:\Old", ToRoot = @"D:\New" });
        Assert.Equal("Ctrl+Alt+F20", result.Configuration.GlobalHotkey); Assert.Equal(@"D:\New", result.Configuration.MenuProfiles[0].DirectoryRoot);
        Assert.Equal(@"D:\New\Archive", result.Configuration.FavoriteDestinations[0].Path); Assert.Equal(@"C:\Older\Data", result.Configuration.FavoriteDestinations[1].Path);
    }
    [Fact]
    public void WholeRestoreCanReplaceAnInvalidUnsavedDraft()
    {
        var draft = new AppConfig { GlobalHotkey = "invalid hotkey" };
        var result = WorkspaceTransfer.Merge(draft, Workspace(), new() { Restore = true });
        Assert.Equal("Ctrl+Alt+F20", result.Configuration.GlobalHotkey); ConfigStore.Validate(result.Configuration);
        Assert.Throws<InvalidDataException>(() => RuntimeInputs.Validate(new() { Arguments = "{input:missing" }));
    }
    [Fact]
    public void BackupKeepsUnresolvedReferencesVisibleAndRejectsInvalidCountsOrFutureSchema()
    {
        var incoming = Workspace(); incoming.MenuProfiles[0].ActionIds.Add("missing-action");
        Assert.Equal(1, WorkspaceTransfer.Merge(new(), incoming, new()).UnresolvedReferences);
        string json = WorkspaceTransfer.Serialize(incoming);
        Assert.Throws<InvalidDataException>(() => WorkspaceTransfer.Deserialize(json.Replace("\"SchemaVersion\": " + AppConfig.CurrentSchemaVersion, "\"SchemaVersion\": 99", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => WorkspaceTransfer.Deserialize(json.Replace("\"Format\":", "\"Unknown\": true, \"Format\":", StringComparison.Ordinal)));
    }
    [Fact]
    public async Task BackupReadWriteAreBoundedAndInvalidExportPreservesExistingFile()
    {
        string path = Path.Combine(_root, "backup.json"); await WorkspaceTransfer.WriteAsync(path, Workspace());
        Assert.Equal("tool", (await WorkspaceTransfer.ReadAsync(path)).CustomActions[0].Title);
        var invalid = Workspace(); invalid.CustomActions[0].Title = ""; string before = await File.ReadAllTextAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => WorkspaceTransfer.WriteAsync(path, invalid)); Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Throws<InvalidDataException>(() => WorkspaceTransfer.Deserialize(new string(' ', WorkspaceTransfer.MaximumBytes + 1)));
        await File.WriteAllBytesAsync(path, [0xFF, 0xFE, 0xFF]); await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(() => WorkspaceTransfer.ReadAsync(path));
    }
    [Fact]
    public void SchemaThreeAddsEmptyParametersAndPresetsWithoutChangingLegacySettings()
    {
        var old = new AppConfig { SchemaVersion = 3, Theme = "light" };
        var migrated = ConfigJson.Deserialize(ConfigMigrations.Migrate(ConfigJson.Serialize(old)))!;
        Assert.Equal(AppConfig.CurrentSchemaVersion, migrated.SchemaVersion); Assert.Empty(migrated.RenamePresets); Assert.Equal("light", migrated.Theme);
    }
    [Fact]
    public void RuntimeInputExpansionIsSinglePassAndPreservesOneIndependentArgument()
    {
        var action = new CustomActionItem { Title = "tool", Executable = @"C:\Tools\tool.exe", Arguments = "--name=\"{input:name}\" \"{path}\"", Parameters = [new() { Id = "name", Title = "名称" }] };
        var context = new SelectedContext { Items = [new(@"C:\Data\a {input:name}.txt", false)] };
        var values = new Dictionary<string, string> { ["name"] = "space & {path} \"quote\"" };
        var plan = new CustomActionPlan(action, context, values, previewInputs: false); values["name"] = "changed";
        Assert.Equal(new[] { "--name=space & {path} \"quote\"", @"C:\Data\a {input:name}.txt" }, plan.Launch(0).Arguments);
    }
    [Theory]
    [InlineData("number", "11", false)]
    [InlineData("number", "5", true)]
    [InlineData("number", "1.5", false)]
    [InlineData("choice", "B", true)]
    [InlineData("choice", "C", false)]
    [InlineData("text", "", false)]
    [InlineData("text", "hello", true)]
    public void RuntimeValuesValidateRequiredChoicesAndIntegerRanges(string kind, string value, bool valid)
    {
        var action = new CustomActionItem { Parameters = [new() { Id = "value", Title = "值", Kind = kind, Minimum = 1, Maximum = 10, Choices = ["A", "B"] }], Arguments = "{input:value}" };
        if (valid) Assert.Equal(value, RuntimeInputs.Values(action, new Dictionary<string, string> { ["value"] = value }, false)["value"]);
        else Assert.Throws<InvalidDataException>(() => RuntimeInputs.Values(action, new Dictionary<string, string> { ["value"] = value }, false));
    }
    [Fact]
    public void UndefinedDuplicateAndOversizedRuntimeInputsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => RuntimeInputs.Validate(new() { Arguments = "{input:missing}" }));
        var action = new CustomActionItem { Parameters = [new() { Id = "value" }, new() { Id = "value" }] };
        Assert.Throws<InvalidDataException>(() => RuntimeInputs.Validate(action)); action.Parameters.RemoveAt(1);
        Assert.Throws<InvalidDataException>(() => RuntimeInputs.Values(action, new Dictionary<string, string> { ["value"] = new string('x', 1025) }, false));
    }
    [Fact]
    public void LegacyInputLikeLiteralsKeepTheirMeaningDuringConfigAndActionImport()
    {
        var action = new CustomActionItem { Title = "legacy", Executable = @"C:\Tools\tool.exe", Arguments = "\"{input:old}\" \"{path}\"" };
        string old = ConfigJson.Serialize(new() { SchemaVersion = 3, CustomActions = [action] });
        var migrated = ConfigJson.Deserialize(ConfigMigrations.Migrate(old))!; ConfigStore.Validate(migrated);
        var imported = CustomActionTransfer.Deserialize(JsonSerializer.Serialize(new CustomActionDocument { Version = 2, Actions = [action] })).Single();
        Assert.True(imported.PreserveInputLiterals); Assert.True(migrated.CustomActions[0].PreserveInputLiterals);
        var plan = new CustomActionPlan(imported, new() { Items = [new(@"C:\Data\file.txt", false)] });
        Assert.Equal("{input:old}", plan.Launch(0).Arguments[0]);
    }
    [Fact]
    public void ActionExchangePreservesRuntimeDefinitionsAndReadsOlderVersions()
    {
        var action = Workspace().CustomActions[0];
        var copy = Assert.Single(CustomActionTransfer.Deserialize(CustomActionTransfer.Serialize([action])));
        Assert.Equal("number", copy.Parameters[0].Kind); Assert.Equal("100", copy.Parameters[0].DefaultValue);
        Assert.Empty(CustomActionTransfer.Deserialize("""{"Format":"BetterCM.CustomActions","Version":2,"Actions":[]}"""));
    }
    public void Dispose() => Directory.Delete(_root, true);
}
