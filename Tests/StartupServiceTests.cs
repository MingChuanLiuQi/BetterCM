using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class StartupServiceTests
{
    [Fact] public void EnabledRequiresCurrentExecutableAndBackgroundArgument()
    {
        const string exe = @"C:\Apps\Better CM\BetterCM.exe";
        Assert.True(StartupService.Matches('"' + exe + "\" --background", exe));
        Assert.False(StartupService.Matches("\"C:\\old\\BetterCM.exe\" --background", exe));
        Assert.False(StartupService.Matches('"' + exe + '"', exe));
        Assert.False(StartupService.Matches('"' + exe + "\" --background extra", exe));
        Assert.False(StartupService.Matches(null, exe));
    }
}
