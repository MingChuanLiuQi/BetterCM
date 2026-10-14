using BetterCM.Models;
namespace BetterCM.Services;

// Canonical verbs identify Windows operations. Captions/localization never route destructive actions.
public static class ShellCommandMap
{
    public static bool IsNative(string id) => id is "open" or "cut" or "copy" or "paste" or "rename" or "delete" or "properties";
    public static MenuAction? Bind(MenuAction presentation, IEnumerable<MenuAction> native)
    {
        if (!IsNative(presentation.Id)) return presentation;
        var command = native.FirstOrDefault(x => x.Group.Length == 0 && x.Verb.Equals(presentation.Id, StringComparison.OrdinalIgnoreCase));
        return command == null ? null : new()
        {
            Id = presentation.Id, Title = command.Title, Glyph = presentation.Glyph, Shortcut = presentation.Shortcut,
            Enabled = command.Enabled, ShellKey = command.ShellKey, Verb = command.Verb, Group = command.Group,
            Detail = command.Detail, Execute = command.Execute
        };
    }
}
