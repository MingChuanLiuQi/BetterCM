using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class FirstFrameChecksTests
{
    private static FirstFrameSample Frame(double delay) => new(true, true, true, delay);
    private static FirstFrameSample[] Frames(params double[] delays) => delays.Select(Frame).ToArray();
    [Fact]
    public void ColdSampleAndFiveSteadySamplesHaveSeparateBudgets()
    {
        var result = FirstFrameChecks.Evaluate(Frame(370), Frames(80, 180, 100, 280, 120));
        Assert.True(result.Passed); Assert.Equal(120, result.MedianMs); Assert.Equal(280, result.P95Ms);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void UnmeasuredOrInvalidTimingsNeverPass(double delay)
    {
        Assert.False(FirstFrameChecks.Evaluate(Frame(delay), Frames(80, 90, 100, 110, 120)).Passed);
        Assert.False(FirstFrameChecks.Evaluate(Frame(370), Frames(80, 90, 100, 110, delay)).Passed);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EverySampleMustBeHeldCompletedAndUncloakedAndTheSuiteMustBeComplete(int failure)
    {
        var cold = Frame(370); var stable = Frames(80, 90, 100, 110, 120);
        if (failure == 0) cold = cold with { Held = false };
        if (failure == 1) stable[0] = stable[0] with { Held = false };
        if (failure == 2) stable[0] = stable[0] with { Completed = false };
        if (failure == 3) stable[0] = stable[0] with { Uncloaked = false };
        if (failure == 4) stable = stable[..4];
        if (failure == 5) stable = [.. stable, Frame(100)];
        Assert.False(FirstFrameChecks.Evaluate(cold, stable).Passed);
    }
    [Fact]
    public void MedianAndTailBudgetsIndependentlyRejectRegressions()
    {
        Assert.False(FirstFrameChecks.Evaluate(Frame(370), Frames(80, 100, 260, 270, 280)).Passed);
        Assert.False(FirstFrameChecks.Evaluate(Frame(370), Frames(80, 90, 100, 110, 510)).Passed);
    }
    [Fact]
    public void BudgetLimitsAreStrictAndDoNotRoundDownFailures()
    {
        Assert.False(FirstFrameChecks.Evaluate(Frame(1000), Frames(80, 90, 100, 110, 120)).Passed);
        Assert.False(FirstFrameChecks.Evaluate(Frame(370), Frames(100, 200, 250, 250, 300)).Passed);
        Assert.False(FirstFrameChecks.Evaluate(Frame(370), Frames(80, 90, 100, 110, 500)).Passed);
    }
}
