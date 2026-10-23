using BetterCM.Models;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class CascadeMenuTests
{
    [Theory]
    [InlineData(4, 7)]
    [InlineData(6, 11)]
    [InlineData(8, 14)]
    public void BothSidesUseSameGapAndAlignTheFirstRow(int gap, int inset)
    {
        var work = new NativeMethods.RECT { Left = 0, Top = 0, Right = 2400, Bottom = 1400 };
        var parent = new NativeMethods.RECT { Left = 700, Top = 100, Right = 1100, Bottom = 900 };
        var right = MenuPlacement.Cascade(parent, 250, 400, 500, work, gap: gap, firstRowInset: inset);
        var left = MenuPlacement.Cascade(parent, 250, 400, 500, work, preferLeft: true, gap: gap, firstRowInset: inset);
        Assert.Equal(gap, right.Left - parent.Right); Assert.Equal(gap, parent.Left - left.Right);
        Assert.Equal(250, right.Top + inset); Assert.Equal(250, left.Top + inset);
    }
    [Fact]
    public void SubmenuDelayDefaultsRoundTripsAndRejectsInvalidValues()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<BetterCM.Config.AppConfig>("{}")!;
        Assert.Equal(180, old.SubmenuHoverDelayMs);
        old.SubmenuHoverDelayMs = 320; Assert.Equal(320, old.Clone().SubmenuHoverDelayMs);
        old.SubmenuHoverDelayMs = 0; Assert.Throws<InvalidDataException>(() => BetterCM.Config.ConfigStore.Validate(old));
    }
    [Fact]
    public void CascadePreservesEveryCommandWithOnlyOneLevelOnEachSurface()
    {
        List<MenuAction> actions = [new() { Id = "one", ShellKey = "1", Group = "Tool", Title = "One" }, new() { Id = "two", ShellKey = "2", Group = "Tool · Nested", Title = "Two" }, new() { Id = "three", ShellKey = "3", Title = "Root" }];
        var roots = MenuComposer.Compose([], [], actions, new HashSet<string>(), false, "", allItems: true);
        Assert.Equal(2, roots.Count);
        var tool = roots.Single(x => x.IsGroup); Assert.Equal(2, tool.Children.Count);
        Assert.Equal("2", tool.Hint);
        Assert.Same(actions[1], tool.Children.Single(x => x.IsGroup).Children.Single().Action);
        Assert.All(tool.Children, x => Assert.Equal(0, x.Depth));
    }
    [Fact]
    public void SearchAndPinsStillReachNativeCommandsWithoutDuplicatingThem()
    {
        List<MenuAction> actions = [new() { Id = "sha", ShellKey = "sha", Group = "Tool · Hash", Title = "SHA-256" }, new() { Id = "zip", ShellKey = "zip", Group = "Tool", Title = "Zip" }];
        var search = MenuComposer.Compose([], [], actions, new HashSet<string>(), false, "SHA");
        Assert.Same(actions[0], Assert.Single(search).Action);
        var pinned = MenuComposer.Compose([], [], actions, new HashSet<string> { "sha" }, false, "", true);
        Assert.Same(actions[0], pinned.Single(x => x.IsPinned).Action);
        Assert.Same(actions[1], pinned.Single(x => x.IsGroup).Children.Single().Action);
    }
    [Theory]
    [InlineData("快速访问结束 - 下载 (已固定)")]
    [InlineData("快速访问开始 – 下载（已固定）")]
    [InlineData("\u200e下载\u200b (已固定)")]
    public void RepinnedNavigationNamesAreNormalized(string name) => Assert.Equal("下载", ExplorerContextService.NavigationName(name));

    [Theory]
    [InlineData(0, 1000, 750)]
    [InlineData(-1600, 0, -250)]
    public void CascadesFlipAtRightEdgeAndStayInsideWorkArea(int left, int right, int parentLeft)
    {
        var work = new NativeMethods.RECT { Left = left, Top = -100, Right = right, Bottom = 800 };
        var parent = new NativeMethods.RECT { Left = parentLeft, Right = right - 4, Top = 500, Bottom = 780 };
        var placement = MenuPlacement.Cascade(parent, 720, 300, 250, work);
        Assert.True(placement.Right <= parent.Left + 1); Assert.True(placement.Left >= work.Left);
        Assert.True(placement.Bottom <= work.Bottom - 4); Assert.True(placement.Top >= work.Top + 4);
    }
    [Fact]
    public void SubmenuObservationKeepsGapsOutsideAndAllWindowsInside()
    {
        var observation = new MenuObservation(1, new() { Left = 0, Top = 0, Right = 100, Bottom = 300 }, 7, Submenus: [new(2, new() { Left = 100, Top = 100, Right = 200, Bottom = 200 })]);
        Assert.True(observation.ContainsHandle(2)); Assert.True(observation.ContainsPoint(new() { X = 150, Y = 150 }));
        Assert.False(observation.ContainsPoint(new() { X = 150, Y = 50 })); Assert.False(observation.ContainsHandle(3));
    }
}
