using System.Windows;
namespace BetterCM.Models;

public sealed class MenuEntry
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Glyph { get; init; } = "\uE8A7";
    public string Hint { get; init; } = "";
    public string Mnemonic { get; init; } = "";
    public string Detail { get; init; } = "";
    public MenuAction? Action { get; init; }
    public bool IsGroup { get; init; }
    public IReadOnlyList<MenuEntry> Children { get; set; } = [];
    public int? DeferredSubmenu { get; set; }
    public bool IsSeparator { get; init; }
    public bool IsExpanded { get; init; }
    public bool IsPinned { get; init; }
    public bool IsNativeLayout { get; init; }
    public bool Checked { get; init; }
    public bool IsDefault { get; init; }
    public bool GroupEnabled { get; init; } = true;
    public bool CanPin => !IsNativeLayout && Action?.ShellKey != null;
    public string Origin { get; init; } = "";
    public bool IsShell => Action?.ShellKey != null || IsGroup && Origin == "Shell";
    public string Source => Origin;
    public bool Enabled => IsGroup ? GroupEnabled : Action?.Enabled == true;
    public int Depth { get; init; }
    public Thickness Indent => new(Depth * 12, 0, 0, 0);
}
