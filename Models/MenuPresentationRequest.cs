using BetterCM.Config;
using BetterCM.Services;
namespace BetterCM.Models;

public sealed record MenuPresentationRequest
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required SelectedContext Context { get; init; }
    public required AppConfig Config { get; init; }
    public List<MenuAction> Toolbar { get; init; } = [];
    public List<MenuAction> Primary { get; init; } = [];
    public List<MenuAction> More { get; init; } = [];
    public bool Activate { get; init; } = true;
    public List<MenuAction>? Shell { get; init; }
    public string? ShellError { get; init; }
    public IReadOnlyList<ShellMenuNode>? NativeMenu { get; init; }
    public Func<Task>? NativeRefresh { get; init; }
    public Func<int?, Task<NativeMenuPage>>? NativeLoad { get; init; }
}
