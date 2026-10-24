using BetterCM.Services;
namespace BetterCM.Config;

public sealed class RuntimeParameter
{
    public string Id { get; set; } = "value";
    public string Title { get; set; } = "参数";
    public string Kind { get; set; } = "text";
    public string DefaultValue { get; set; } = "";
    public bool Required { get; set; } = true;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 10000;
    public List<string> Choices { get; set; } = [];
    public RuntimeParameter Clone() => new() { Id = Id, Title = Title, Kind = Kind, DefaultValue = DefaultValue, Required = Required, Minimum = Minimum, Maximum = Maximum, Choices = [.. Choices] };
}

public sealed class RenamePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public RenameOptions Options { get; set; } = new();
    public RenamePreset Clone() => new() { Id = Id, Title = Title, Options = Options.Clone() };
}
