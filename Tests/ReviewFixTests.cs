using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class ReviewFixTests
{
    [Fact]
    public void InternalUpdatesPreserveDraftsAndMergePinChangesPerKey()
    {
        var baseline = new AppConfig { PinnedShellActions = ["remove-in-editor", "keep"] };
        var draft = baseline.Clone();
        draft.Theme = "dark"; draft.CustomActions.Add(new() { Title = "pending tool" });
        draft.PinnedShellActions.Remove("remove-in-editor"); draft.HiddenShellActions.Add("hide-in-editor");
        var saved = baseline.Clone();
        saved.ReplaceExplorerMenu = false; saved.PinnedShellActions.Add("pin-from-menu"); saved.HiddenShellActions.Add("hide-from-menu");
        var merged = SettingsRebase.Merge(baseline, draft, saved);
        Assert.Equal("dark", merged.Theme); Assert.False(merged.ReplaceExplorerMenu);
        Assert.Equal("pending tool", Assert.Single(merged.CustomActions).Title);
        Assert.True(merged.PinnedShellActions.SetEquals(["keep", "pin-from-menu"]));
        Assert.True(merged.HiddenShellActions.SetEquals(["hide-in-editor", "hide-from-menu"]));
        merged.CustomActions[0].Title = "changed";
        Assert.Equal("pending tool", draft.CustomActions[0].Title);
        Assert.Equal("system", saved.Theme);
    }
    [Theory]
    [InlineData("cut")]
    [InlineData("copy")]
    [InlineData("delete")]
    [InlineData("rename")]
    public void SelectionActionsNeverSupportFolderBackground(string id)
    {
        Assert.False(ActionCatalog.ById[id].Supports(new() { CurrentDirectory = @"C:\Fixture", ShellBackground = true }));
        Assert.True(ActionCatalog.ById[id].Supports(new() { Items = [new(@"C:\Fixture\one.txt", false)] }));
        Assert.Equal(id != "rename", ActionCatalog.ById[id].Supports(new() { Items = [new(@"C:\Fixture\one.txt", false), new(@"C:\Other\two.txt", false)] }));
    }
    [Fact]
    public void LoadedEmptyNativeGroupsAreDisabledWhileDeferredGroupsRemainAvailable()
    {
        var rows = NativeMenuComposer.Compose([new("empty", Children: []), new("deferred", Children: [], DeferredSubmenu: 7)], []);
        Assert.False(rows[0].Enabled); Assert.True(rows[1].Enabled);
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("path")]
    [InlineData("shell:collision")]
    [InlineData("native:collision")]
    public void InvalidOrReservedCustomIdsAreRejected(string id)
        => Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { CustomActions = [new() { Id = id, Title = "tool", Executable = @"C:\Tools\tool.exe" }] }));
    [Fact]
    public void DuplicateCustomIdsAreRejectedEvenWhenAnActionIsDisabled()
        => Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { CustomActions = [new() { Id = "duplicate", Title = "first", Executable = @"C:\Tools\one.exe" }, new() { Id = "duplicate", Title = "second", Executable = @"C:\Tools\two.exe", Enabled = false }] }));
    [Fact]
    public void WatcherRecognizesOwnWritesButStillDetectsExternalEdits()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-own-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ConfigStore(Path.Combine(root, "config.json"));
            Assert.True(store.Save(new())); Assert.False(store.HasDiskChanges);
            var next = store.Current.Clone(); next.PinnedShellActions.Add("pin");
            Assert.True(store.Save(next)); Assert.False(store.HasDiskChanges);
            File.WriteAllText(store.FilePath, "{ invalid"); Assert.True(store.HasDiskChanges);
            Assert.False(store.Load()); Assert.True(store.HasDiskChanges);
            File.WriteAllText(store.FilePath, ConfigJson.Serialize(next));
            Assert.True(store.Load()); Assert.False(store.HasDiskChanges);
            File.Delete(store.FilePath); Assert.True(store.HasDiskChanges);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task OfflineProgramProbesAreSharedAndBoundedWithoutBlockingCallers()
    {
        using var release = new ManualResetEventSlim();
        int probes = 0;
        var availability = new ProgramAvailability(_ => { Interlocked.Increment(ref probes); release.Wait(); return true; });
        try
        {
            var requests = Enumerable.Range(0, 12).Select(i => availability.CheckAsync("program" + i % 6)).ToArray();
            var results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.All(results, value => Assert.Null(value));
            Assert.InRange(probes, 1, 4);
        }
        finally { release.Set(); }
    }
    [Fact]
    public async Task FastProgramProbesBeyondConcurrencyLimitQueueAndSucceedWithinBudget()
    {
        var availability = new ProgramAvailability(path => path.StartsWith("fast-", StringComparison.Ordinal));
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => availability.CheckAsync("fast-" + i)));
        Assert.All(results, value => Assert.Equal(true, value));
    }
    [Theory]
    [InlineData(@"D:\Scripts\Sub\deploy.ps1", @"D:\Scripts\Sub")]
    [InlineData(@"D:\Scripts\Sub\setup.bat", @"D:\Scripts\Sub")]
    [InlineData(@"D:\Scripts\Sub\install.cmd", @"D:\Scripts\Sub")]
    [InlineData(@"D:\Scripts\Sub\package.msi", @"D:\Scripts\Sub")]
    public void ElevatedScriptsAndInstallersUseTargetDirectoryAsWorkingDirectory(string targetPath, string expectedDirectory)
    {
        var ctx = new SelectedContext { CurrentDirectory = expectedDirectory, Items = [new(targetPath, false)] };
        var start = ActionExecutor.CreateAdminStartInfo(ctx);
        Assert.Equal("runas", start.Verb);
        Assert.Equal(expectedDirectory, start.WorkingDirectory);
    }
    [Fact]
    public async Task ImmediateStaWorkerDisposalAlwaysCompletesShutdownCleanly()
    {
        for (int i = 0; i < 16; i++)
        {
            var worker = new StaWorker("BetterCM test worker " + i);
            worker.Dispose();
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }
}
