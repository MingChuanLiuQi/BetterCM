using System.Text;
using System.Text.Json;
using BetterCM.Config;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class CustomActionTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-transfer-" + Guid.NewGuid().ToString("N"));
    public CustomActionTransferTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private CustomActionItem Action(string title = "动作") => new()
    { Title = title, Executable = Path.Combine(_root, "missing tool.exe"), Arguments = "--open {paths}", SelectionMode = "batch", MatchExtensions = [".txt", "dir"] };
    [Fact]
    public async Task ExportAndImportRoundTripAllFieldsWithoutResolvingOrLaunchingPrograms()
    {
        var action = Action(); action.Enabled = false; action.IsPrimary = false; action.RunAsAdmin = true; action.UseTargetWorkingDirectory = true;
        string path = Path.Combine(_root, "actions.json");
        await CustomActionTransfer.WriteAsync(path, [action]);
        var copy = Assert.Single(await CustomActionTransfer.ReadAsync(path));
        Assert.Equal(ConfigJson.Serialize(new() { CustomActions = [action] }), ConfigJson.Serialize(new() { CustomActions = [copy] }));
        copy.MatchExtensions.Clear(); Assert.Equal(2, action.MatchExtensions.Count);
        Assert.False(File.Exists(action.Executable));
    }
    [Theory]
    [InlineData("Skip", 1, 0, 0, 1)]
    [InlineData("Replace", 1, 0, 1, 0)]
    [InlineData("KeepBoth", 2, 1, 0, 0)]
    public void IdConflictsFollowChosenPolicyAndNeverMutateInputs(string policyName, int count, int added, int replaced, int skipped)
    {
        var policy = Enum.Parse<CustomActionDuplicatePolicy>(policyName);
        var existing = Action("original"); var incoming = Action("imported"); incoming.Id = existing.Id;
        var result = CustomActionTransfer.Merge([existing], [incoming], policy);
        Assert.Equal(count, result.Actions.Count); Assert.Equal(added, result.Added); Assert.Equal(replaced, result.Replaced); Assert.Equal(skipped, result.Skipped);
        Assert.Equal("original", existing.Title); Assert.Equal("imported", incoming.Title);
        Assert.Equal(count, result.Actions.Select(x => x.Id).Distinct().Count());
        Assert.Equal(existing.Id, result.Actions[0].Id);
        Assert.Equal(policy == CustomActionDuplicatePolicy.Replace ? "imported" : "original", result.Actions[0].Title);
        result.Actions[0].MatchExtensions.Clear(); Assert.Equal(2, existing.MatchExtensions.Count);
    }
    [Fact]
    public void SameDefinitionIgnoresPathCaseAndExtensionOrderButHonorsElevation()
    {
        var first = Action(); var second = Action(); second.Executable = first.Executable.ToUpperInvariant(); second.MatchExtensions = [" DIR ", ".TXT"];
        Assert.Equal(1, CustomActionTransfer.Merge([first], [second], CustomActionDuplicatePolicy.Skip).Skipped);
        second.RunAsAdmin = true;
        Assert.Equal(1, CustomActionTransfer.Merge([first], [second], CustomActionDuplicatePolicy.Skip).Added);
    }
    [Fact]
    public void MergePreservesOrderingAndDetectsDuplicatesWithinIncomingFile()
    {
        var original = Action("original"); var added = Action("added"); var duplicate = Action("added");
        var result = CustomActionTransfer.Merge([original], [added, duplicate], CustomActionDuplicatePolicy.Skip);
        Assert.Equal(new[] { "original", "added" }, result.Actions.Select(x => x.Title)); Assert.Equal(1, result.Added); Assert.Equal(1, result.Skipped);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Actions\":[]}")]
    [InlineData("{\"Format\":\"BetterCM.CustomActions\",\"Version\":99,\"Actions\":[]}")]
    [InlineData("{\"Format\":\"Other\",\"Version\":1,\"Actions\":[]}")]
    [InlineData("{\"Format\":\"BetterCM.CustomActions\",\"Version\":1,\"Actions\":null}")]
    [InlineData("{\"Format\":\"BetterCM.CustomActions\",\"Version\":1,\"Actions\":[null]}")]
    [InlineData("{\"Format\":\"BetterCM.CustomActions\",\"Version\":1,\"Actions\":[],\"FutureSetting\":true}")]
    public void InvalidOrFutureDocumentsAreRejected(string json) => Assert.ThrowsAny<Exception>(() => CustomActionTransfer.Deserialize(json));
    [Theory]
    [InlineData("shell:reserved")]
    [InlineData("native:reserved")]
    [InlineData("path")]
    [InlineData("")]
    public void ReservedAndEmptyIdsCannotBeImported(string id)
    {
        var action = Action(); action.Id = id;
        Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Deserialize(JsonSerializer.Serialize(new CustomActionDocument { Actions = [action] })));
    }
    [Fact]
    public void DuplicateIdsAndRelativeEnabledProgramsAreRejected()
    {
        var first = Action(); var second = Action(); second.Id = first.Id;
        Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Serialize([first, second]));
        first.Executable = "unresolved.exe";
        Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Serialize([first]));
    }
    [Fact]
    public async Task InvalidExportDoesNotOverwriteExistingDestination()
    {
        string path = Path.Combine(_root, "keep.json"); await File.WriteAllTextAsync(path, "keep original");
        var action = Action(); action.Title = "";
        await Assert.ThrowsAsync<InvalidDataException>(() => CustomActionTransfer.WriteAsync(path, [action]));
        Assert.Equal("keep original", await File.ReadAllTextAsync(path)); Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
    [Fact]
    public async Task OversizedAndInvalidUtf8FilesAreRejected()
    {
        string path = Path.Combine(_root, "oversized.json"); await File.WriteAllBytesAsync(path, new byte[CustomActionTransfer.MaximumBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => CustomActionTransfer.ReadAsync(path));
        await File.WriteAllBytesAsync(path, [0xff, 0xff]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => CustomActionTransfer.ReadAsync(path));
        Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Deserialize(new string(' ', CustomActionTransfer.MaximumBytes + 1)));
    }
    [Fact]
    public void MergeAndExportEnforceActionLimitWithoutChangingOriginalList()
    {
        var actions = Enumerable.Range(0, CustomActionTransfer.MaximumActions).Select(i => Action("item" + i)).ToList();
        Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Merge(actions, [Action("extra")], CustomActionDuplicatePolicy.Skip));
        Assert.Equal(CustomActionTransfer.MaximumActions, actions.Count);
        actions.Add(Action("extra")); Assert.Throws<InvalidDataException>(() => CustomActionTransfer.Serialize(actions));
    }
    [Fact]
    public async Task EmptyAndBomEncodedDocumentsRoundTrip()
    {
        Assert.Empty(CustomActionTransfer.Deserialize(CustomActionTransfer.Serialize([])));
        string path = Path.Combine(_root, "bom.json");
        await File.WriteAllTextAsync(path, CustomActionTransfer.Serialize([Action()]), new UTF8Encoding(true));
        Assert.Single(await CustomActionTransfer.ReadAsync(path));
    }
}
