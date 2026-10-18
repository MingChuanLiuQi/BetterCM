using System.IO.Compression;
using System.Text.Json;
using BetterCM.Config;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class HardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BetterCM-hardening-" + Guid.NewGuid().ToString("N"));
    public HardeningTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact] public void BuiltinSystemProgramsUseExistingAbsolutePaths()
    {
        foreach (var path in new[] { ExecutableResolver.WindowsPowerShell, ExecutableResolver.SystemProgram("notepad.exe"), ExecutableResolver.SystemProgram("msiexec.exe") })
        {
            Assert.True(Path.IsPathFullyQualified(path));
            Assert.True(File.Exists(path), path);
            Assert.DoesNotContain(path, c => char.IsControl(c));
        }
    }

    [Fact] public void PlantingProgramInTargetCannotOverridePinnedProgram()
    {
        string trusted = Path.Combine(_root, "trusted"), target = Path.Combine(_root, "target");
        Directory.CreateDirectory(trusted); Directory.CreateDirectory(target);
        string trustedProgram = Path.Combine(trusted, "editor.exe"), planted = Path.Combine(target, "editor.exe");
        File.WriteAllText(trustedProgram, "trusted fixture"); File.WriteAllText(planted, "must never execute");
        string resolved = ExecutableResolver.Resolve("editor", ";.;relative;" + trusted)!;
        Assert.Equal(trustedProgram, resolved);
        var start = ActionExecutor.CreateStartInfo(resolved, ["a & b.txt"], target);
        Assert.Equal(trustedProgram, start.FileName); Assert.Equal(target, start.WorkingDirectory);
        Assert.Equal("a & b.txt", Assert.Single(start.ArgumentList));
        Assert.Throws<InvalidOperationException>(() => ActionExecutor.CreateStartInfo("editor.exe", directory: target));
        Assert.Null(ExecutableResolver.Resolve("editor", ";.;relative"));
        Assert.Null(ExecutableResolver.Resolve(@"target\editor.exe", trusted));
    }
    [Theory]
    [InlineData("editor.cmd")]
    [InlineData("editor.bat")]
    [InlineData("editor.ps1")]
    public void ImplicitShellInterpretersAreRejected(string name)
    {
        string path = Path.Combine(_root, name); File.WriteAllText(path, "fixture");
        Assert.Null(ExecutableResolver.Resolve(path));
        Assert.Throws<InvalidOperationException>(() => ActionExecutor.CreateStartInfo(path));
    }
    [Fact] public void SchemaOneMigratesInMemoryAndKeepsOriginalUntilSave()
    {
        string path = Path.Combine(_root, "config.json");
        string legacy = "{\"SchemaVersion\":1,\"Theme\":\"dark\",\"CustomActions\":[{\"Title\":\"old\",\"Executable\":\"BetterCM-missing-program\"}]}";
        File.WriteAllText(path, legacy); var store = new ConfigStore(path);
        Assert.True(store.Load(), store.LastError); Assert.Equal(AppConfig.CurrentSchemaVersion, store.Current.SchemaVersion);
        Assert.False(Assert.Single(store.Current.CustomActions).Enabled);
        Assert.Equal("BetterCM-missing-program", store.Current.CustomActions[0].Executable);
        Assert.Equal(legacy, File.ReadAllText(path));
        Assert.True(store.Save(store.Current)); Assert.Equal(legacy, File.ReadAllText(store.BackupPath));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Actions\":[],\"CustomActions\":[]}")]
    [InlineData("{\"schemaversion\":1,\"Theme\":\"light\"}")]
    public void LegacyMissingFieldsAndEmptyListsRemainSupported(string json)
    {
        string migrated = ConfigMigrations.Migrate(json);
        using var parsed = JsonDocument.Parse(migrated);
        Assert.Equal(AppConfig.CurrentSchemaVersion, parsed.RootElement.GetProperty("SchemaVersion").GetInt32());
    }
    [Theory]
    [InlineData("{\"SchemaVersion\":99,\"Unknown\":123}")]
    [InlineData("{\"SchemaVersion\":0}")]
    [InlineData("{\"SchemaVersion\":null}")]
    [InlineData("{\"SchemaVersion\":1,\"schemaversion\":1}")]
    [InlineData("{\"Actions\":null}")]
    [InlineData("{")]
    [InlineData("[]")]
    public void InvalidOrFutureConfigurationIsPreserved(string json)
    {
        string path = Path.Combine(_root, "config.json"); File.WriteAllText(path, json);
        var store = new ConfigStore(path); Assert.False(store.Load()); Assert.False(store.Save(new()));
        Assert.Equal(json, File.ReadAllText(path)); Assert.Equal(json, File.ReadAllText(store.InvalidCopyPath!));
    }
    [Fact] public void DiagnosticsExcludeUserSuppliedPathsTitlesAndLogMessages()
    {
        const string secret = "PRIVATE-FILENAME-username-秘密";
        var config = new AppConfig { CustomActions = [new() { Title = secret, Executable = @"C:\private\" + secret + ".exe", Arguments = secret, MatchExtensions = [secret] }], PinnedShellActions = [secret] };
        File.WriteAllText(Path.Combine(_root, "BetterCM.log"), "[2026-10-18T12:00:00] pid=1 action: System.IO.IOException: C:\\Users\\" + secret + "\n at " + secret);
        string destination = Path.Combine(_root, "diagnostics.zip"); DiagnosticExporter.Export(destination, config, 144, _root);
        using var archive = ZipFile.OpenRead(destination);
        Assert.Contains(archive.Entries, x => x.FullName == "environment.json");
        Assert.Contains(archive.Entries, x => x.FullName == "BetterCM.log.summary.json");
        foreach (var entry in archive.Entries) { using var reader = new StreamReader(entry.Open()); string text = reader.ReadToEnd(); Assert.DoesNotContain(secret, text); Assert.DoesNotContain(@"C:\\Users", text); }
    }
    [Fact] public async Task LogicCheckIdsAreUniqueAndDisplayNamesDoNotDefineIdentity()
    {
        var results = await LogicChecks.RunAsync(Path.Combine(_root, "logic"));
        Assert.All(results, result => Assert.False(string.IsNullOrWhiteSpace(result.Id)));
        Assert.Equal(results.Count, results.Select(x => x.Id).Distinct().Count());
        var first = new RegressionCheck(new CheckIdentity("Menu/Stable", "旧名称"), true);
        var second = new RegressionCheck(new CheckIdentity("Menu/Stable", "新名称"), true);
        Assert.Equal(first.Id, second.Id);
    }
    [Fact] public async Task CrossProcessProbeReaderRetriesTemporarySharingViolations()
    {
        string report = Path.Combine(_root, "probe.json"); File.WriteAllText(report, "{}");
        var held = new FileStream(report, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Task<string?> reading;
        try
        {
            reading = ShellLaunchChecks.ReadProbeAsync(report, TimeSpan.FromSeconds(2));
            await Task.Delay(100);
            Assert.False(reading.IsCompleted);
        }
        finally { held.Dispose(); }
        Assert.Equal("{}", await reading);
    }
}
