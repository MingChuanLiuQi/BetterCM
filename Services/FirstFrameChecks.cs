namespace BetterCM.Services;

internal sealed record FirstFrameSample(bool Held, bool Completed, bool Uncloaked, double DelayMs)
{
    internal bool Observed => Held && Completed && Uncloaked && double.IsFinite(DelayMs) && DelayMs > 0;
}
internal sealed record FirstFrameBudget(bool Passed, double MedianMs, double P95Ms);
internal static class FirstFrameChecks
{
    internal const int StableSampleCount = 5;
    internal const double ColdLimitMs = 1000, MedianLimitMs = 250, P95LimitMs = 500;
    internal static FirstFrameBudget Evaluate(FirstFrameSample cold, IReadOnlyList<FirstFrameSample> stable)
    {
        if (stable.Count != StableSampleCount || !cold.Observed || stable.Any(x => !x.Observed)) return new(false, double.NaN, double.NaN);
        var ordered = stable.Select(x => x.DelayMs).Order().ToArray();
        double median = ordered[ordered.Length / 2], p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1];
        return new(cold.DelayMs < ColdLimitMs && median < MedianLimitMs && p95 < P95LimitMs, median, p95);
    }
}
