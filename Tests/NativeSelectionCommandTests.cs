using BetterCM.Services;
using Xunit;

namespace BetterCM.Tests;

public class NativeSelectionCommandTests
{
    private static ShellItem Pin => new(90, "固定到开始", "", "", "PinToStartScreen", true);
    [Theory]
    [InlineData("PinToStartScreen", true)]
    [InlineData("unpinfromstartscreen", true)]
    [InlineData("taskbarpin", true)]
    [InlineData("taskbarunpin", true)]
    [InlineData("open", false)]
    [InlineData("", false)]
    public void OnlyExplicitNativeStartVerbsUseExplorer(string verb, bool expected)
        => Assert.Equal(expected, NativeSelectionCommand.RequiresExplorer(Pin with { Verb = verb }));
    [Fact] public void ExtensionGroupCannotHijackNativeRoute()
        => Assert.False(NativeSelectionCommand.RequiresExplorer(Pin with { Group = "Extension" }));
    [Fact] public void MatchingCaptionCannotRedirectUnrelatedCommand()
        => Assert.False(NativeSelectionCommand.RequiresExplorer(Pin with { Verb = "other" }));
}
