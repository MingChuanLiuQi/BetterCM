using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class NativeMenuTests
{
    [Theory]
    [InlineData("新建(&W)", "W")]
    [InlineData("Rock && Roll (&R)", "R")]
    [InlineData("Rock && Roll", "")]
    [InlineData("新建(W)", "W")]
    public void NativeMnemonicsRespectLiteralAmpersands(string caption, string expected) => Assert.Equal(expected, ShellMenuSession.ReadMnemonic(caption));
    private static readonly List<MenuAction> Commands = [new() { Id = "native:7", Title = "Open Git Bash here", ShellKey = "git", Enabled = true }, new() { Id = "native:9", Title = "文本文档", Enabled = false }];
    [Fact] public void LegacyConfigurationDefaultsToClassicAndKeepsOptInPreferences()
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"ShowMoreItems\":true,\"PinnedShellActions\":[\"git\"]}")!;
        Assert.True(config.ClassicMenu); Assert.Contains("git", config.PinnedShellActions);
        config.ClassicMenu = false; Assert.False(config.Clone().ClassicMenu);
    }
    [Fact] public void NativeOrderSeparatorsAndNestedGroupsStayIntact()
    {
        ShellMenuNode[] menu = [new("Open Git Bash here", 7, IsDefault: true), new("", IsSeparator: true), new("新建", Children: [new("文本文档", 9, Enabled: false)]), new("", IsSeparator: true)];
        var rows = NativeMenuComposer.Compose(menu, Commands);
        Assert.Equal(4, rows.Count); Assert.Equal("Open Git Bash here", rows[0].Title); Assert.Same(Commands[0], rows[0].Action);
        Assert.True(rows[0].IsDefault); Assert.False(rows[0].CanPin); Assert.True(rows[1].IsSeparator); Assert.True(rows[3].IsSeparator);
        Assert.True(rows[2].IsGroup); Assert.Single(rows[2].Children); Assert.False(rows[2].Children[0].Enabled);
        Assert.DoesNotContain(rows, x => x.Title == "Git");
    }
    [Fact] public void DisabledAncestorsRemainDisabledInDirectSearchResults()
    {
        ShellMenuNode[] menu = [new("新建", Enabled: false, Checked: true, Children: [new("文本文档", 9)]), new("Open Git Bash here", 7)];
        var rows = NativeMenuComposer.Compose(menu, Commands, "文本文档");
        Assert.Single(rows); Assert.Equal("新建 · 文本文档", rows[0].Title); Assert.False(rows[0].Enabled); Assert.False(rows[0].IsGroup);
    }
    [Fact] public void SearchShowsFullPathAndDirectNativeCommand()
    {
        var command = new MenuAction { Id = "native:42", Title = "SHA-256" };
        var rows = NativeMenuComposer.Compose([new("NanaZip", Children: [new("校验", Children: [new("SHA-256", 42)])])], [command], " SHA-256 ");
        Assert.Single(rows); Assert.Equal("NanaZip · 校验 · SHA-256", rows[0].Title); Assert.Same(command, rows[0].Action); Assert.False(rows[0].IsGroup);
    }
    [Fact] public void MatchingAncestorIncludesLeavesWithoutEmptyGroupsOrIdCollisions()
    {
        ShellMenuNode[] menu = [new("工具", Children: [new("空组", Children: []), new("一", 7)]), new("工具", Children: [new("二", 9)])];
        var rows = NativeMenuComposer.Compose(menu, Commands, "工具");
        Assert.Equal(2, rows.Count); Assert.All(rows, x => Assert.False(x.IsGroup)); Assert.Equal(2, rows.Select(x => x.Id).Distinct().Count());
        Assert.Equal("native-row:0:1", rows[0].Id); Assert.Equal("native-row:1:0", rows[1].Id);
    }
    [Fact] public void UnfilteredEnabledParentWithDisabledChildrenPreservesNativeState()
    {
        var rows = NativeMenuComposer.Compose([new("新建", Enabled: true, Children: [new("文本文档", 9, Enabled: false)])], Commands);
        Assert.True(rows[0].Enabled); Assert.False(rows[0].Children[0].Enabled);
        Assert.False(ShellCommandMap.IsNative("native:7"));
    }
    [Fact] public void ShellTreeRoundTripsThroughHostProtocolWithoutDroppingState()
    {
        var response = new ShellResponse(true, Menu: [new("新建", Children: [new("文件夹", 1, Shortcut: "Ctrl+Shift+N", Checked: true)])]);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ShellResponse>(System.Text.Json.JsonSerializer.Serialize(response))!;
        Assert.Equal("Ctrl+Shift+N", restored.Menu![0].Children![0].Shortcut); Assert.True(restored.Menu[0].Children![0].Checked);
    }
    [Fact] public void NativeAliasesMayShareACommandWhileKeepingRowTitlesAndStates()
    {
        Func<Task> execute = () => Task.CompletedTask;
        MenuAction[] aliases = [new() { Id = "native:79", Title = "第一入口", Enabled = false, Execute = execute }, new() { Id = "native:79", Title = "第二入口", Enabled = true, Execute = execute }];
        ShellMenuNode[] menu = [new("第一入口", 79, Enabled: false), new("第二入口", 79, Enabled: true)];
        var rows = NativeMenuComposer.Compose(menu, aliases);
        Assert.Equal(2, rows.Count); Assert.Equal("第二入口", rows[1].Title);
        Assert.False(rows[0].Enabled); Assert.True(rows[1].Enabled); Assert.Same(execute, rows[1].Action!.Execute);
    }
}
