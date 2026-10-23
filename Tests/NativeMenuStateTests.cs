using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class NativeMenuStateTests
{
    private static MenuAction Command(string title) => new() { Id = "native:1", Title = title, Enabled = true };
    [Fact]
    public void RepeatedKeyboardSnapshotsReuseRowsAndPreserveNativeState()
    {
        var action = Command("操作"); var state = new NativeMenuState();
        state.Reset([new("原生动作", 1, Checked: true, IsDefault: true, Shortcut: "Ctrl+1")], [action]);
        var rows = state.Rows(); Assert.Same(rows, state.Rows()); Assert.Same(action, rows[0].Action);
        Assert.True(rows[0].Checked); Assert.True(rows[0].IsDefault); Assert.Equal("Ctrl+1", rows[0].Hint);
    }
    [Fact]
    public void IncrementalCommandsKeepFirstIdentityAndOnlyInvalidateForNewIds()
    {
        var original = Command("第一份"); var state = new NativeMenuState(); state.Reset([new("动作", 1)], [original]);
        var rows = state.Rows(); state.AddCommands([Command("重复")]); Assert.Same(rows, state.Rows());
        state.AddCommands([new() { Id = "native:2", Title = "新发现" }]); Assert.NotSame(rows, state.Rows());
        Assert.Equal(2, state.Commands.Count); Assert.Same(original, state.Rows()[0].Action);
    }
    [Fact]
    public void NewGenerationClearsOldDelegatesEvenWhenCommandIdsMatch()
    {
        var state = new NativeMenuState(); var old = Command("旧"); var current = Command("新");
        state.Reset([new("旧节点", 1)], [old]); var previous = state.Rows(); state.Reset([new("新节点", 1)], [current]);
        Assert.NotSame(previous, state.Rows()); Assert.Same(current, state.Rows()[0].Action); Assert.Single(state.Commands);
    }
    [Fact]
    public void TreeChangesInvalidateRowsAndFilteringDoesNotOverwriteFullSnapshot()
    {
        var state = new NativeMenuState(); state.Reset([new("分组", Children: [], DeferredSubmenu: 4)], [Command("动作")]);
        var pending = state.Rows(); state.SetMenu(NativeMenuTree.Replace(state.Menu!, 4, [new("动作", 1)]));
        var full = state.Rows(); Assert.NotSame(pending, full); Assert.Null(full[0].DeferredSubmenu);
        var filtered = state.Rows("动作"); Assert.Single(filtered); Assert.False(filtered[0].IsGroup); Assert.Same(full, state.Rows());
    }
}
