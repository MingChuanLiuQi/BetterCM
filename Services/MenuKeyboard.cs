using System.Windows.Input;
using BetterCM.Models;
namespace BetterCM.Services;

internal static class MenuKeyboard
{
    internal static IEnumerable<MenuEntry> Leaves(IEnumerable<MenuEntry> rows) => rows.SelectMany(x => x.IsGroup ? Leaves(x.Children) : new[] { x });
    internal static bool Matches(string shortcut, Key key, ModifierKeys modifiers)
    {
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(string.IsNullOrEmpty)) return false;
        ModifierKeys parsed = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            var next = part.ToLowerInvariant() switch { "ctrl" or "control" => ModifierKeys.Control, "shift" => ModifierKeys.Shift, "alt" => ModifierKeys.Alt, _ => (ModifierKeys)(-1) };
            if (next == (ModifierKeys)(-1) || (parsed & next) != 0) return false;
            parsed |= next;
        }
        var text = parts[^1].ToUpperInvariant();
        Key parsedKey = text switch
        {
            "ENTER" or "RETURN" => Key.Enter, "DEL" or "DELETE" => Key.Delete,
            "ESC" or "ESCAPE" => Key.Escape, "INS" => Key.Insert,
            "PGUP" => Key.PageUp, "PGDN" => Key.PageDown,
            _ when text.Length == 1 && char.IsAsciiDigit(text[0]) => (Key)((int)Key.D0 + text[0] - '0'),
            _ when text.Length > 0 && char.IsAsciiLetter(text[0]) && Enum.TryParse<Key>(text, true, out var value) && Enum.IsDefined(value) => value,
            _ => Key.None
        };
        return parsedKey != Key.None && parsedKey == key && parsed == modifiers;
    }
    internal static MenuAction? NativeAction(IEnumerable<MenuEntry> rows, Key key, ModifierKeys modifiers)
    {
        var leaves = Leaves(rows).Where(x => x.Enabled && x.Action != null).ToList();
        var printed = leaves.FirstOrDefault(x => Matches(x.Hint, key, modifiers));
        if (printed != null) return printed.Action;
        // Some native handlers omit the printed accelerator but provide a canonical verb.
        string? verb = (key, modifiers) switch
        {
            (Key.C, ModifierKeys.Control) => "copy", (Key.X, ModifierKeys.Control) => "cut", (Key.V, ModifierKeys.Control) => "paste",
            (Key.F2, ModifierKeys.None) => "rename", (Key.Delete, ModifierKeys.None) => "delete", (Key.Enter, ModifierKeys.Alt) => "properties", _ => null
        };
        return verb == null ? null : leaves.FirstOrDefault(x => x.Action!.Group.Length == 0 && x.Action.Verb.Equals(verb, StringComparison.OrdinalIgnoreCase))?.Action;
    }
    internal static MenuAction? EnterAction(IEnumerable<MenuEntry> rows, MenuEntry? selected, bool classic)
    {
        if (selected != null) return selected.Enabled ? selected.Action : null;
        return rows.FirstOrDefault(x => x.Enabled && x.IsDefault && x.Action != null)?.Action
            ?? (classic ? null : rows.FirstOrDefault(x => x.Action?.Enabled == true)?.Action);
    }
}
