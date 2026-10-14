using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;
public class BackgroundProcessTests
{
    [Theory]
    [InlineData("")]
    [InlineData("含中文 空格 & 符号")]
    [InlineData("quote\"inside")]
    [InlineData(@"C:\尾部 空格\")]
    [InlineData("\\\\\"mixed")]
    public void NativeArgumentQuotingRoundTrips(string argument)
    {
        var restored = ActionExecutor.ParseArguments(BackgroundProcess.Quote(argument));
        Assert.Single(restored); Assert.Equal(argument, restored[0]);
    }
}
