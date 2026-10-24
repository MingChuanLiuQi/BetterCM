using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class ActionRulesTests
{
    private static SelectedContext Files(params string[] paths) => new() { CurrentDirectory = @"C:\Work", Items = paths.Select(x => new SelectedItem(x, false)).ToArray() };
    [Theory]
    [InlineData(@"C:\Work\a.txt", @"c:\work", true)]
    [InlineData(@"C:\WorkTwo\a.txt", @"C:\Work", false)]
    [InlineData(@"C:\Work\..\Else\a.txt", @"C:\Work", false)]
    [InlineData(@"C:\Work\Sub\a.txt", @"C:\Work\", true)]
    [InlineData(@"\\server\share\a.txt", @"\\server\share", true)]
    [InlineData(@"C:\file.txt", @"C:\", true)]
    [InlineData(@"relative\file.txt", @"C:\", false)]
    public void DirectoryRulesRespectBoundariesAndNormalization(string path, string root, bool expected)
        => Assert.Equal(expected, ActionRules.IsWithin(path, root));
    [Fact]
    public void AllSelectionItemsMustMatchWithoutAccessingTheFilesystem()
    {
        var rule = new ActionRule { Target = "files", DirectoryRoot = @"C:\Work", NamePattern = "REPORT-?.TXT", MinimumCount = 2, MaximumCount = 3 };
        Assert.True(ActionRules.Matches(rule, Files(@"C:\Work\report-a.txt", @"C:\Work\sub\report-b.txt")));
        Assert.False(ActionRules.Matches(rule, Files(@"C:\Work\report-a.txt")));
        Assert.False(ActionRules.Matches(rule, Files(@"C:\Work\report-a.txt", @"C:\Work\other.txt")));
        Assert.False(ActionRules.Matches(rule, Files(@"C:\Work\report-a.txt", @"C:\Else\report-b.txt")));
    }
    [Fact]
    public void BackgroundAndFolderRulesAreDistinct()
    {
        var background = new SelectedContext { CurrentDirectory = @"C:\Work", ShellBackground = true };
        var folder = background with { Items = [new(@"C:\Work\Sub", true)] };
        Assert.True(ActionRules.Matches(new() { Target = "background", MaximumCount = 0 }, background));
        Assert.False(ActionRules.Matches(new() { Target = "folders" }, background));
        Assert.True(ActionRules.Matches(new() { Target = "folders" }, folder));
        Assert.False(ActionRules.Matches(new() { Target = "background" }, folder));
    }
    [Fact]
    public void VirtualContextsDoNotMatchFilesystemRules()
        => Assert.False(ActionRules.Matches(new(), new() { ShellParsingPaths = ["::{virtual}"] }));
    [Fact]
    public void ProfileUsesDeepestCommonDirectoryThenManualFallback()
    {
        var general = new MenuProfile { Title = "work", DirectoryRoot = @"C:\Work" };
        var specific = new MenuProfile { Title = "sub", DirectoryRoot = @"C:\Work\Sub", ActionIds = ["hash"] };
        var sameDirectory = new MenuProfile { Title = "same directory with separator", DirectoryRoot = @"C:\Work\Sub\" };
        var manual = new MenuProfile { Title = "manual" };
        var config = new AppConfig { AutoSelectProfile = true, MenuProfiles = [general, specific, sameDirectory, manual], ActiveProfileId = manual.Id };
        Assert.Same(specific, ActionRules.SelectProfile(config, Files(@"C:\Work\Sub\a.txt")));
        Assert.Same(general, ActionRules.SelectProfile(config, Files(@"C:\Work\Sub\a.txt", @"C:\Work\Other\b.txt")));
        Assert.Same(manual, ActionRules.SelectProfile(config, Files(@"D:\Else\a.txt")));
        config.AutoSelectProfile = false;
        Assert.Same(manual, ActionRules.SelectProfile(config, Files(@"C:\Work\Sub\a.txt")));
        Assert.True(ActionRules.Allows(specific, "hash")); Assert.False(ActionRules.Allows(specific, "path")); Assert.True(ActionRules.Allows(null, "anything"));
    }
    [Fact]
    public void SchemaTwoMigrationPreservesActionsAndDoesNotRestrictTheirRules()
    {
        var migrated = ConfigJson.Deserialize(ConfigMigrations.Migrate("""{"SchemaVersion":2,"Theme":"dark","CustomActions":[{"Id":"old","Title":"old","Executable":"C:\\Tools\\tool.exe","IsPrimary":false}]}"""))!;
        ConfigStore.Validate(migrated);
        Assert.Equal(AppConfig.CurrentSchemaVersion, migrated.SchemaVersion); Assert.Equal("dark", migrated.Theme);
        var action = Assert.Single(migrated.CustomActions); Assert.False(action.IsPrimary);
        Assert.Equal("any", action.Rule.Target); Assert.Equal("", action.Group); Assert.Empty(migrated.MenuProfiles); Assert.Empty(migrated.FavoriteDestinations);
    }
    [Theory]
    [InlineData("batch-rename")]
    [InlineData("favorite:old")]
    public void MigrationPreservesActionsWhoseIdsUseNewlyReservedNames(string id)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new AppConfig { SchemaVersion = 2, CustomActions = [new() { Id = id, Title = "existing", Executable = @"C:\Tools\tool.exe" }] });
        var first = ConfigJson.Deserialize(ConfigMigrations.Migrate(json))!;
        var second = ConfigJson.Deserialize(ConfigMigrations.Migrate(json))!;
        ConfigStore.Validate(first);
        Assert.Equal(first.CustomActions[0].Id, second.CustomActions[0].Id);
        Assert.NotEqual(id, first.CustomActions[0].Id); Assert.Equal("existing", first.CustomActions[0].Title);
    }
    [Theory]
    [InlineData("unknown", "*", "", 0, 0)]
    [InlineData("any", "", "", 0, 0)]
    [InlineData("any", "a\\*", "", 0, 0)]
    [InlineData("any", "*", "relative", 0, 0)]
    [InlineData("any", "*", "", -1, 0)]
    [InlineData("any", "*", "", 3, 2)]
    public void InvalidRulesAreRejected(string target, string pattern, string root, int min, int max)
        => Assert.Throws<InvalidDataException>(() => ActionRules.ValidateRule(new() { Target = target, NamePattern = pattern, DirectoryRoot = root, MinimumCount = min, MaximumCount = max }, ""));
    [Fact]
    public void MissingProfilesAndNullNestedListsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { ActiveProfileId = "missing" }));
        Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { MenuProfiles = [new() { Title = "bad", ActionIds = null! }] }));
        Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { Actions = [new() { Rule = null! }] }));
        Assert.Throws<InvalidDataException>(() => ConfigStore.Validate(new() { FavoriteDestinations = [new() { Title = "bad", Path = "relative" }] }));
    }
    [Fact]
    public void PrimaryAndMoreGroupsRemainSearchableAndNativeBindingsKeepTheirGroup()
    {
        var primary = new MenuAction { Id = "tool-a", Title = "打开", Group = "开发工具" };
        var another = new MenuAction { Id = "tool-b", Title = "测试", Group = "开发工具" };
        var rows = MenuComposer.Compose([primary, another], [], [], new HashSet<string>(), false, "");
        Assert.Equal(2, Assert.Single(rows).Children.Count);
        Assert.Equal("开发工具 · 打开", Assert.Single(MenuComposer.Compose([primary], [], [], new HashSet<string>(), false, "开发")).Title);
        var nested = Assert.Single(MenuComposer.Compose([], [primary, another], [], new HashSet<string>(), true, ""));
        Assert.True(nested.IsGroup); Assert.Equal(2, nested.Children.Count);
        var native = ShellCommandMap.Bind(new() { Id = "rename", Group = "整理" }, [new() { Id = "shell:1", Verb = "rename", ShellKey = "rename", Title = "重命名" }])!;
        Assert.Equal("整理", Assert.Single(MenuComposer.Compose([native], [], [], new HashSet<string>(), false, "")).Title);
    }
    [Fact]
    public void ActionExchangePreservesRulesGroupsAndStillReadsVersionOne()
    {
        var action = new CustomActionItem { Title = "tool", Executable = @"C:\Tools\tool.exe", Group = "开发", Rule = new() { Target = "files", MinimumCount = 2 } };
        var copy = Assert.Single(CustomActionTransfer.Deserialize(CustomActionTransfer.Serialize([action])));
        Assert.Equal("开发", copy.Group); Assert.Equal(2, copy.Rule.MinimumCount);
        Assert.Empty(CustomActionTransfer.Deserialize("""{"Format":"BetterCM.CustomActions","Version":1,"Actions":[]}"""));
        action.Id = "different"; action.Rule.MinimumCount = 3;
        var merged = CustomActionTransfer.Merge([copy], [action], CustomActionDuplicatePolicy.Skip);
        Assert.Equal(2, merged.Actions.Count);
    }
}
