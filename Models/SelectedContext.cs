using System.IO;
namespace BetterCM.Models;

public sealed record SelectedItem(string Path, bool IsDirectory)
{
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } name ? name : Path;
    public string Extension => IsDirectory ? "dir" : System.IO.Path.GetExtension(Path).ToLowerInvariant();
}
public sealed record SelectedContext
{
    public IReadOnlyList<SelectedItem> Items { get; init; } = [];
    public string CurrentDirectory { get; init; } = "";
    public string Source { get; init; } = "未识别";
    [System.Text.Json.Serialization.JsonIgnore] public nint SourceWindow { get; init; }
    public IReadOnlyList<string> ShellParsingPaths { get; init; } = [];
    public string DisplayName { get; init; } = "";
    public bool ShellBackground { get; init; }
    public string ShellViewPath { get; init; } = "";
    public bool PinnedNavigation { get; init; }
    public IEnumerable<string> ShellTargets => ShellParsingPaths.Count > 0 ? ShellParsingPaths : TargetPaths;
    public DateTime CapturedAt { get; init; } = DateTime.Now;
    public IReadOnlyList<string> Paths => Items.Select(x => x.Path).ToArray();
    public bool HasSelection => Items.Count > 0;
    public bool IsSingleItem => Items.Count == 1;
    public string PrimaryPath => HasSelection ? Items[0].Path : CurrentDirectory;
    public bool IsDirectory => HasSelection ? Items[0].IsDirectory : !string.IsNullOrEmpty(CurrentDirectory);
    public bool HasFileTarget => !string.IsNullOrEmpty(PrimaryPath) && (ShellParsingPaths.Count == 0 || ShellParsingPaths.Count == (HasSelection ? Items.Count : 1));
    public bool HasTarget => HasFileTarget || ShellParsingPaths.Count > 0;
    public string PrimaryName => DisplayName.Length > 0 ? DisplayName : HasSelection ? Items[0].Name : PathFormatter.Name(CurrentDirectory);
    public string Extension => HasSelection ? Items[0].Extension : "dir";
    public bool IsText => IsSingleItem && ActionCatalog.TextExtensions.Contains(Extension);
    public bool IsWallpaper => IsSingleItem && Extension is ".jpg" or ".jpeg" or ".png" or ".bmp";
    public bool IsExecutable => IsSingleItem && Extension is ".exe" or ".bat" or ".cmd" or ".ps1" or ".msi";
    public IEnumerable<string> TargetPaths => HasSelection ? Items.Select(x => x.Path) : HasFileTarget ? [CurrentDirectory] : [];
}
