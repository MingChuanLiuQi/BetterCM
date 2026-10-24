using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class IterationTests
{
    [Fact]
    public void CloneCoversEveryWritablePropertyIncludingFutureCustomActionFields()
    {
        var original = new AppConfig();
        FillEveryProperty(original);
        var clone = original.Clone();
        Assert.Equal(ConfigJson.Serialize(original), ConfigJson.Serialize(clone));
        AssertIndependentGraph(original, clone);
    }
    private static void FillEveryProperty(object value)
    {
        foreach (var property in value.GetType().GetProperties().Where(x => x.CanWrite))
        {
            object next = property.PropertyType switch
            {
                var type when type == typeof(string) => "non-default-" + property.Name,
                var type when type == typeof(bool) => !(bool)property.GetValue(value)!,
                var type when type == typeof(int) => (int)property.GetValue(value)! + 17,
                var type when type == typeof(List<string>) => new List<string> { "non-default-" + property.Name },
                var type when type == typeof(HashSet<string>) => new HashSet<string>(["non-default-" + property.Name], StringComparer.OrdinalIgnoreCase),
                var type when type == typeof(List<ActionPreference>) => new List<ActionPreference> { new() },
                var type when type == typeof(List<CustomActionItem>) => new List<CustomActionItem> { new() },
                var type when type == typeof(List<FavoriteDestination>) => new List<FavoriteDestination> { new() },
                var type when type == typeof(List<MenuProfile>) => new List<MenuProfile> { new() },
                var type when type == typeof(ActionRule) => new ActionRule { Target = "non-default", NamePattern = "test*", MinimumCount = 17 },
                _ => throw new NotSupportedException("Add a non-default clone fixture for " + property.DeclaringType!.Name + "." + property.Name)
            };
            if (next is System.Collections.IEnumerable sequence && next is not string)
                foreach (var item in sequence.Cast<object>().Where(x => x is not string)) FillEveryProperty(item);
            property.SetValue(value, next);
        }
    }
    private static void AssertIndependentGraph(object original, object clone)
    {
        if (original is string || original.GetType().IsValueType) { Assert.Equal(original, clone); return; }
        Assert.NotSame(original, clone);
        if (original is System.Collections.IEnumerable sequence)
        {
            var left = sequence.Cast<object>().ToArray();
            var right = ((System.Collections.IEnumerable)clone).Cast<object>().ToArray();
            Assert.Equal(left.Length, right.Length);
            for (int i = 0; i < left.Length; i++) AssertIndependentGraph(left[i], right[i]);
        }
        else
        {
            foreach (var property in original.GetType().GetProperties().Where(x => x.CanWrite))
                AssertIndependentGraph(property.GetValue(original)!, property.GetValue(clone)!);
        }
    }
    [Fact]
    public async Task DisposingAStartingStaWorkerAlwaysStopsItsThread()
    {
        for (int i = 0; i < 16; i++)
        {
            using var worker = new StaWorker("dispose regression");
            worker.Dispose();
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.Run(() => true));
        }
    }
    [Fact]
    public void InstanceNamesNormalizeCaseTrailingSeparatorAndRelativeSegments()
    {
        Assert.Equal("BetterCM", AppInstance.Name(null));
        Assert.Equal(AppInstance.Name(@"C:\Data\BetterCM"), AppInstance.Name(@"c:\data\child\..\BetterCM\"));
        Assert.NotEqual(AppInstance.Name(@"C:\Data\A"), AppInstance.Name(@"C:\Data\B"));
    }
    [Theory]
    [InlineData(@"C:\{dir}\{name}.txt", @"C:\{dir}")]
    [InlineData(@"C:\a$1\{path}.txt", @"C:\a$1")]
    [InlineData(@"\\server\{dir}\{name}.txt", @"\\server\{dir}")]
    public void InsertedPathsAreNeverExpandedAgain(string path, string directory)
    {
        Assert.Equal(path + "|" + directory + "|" + PathFormatter.Name(path) + "|{unknown}", PathFormatter.Expand("{path}|{dir}|{name}|{unknown}", path, directory));
        Assert.Equal(path + path, PathFormatter.Expand("{path}{path}", path, directory));
    }
    [Fact]
    public void PreferenceOrderingPreservesDefaultsTiesAndFirstDuplicate()
    {
        var config = new AppConfig { Actions = [new() { Id = "hash", Order = -1, Enabled = false }, new() { Id = "hash", Order = 99 }, new() { Id = "unknown", Order = -2 }] };
        var expected = ActionCatalog.All.OrderBy(x => ActionCatalog.Preference(config, x).Order).Select(x => x.Id);
        var actual = ActionCatalog.Ordered(config).ToArray();
        Assert.Equal(expected, actual.Select(x => x.Definition.Id));
        Assert.Equal("hash", actual[0].Definition.Id);
        Assert.Same(config.Actions[0], actual[0].Preference);
        Assert.False(actual[0].Preference.Enabled);
        Assert.Equal(ActionCatalog.All.Count, actual.Length);
    }
    [Fact]
    public void ClonePreservesEverySerializedSettingAndIsolatesNestedCollections()
    {
        var original = new AppConfig { Actions = [new() { Id = "path", Order = 15 }], CustomActions = [new() { Title = "test", MatchExtensions = [".txt"] }], PinnedShellActions = new(StringComparer.OrdinalIgnoreCase) { "Open" }, HiddenShellActions = ["delete"] };
        var clone = original.Clone();
        Assert.Equal(ConfigJson.Serialize(original), ConfigJson.Serialize(clone));
        Assert.Same(original.PinnedShellActions.Comparer, clone.PinnedShellActions.Comparer);
        clone.Actions[0].Enabled = false;
        clone.CustomActions[0].MatchExtensions.Add(".json");
        clone.CustomActions[0].Title = "changed";
        clone.PinnedShellActions.Clear(); clone.HiddenShellActions.Clear();
        Assert.True(original.Actions[0].Enabled);
        Assert.Single(original.CustomActions[0].MatchExtensions);
        Assert.Equal("test", original.CustomActions[0].Title);
        Assert.Single(original.PinnedShellActions); Assert.Single(original.HiddenShellActions);
    }
    [Theory]
    [InlineData("CabinetWClass", true)]
    [InlineData("Progman", true)]
    [InlineData("WorkerW", true)]
    [InlineData("Notepad", false)]
    [InlineData("", false)]
    public void ClipboardFallbackIsLimitedToOtherWindowClasses(string kind, bool explorer)
        => Assert.Equal(explorer, ExplorerContextService.UsesExplorerContext(kind));
    [Theory]
    [InlineData("--self-test")]
    [InlineData("--verb-probe")]
    [InlineData("--shell-test-host")]
    [InlineData("--preview-file")]
    [InlineData("--inspect-menu")]
    [InlineData("--performance-check")]
    public void ProductionRouterRejectsDiagnostics(string argument)
    {
        Assert.False(StartupRouter.IsSupported([argument]));
        Assert.False(StartupRouter.IsSupported([argument, "report.json"]));
    }
    [Fact]
    public void ProductionRouterValidatesHelperArityAndKnownModes()
    {
        Assert.True(StartupRouter.IsSupported([]));
        Assert.True(StartupRouter.IsSupported(["--shutdown"]));
        Assert.True(StartupRouter.IsSupported(["--background"]));
        Assert.True(StartupRouter.IsSupported(["--shell-host", "pipe"]));
        Assert.True(StartupRouter.IsSupported(["--context-host", "pipe"]));
        Assert.False(StartupRouter.IsSupported(["--shell-host"]));
        Assert.False(StartupRouter.IsSupported(["--shell-host", ""]));
        Assert.False(StartupRouter.IsSupported(["--background", "--self-test"]));
    }
    [Fact]
    public async Task CancelledCustomBatchDoesNotLaunchAProgram()
    {
        var context = new SelectedContext { CurrentDirectory = Environment.SystemDirectory, Items = [new(Path.Combine(Environment.SystemDirectory, "notepad.exe"), false)] };
        var action = new CustomActionItem { Executable = ExecutableResolver.SystemProgram("notepad.exe"), SelectionMode = "each" };
        using var shell = new ShellBridge();
        var executor = new ActionExecutor(shell);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteCustomAsync(action, context, new CancellationToken(true)));
    }
}
