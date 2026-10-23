using System.Diagnostics;
namespace BetterCM.Services;

// Cumulative monotonic milestones. Never store targets, window handles or shell titles.
public sealed record MenuRequestSample(long RequestId, string Input, string Outcome, double QueueMs,
    double? ContextReadyMs, double? ShellReadyMs, double? ActionsReadyMs, double? WindowShownMs,
    double? PresentationObservedMs, bool KeyboardReady, double FinishedMs);

internal sealed class MenuRequestTiming
{
    private static long _nextId;
    private readonly long _started;
    private readonly Func<long, double> _elapsed;
    internal MenuRequestSample Sample { get; private set; }
    internal bool Finished => Sample.Outcome != "Preparing";
    internal MenuRequestTiming(TriggerRequest request, Func<long, double>? elapsed = null)
    {
        long now = Stopwatch.GetTimestamp();
        _started = request.ReceivedTimestamp > 0 && request.ReceivedTimestamp <= now ? request.ReceivedTimestamp : now;
        _elapsed = elapsed ?? (start => Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        Sample = new(Interlocked.Increment(ref _nextId), request.PointerActivation ? "RightClick" : "Hotkey", "Preparing", Elapsed(), null, null, null, null, null, false, 0);
    }
    private double Elapsed() => Math.Max(Sample?.QueueMs ?? 0, _elapsed(_started));
    internal void ContextReady() { if (!Finished) Sample = Sample with { ContextReadyMs = Elapsed() }; }
    internal void ShellReady() { if (!Finished) Sample = Sample with { ShellReadyMs = Elapsed() }; }
    internal void ActionsReady() { if (!Finished) Sample = Sample with { ActionsReadyMs = Elapsed() }; }
    internal void WindowShown() { if (!Finished) Sample = Sample with { WindowShownMs = Elapsed() }; }
    internal void Complete(bool rendered, bool keyboardReady)
    {
        if (Finished) return;
        Sample = Sample with { PresentationObservedMs = rendered ? Elapsed() : null, KeyboardReady = rendered && keyboardReady };
        Finish(rendered ? keyboardReady ? "Ready" : "FocusUnavailable" : "DismissedBeforePresentation");
    }
    internal void Finish(string outcome)
    {
        if (!Finished) Sample = Sample with { Outcome = outcome, FinishedMs = Elapsed() };
    }
}

internal sealed class MenuRequestHistory
{
    private readonly Queue<MenuRequestSample> _samples = new();
    internal void Add(MenuRequestSample sample)
    {
        if (sample.Outcome == "Preparing") throw new ArgumentException("Only finished requests belong in history.", nameof(sample));
        if (_samples.Count == 32) _samples.Dequeue();
        _samples.Enqueue(sample);
    }
    internal MenuRequestSample[] Snapshot() => [.. _samples];
}
