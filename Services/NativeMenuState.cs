using BetterCM.Models;
namespace BetterCM.Services;

// Owned by one menu generation. Never carries commands or delegates across sessions.
internal sealed class NativeMenuState
{
    private readonly Dictionary<string, MenuAction> _commands = new(StringComparer.Ordinal);
    private IReadOnlyList<MenuEntry>? _rows;
    internal IReadOnlyList<ShellMenuNode>? Menu { get; private set; }
    internal IReadOnlyDictionary<string, MenuAction> Commands => _commands;
    internal void SetMenu(IReadOnlyList<ShellMenuNode>? menu) { Menu = menu; _rows = null; }
    internal void Reset(IReadOnlyList<ShellMenuNode>? menu, IEnumerable<MenuAction> commands)
    { SetMenu(menu); ReplaceCommands(commands); }
    internal void ReplaceCommands(IEnumerable<MenuAction> commands)
    { _commands.Clear(); _rows = null; AddCommands(commands); }
    internal void AddCommands(IEnumerable<MenuAction> commands)
    {
        foreach (var action in commands)
            if (_commands.TryAdd(action.Id, action)) _rows = null;
    }
    internal IReadOnlyList<MenuEntry> Rows(string filter = "")
    {
        if (Menu == null) return [];
        if (filter.Trim().Length > 0) return NativeMenuComposer.ComposeIndexed(Menu, _commands, filter);
        return _rows ??= NativeMenuComposer.ComposeIndexed(Menu, _commands);
    }
}
