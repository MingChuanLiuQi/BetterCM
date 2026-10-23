using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class ShellMappingTests
{
    [Fact]
    public void DisabledShellCommandCannotBeEnabledByPresentation()
    {
        var action = new MenuAction { Id = "paste", Enabled = true };
        Func<Task> invoke = () => Task.CompletedTask;
        var bound = ShellCommandMap.Bind(action, [new() { Id = "shell:5", ShellKey = "|paste", Verb = "paste", Enabled = false, Execute = invoke }]);
        Assert.NotNull(bound); Assert.False(bound.Enabled); Assert.Same(invoke, bound.Execute); Assert.Equal("|paste", bound.ShellKey);
    }
    [Fact]
    public void MissingVerbDoesNotFallBackToLocalizedCaptionOrExtensionGroup()
    {
        Assert.Null(ShellCommandMap.Bind(new() { Id = "delete" }, [new() { Title = "删除" }, new() { Verb = "delete", Group = "Extension" }]));
    }
    [Fact]
    public void FullModePreservesGroupsAndDistinctCommandsWithSameKey()
    {
        List<MenuAction> shell = [new() { Id = "1", ShellKey = "same", Group = "A · B", Title = "one" }, new() { Id = "2", ShellKey = "same", Group = "A · B", Title = "two" }];
        var rows = MenuComposer.Compose([], [], shell, new HashSet<string>(), false, "", true);
        var root = Assert.Single(rows); Assert.True(root.IsGroup); Assert.Equal("A", root.Title);
        var nested = Assert.Single(root.Children); Assert.Equal("B", nested.Title);
        Assert.Equal(2, nested.Children.Count); Assert.Same(shell[1], nested.Children[1].Action);
    }
    [Fact]
    public void OldConfigKeepsNormalModeAndCompactRoundTrips()
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"ShowMoreItems\":false}")!;
        Assert.False(config.CompactMenu); Assert.False(config.ShowMoreItems);
        config.CompactMenu = true; Assert.True(config.Clone().CompactMenu);
    }
}
