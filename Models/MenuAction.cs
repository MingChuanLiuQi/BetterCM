namespace BetterCM.Models;
public sealed class MenuAction
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Glyph { get; init; } = "\uE8A7";
    public string Shortcut { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string? ShellKey { get; init; }
    public string Group { get; init; } = "";
    public string Verb { get; init; } = "";
    public Func<Task>? Execute { get; init; }
}
