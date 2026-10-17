using BetterCM.Services;
using Xunit;

namespace BetterCM.Tests;

public sealed class SettingsWindowSizingTests
{
    [Theory]
    [InlineData(1366, 728, 1)] [InlineData(1920, 1040, 1.5)]
    [InlineData(1920, 1040, 1.75)] [InlineData(1920, 1040, 2)]
    [InlineData(1366, 728, 1.25)] [InlineData(1366, 728, 2)]
    public void MainWindowAndMinimumFitInsideScaledWorkArea(double width, double height, double scale)
    {
        double workWidth = width / scale, workHeight = height / scale;
        var bounds = SettingsWindowSizing.Fit(workWidth, workHeight, 1040, 800, 920, 640);
        Assert.InRange(bounds.Width, 1, workWidth - 24); Assert.InRange(bounds.Height, 1, workHeight - 24);
        Assert.True(bounds.MinWidth <= bounds.Width && bounds.MinHeight <= bounds.Height);
    }
}
