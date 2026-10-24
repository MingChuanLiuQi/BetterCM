namespace BetterCM.Config;

public sealed class ActionRule
{
    public string Target { get; set; } = "any";
    public string NamePattern { get; set; } = "*";
    public string DirectoryRoot { get; set; } = "";
    public int MinimumCount { get; set; }
    public int MaximumCount { get; set; }
    public ActionRule Clone() => (ActionRule)MemberwiseClone();
}

public sealed class FavoriteDestination
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public bool DateSubfolder { get; set; }
    public FavoriteDestination Clone() => (FavoriteDestination)MemberwiseClone();
}

public sealed class MenuProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string DirectoryRoot { get; set; } = "";
    public List<string> ActionIds { get; set; } = [];
    public MenuProfile Clone() => new() { Id = Id, Title = Title, DirectoryRoot = DirectoryRoot, ActionIds = [.. ActionIds] };
}
