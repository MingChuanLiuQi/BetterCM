using System.Windows.Input;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class MenuKeyboardTests
{
    [Theory]
    [InlineData("Ctrl+C", Key.C, ModifierKeys.Control, true)]
    [InlineData("Control + Shift + N", Key.N, ModifierKeys.Control | ModifierKeys.Shift, true)]
    [InlineData("Alt+Enter", Key.Enter, ModifierKeys.Alt, true)]
    [InlineData("Delete", Key.Delete, ModifierKeys.None, true)]
    [InlineData("Ctrl+C", Key.C, ModifierKeys.None, false)]
    [InlineData("Ctrl+Ctrl+C", Key.C, ModifierKeys.Control, false)]
    [InlineData("Ctrl++C", Key.C, ModifierKeys.Control, false)]
    [InlineData("Ctrl+1", Key.D1, ModifierKeys.Control, true)]
    [InlineData("Alt+0", Key.D0, ModifierKeys.Alt, true)]
    [InlineData("9", Key.D9, ModifierKeys.None, true)]
    [InlineData("1", Key.Cancel, ModifierKeys.None, false)]
    [InlineData("35", Key.D1, ModifierKeys.None, false)]
    [InlineData("INS", Key.Insert, ModifierKeys.None, true)]
    [InlineData("PGUP", Key.PageUp, ModifierKeys.None, true)]
    [InlineData("Ctrl+PGDN", Key.PageDown, ModifierKeys.Control, true)]
    public void PrintedNativeShortcutsRespectKeysAndModifiers(string text, Key key, ModifierKeys modifiers, bool expected)
        => Assert.Equal(expected, MenuKeyboard.Matches(text, key, modifiers));
    [Fact]
    public void PrintedShortcutWorksWithoutCanonicalVerbAndKeepsNativeIdentity()
    {
        var action = new MenuAction { Id = "native:73", Title = "复制", Verb = "" };
        var rows = NativeMenuComposer.Compose([new("扩展", Children: [new("复制", 73, Shortcut: "Ctrl+C")])], [action]);
        Assert.Same(action, MenuKeyboard.NativeAction(rows, Key.C, ModifierKeys.Control));
    }
    [Fact]
    public void CanonicalFallbackWorksWhenNativeHandlerOmitsPrintedShortcut()
    {
        var action = new MenuAction { Id = "native:8", Title = "复制", Verb = "copy" };
        Assert.Same(action, MenuKeyboard.NativeAction(NativeMenuComposer.Compose([new("复制", 8)], [action]), Key.C, ModifierKeys.Control));
    }
    [Fact]
    public void DisabledNativeShortcutDoesNotExecute()
    {
        var action = new MenuAction { Id = "native:8", Title = "复制", Verb = "copy", Enabled = false };
        Assert.Null(MenuKeyboard.NativeAction(NativeMenuComposer.Compose([new("复制", 8, Enabled: false, Shortcut: "Ctrl+C")], [action]), Key.C, ModifierKeys.Control));
    }
    [Fact]
    public void EnterUsesNativeDefaultRatherThanFirstAvailableAction()
    {
        MenuEntry first = new() { Action = new() { Id = "first" } }, defaultRow = new() { Action = new() { Id = "default" }, IsDefault = true };
        Assert.Same(defaultRow.Action, MenuKeyboard.EnterAction([first, defaultRow], null, true));
        Assert.Null(MenuKeyboard.EnterAction([first], null, true));
        Assert.Same(first.Action, MenuKeyboard.EnterAction([first, defaultRow], first, true));
    }
    [Fact]
    public void EnhancedNativeBindingsKeepClipboardShortcutActions()
    {
        var mapped = ShellCommandMap.Bind(new() { Id = "copy", Shortcut = "Ctrl+C" }, [new() { Id = "shell:31", Verb = "copy" }]);
        Assert.NotNull(mapped); Assert.Equal("copy", mapped.Id); Assert.Equal("Ctrl+C", mapped.Shortcut);
    }
}
