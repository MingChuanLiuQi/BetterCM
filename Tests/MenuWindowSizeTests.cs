using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public class MenuWindowSizeTests
{
    [Theory]
    [InlineData(480, 960, 480, 960)]
    [InlineData(-1, -20, 0, 0)]
    [InlineData(65536, 90000, 65535, 65535)]
    [InlineData(int.MaxValue, 640, 65535, 640)]
    [InlineData(320, int.MaxValue, 320, 65535)]
    public void DimensionsUseUnsignedWordsWithoutWrapping(int width, int height, int expectedWidth, int expectedHeight)
    {
        ulong packed = (ulong)MenuWindowSize.Pack(width, height);
        Assert.Equal((ulong)expectedWidth, packed & 0xffff);
        Assert.Equal((ulong)expectedHeight, (packed >> 16) & 0xffff);
        Assert.Equal(0UL, packed >> 32);
    }
}
