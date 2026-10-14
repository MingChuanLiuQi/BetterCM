using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class LogicFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM.Tests-" + Guid.NewGuid().ToString("N"));
    public IReadOnlyList<RegressionCheck> Results { get; }
    public LogicFixture() => Results = Task.Run(() => LogicChecks.RunAsync(_root)).GetAwaiter().GetResult();
    public void Dispose()
    {
        var root = Path.GetFullPath(_root);
        if (root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("BetterCM.Tests-", StringComparison.Ordinal) && Directory.Exists(root)) Directory.Delete(root, true);
    }
}

public sealed class LogicTests(LogicFixture fixture) : IClassFixture<LogicFixture>
{
    public static TheoryData<string> Cases => new(LogicChecks.Names);
    [Theory]
    [MemberData(nameof(Cases))]
    public void Contract(string name)
    {
        var abnormal = fixture.Results.FirstOrDefault(x => x.Name == "逻辑检查异常");
        Assert.True(abnormal == null, abnormal?.Detail);
        var result = Assert.Single(fixture.Results, x => x.Name == name);
        Assert.True(result.Passed, result.Detail ?? name);
    }
}
