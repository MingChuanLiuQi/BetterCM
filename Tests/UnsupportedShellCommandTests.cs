using BetterCM.Services;
using Xunit;

namespace BetterCM.Tests;

public class UnsupportedShellCommandTests
{
    [Theory]
    [InlineData("PinToStartScreen", true)]
    [InlineData("unpinfromstartscreen", true)]
    [InlineData("taskbarpin", true)]
    [InlineData("taskbarunpin", true)]
    [InlineData("PINTOSTARTSCREEN", true)]
    [InlineData("TASKBARUNPIN", true)]
    [InlineData("pintohome", false)]
    [InlineData("unpinfromhome", false)]
    [InlineData("open", false)]
    [InlineData("properties", false)]
    [InlineData("rename", false)]
    [InlineData("", false)]
    [InlineData("固定到开始", false)]
    [InlineData("my.taskbarpin", false)]
    public void OnlyExactProtectedVerbsAreExcluded(string verb, bool expected)
        => Assert.Equal(expected, UnsupportedShellCommand.IsProtectedPin(verb));
}
