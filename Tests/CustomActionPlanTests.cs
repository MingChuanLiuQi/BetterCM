using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class CustomActionPlanTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "BetterCM plan");
    private static CustomActionItem Action(string mode = "single") => new()
    { Title = "preview", Executable = Path.Combine(Root, "tools", "missing.exe"), SelectionMode = mode, Arguments = mode == "batch" ? "--open {paths}" : "\"{path}\" \"{dir}\" \"{name}\"" };
    private static SelectedContext Context(int count = 1) => new()
    {
        CurrentDirectory = Root,
        Items = Enumerable.Range(0, count).Select(i => new SelectedItem(Path.Combine(Root, $"file {i} {{dir}}.txt"), false)).ToArray()
    };
    [Theory]
    [InlineData("single", 1, 1)]
    [InlineData("each", 2, 2)]
    [InlineData("batch", 2, 1)]
    public void PreviewUsesIndependentArgumentsAndExpectedLaunchCount(string mode, int selected, int launches)
    {
        var context = Context(selected); var plan = new CustomActionPlan(Action(mode), context);
        Assert.Equal(launches, plan.Count);
        if (mode == "batch") Assert.Equal(new[] { "--open" }.Concat(context.TargetPaths), plan.Launch(0).Arguments);
        else
            for (int i = 0; i < launches; i++)
                Assert.Equal(new[] { context.Items[i].Path, Root, context.Items[i].Name }, plan.Launch(i).Arguments);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkingDirectoryMatchesLaunchPolicyWithoutCheckingTargetExistence(bool useTarget)
    {
        var action = Action(); action.UseTargetWorkingDirectory = useTarget;
        string folder = Path.Combine(Root, "nonexistent directory");
        var plan = new CustomActionPlan(action, new() { Items = [new(folder, true)] });
        Assert.Equal(folder, plan.Launch(0).TargetDirectory);
        Assert.Equal(useTarget ? folder : Path.GetDirectoryName(action.Executable), plan.Launch(0).WorkingDirectory);
        Assert.Equal(folder, plan.Launch(0).Arguments[1]);
    }
    [Fact]
    public void BackgroundAndMixedDirectoryBatchUseCapturedTargets()
    {
        var background = new CustomActionPlan(Action(), new() { CurrentDirectory = Root });
        Assert.Equal(Root, background.Launch(0).Arguments[0]);
        var context = new SelectedContext { Items = [new(Path.Combine(Root, "a.txt"), false), new(Path.Combine(Root, "sub folder"), true)] };
        Assert.Equal(new[] { "--open" }.Concat(context.TargetPaths), new CustomActionPlan(Action("batch"), context).Launch(0).Arguments);
    }
    [Fact]
    public void PlanSnapshotsActionAndSelectionBeforeAnyLaunch()
    {
        var action = Action("each"); var items = Context(2).Items.ToList();
        var plan = new CustomActionPlan(action, new() { Items = items });
        action.Arguments = "changed"; action.Executable = "changed.exe"; action.RunAsAdmin = true; items.Clear();
        Assert.Equal(2, plan.Count); Assert.False(plan.RunAsAdmin);
        Assert.EndsWith("missing.exe", plan.Executable); Assert.Equal(3, plan.Launch(0).Arguments.Length);
    }
    [Fact]
    public void InvalidSelectionAndEmbeddedBatchTokenAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new CustomActionPlan(Action(), Context(2)));
        var action = Action("batch"); action.Arguments = "--files={paths}";
        Assert.Throws<InvalidDataException>(() => new CustomActionPlan(action, Context(2)));
        action = Action(); action.MatchExtensions = [".ps1"];
        Assert.Throws<InvalidOperationException>(() => new CustomActionPlan(action, Context()));
    }
    [Fact]
    public void NetworkProgramPreviewDoesNotResolveOrExecuteProgram()
    {
        var action = Action(); action.Executable = @"\\offline.invalid\tools\tool.exe";
        var plan = new CustomActionPlan(action, Context());
        Assert.Equal(action.Executable, plan.Executable); Assert.Contains("这里只预览", plan.Describe());
    }
    [Fact]
    public void LargePreviewIsBoundedAndExplainsTruncation()
    {
        var action = Action("each"); action.Arguments = string.Join(' ', Enumerable.Range(0, 100).Select(i => "argument" + i));
        var plan = new CustomActionPlan(action, Context(100)); string text = plan.Describe();
        Assert.Contains("预计启动：100 次", text); Assert.Contains("另有 97 次启动", text); Assert.Contains("另有 60 项参数", text);
        Assert.DoesNotContain("第 4 次", text); Assert.True(text.Length < 16000);
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.Launch(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => plan.Launch(100));
    }
    [Theory]
    [InlineData("notepad")]
    [InlineData("code")]
    [InlineData("terminal")]
    [InlineData("powershell")]
    public void TemplatesCreateIndependentActionsWithoutElevation(string id)
    {
        var template = CustomActionTemplates.All.Single(x => x.Id == id);
        var first = template.Create(); var second = template.Create();
        Assert.NotEqual(first.Id, second.Id); Assert.False(first.RunAsAdmin); Assert.False(first.UseTargetWorkingDirectory);
        first.MatchExtensions.Clear(); Assert.NotEmpty(second.MatchExtensions);
        second.Executable = Path.Combine(Root, "tool.exe"); ConfigStore.Validate(new() { CustomActions = [second] });
    }
}
