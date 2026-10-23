using System.IO.Compression;
using System.Text.Json;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class MenuRequestTimingTests
{
    [Fact]
    public void SuccessfulRequestKeepsCumulativeMilestonesAndKeyboardReadiness()
    {
        double elapsed = 4;
        var timing = new MenuRequestTiming(new(0, 0, 0, true), _ => elapsed);
        elapsed = 20; timing.ContextReady();
        elapsed = 70; timing.ShellReady();
        elapsed = 80; timing.ActionsReady();
        elapsed = 90; timing.WindowShown();
        elapsed = 100; timing.Complete(true, true);
        var sample = timing.Sample;
        Assert.Equal("RightClick", sample.Input);
        Assert.Equal("Ready", sample.Outcome);
        Assert.Equal(4, sample.QueueMs); Assert.Equal(20, sample.ContextReadyMs);
        Assert.Equal(70, sample.ShellReadyMs); Assert.Equal(80, sample.ActionsReadyMs);
        Assert.Equal(90, sample.WindowShownMs); Assert.Equal(100, sample.PresentationObservedMs);
        Assert.True(sample.KeyboardReady);
    }
    [Theory]
    [InlineData(false, false, "DismissedBeforePresentation")]
    [InlineData(true, false, "FocusUnavailable")]
    public void DismissedAndUnfocusedRequestsCannotBecomeReady(bool rendered, bool keyboard, string outcome)
    {
        var timing = new MenuRequestTiming(new(0, 0, 0));
        timing.Complete(rendered, keyboard);
        timing.Complete(true, true);
        Assert.Equal(outcome, timing.Sample.Outcome);
        Assert.False(timing.Sample.KeyboardReady);
        Assert.Equal(rendered, timing.Sample.PresentationObservedMs != null);
    }
    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Superseded")]
    [InlineData("Failed")]
    [InlineData("Exiting")]
    public void LateResponsesCannotChangeFinishedRequest(string outcome)
    {
        var timing = new MenuRequestTiming(new(0, 0, 0));
        timing.Finish(outcome);
        timing.ContextReady(); timing.ShellReady(); timing.ActionsReady(); timing.WindowShown(); timing.Complete(true, true);
        Assert.Equal(outcome, timing.Sample.Outcome);
        Assert.Null(timing.Sample.PresentationObservedMs); Assert.Null(timing.Sample.ContextReadyMs);
    }
    [Fact]
    public void HistoryIsBoundedAndRetainsFailures()
    {
        var history = new MenuRequestHistory();
        for (int i = 0; i < 40; i++)
        {
            var timing = new MenuRequestTiming(new(0, 0, 0));
            timing.Finish("Cancelled"); history.Add(timing.Sample);
        }
        Assert.Equal(32, history.Snapshot().Length);
        Assert.All(history.Snapshot(), sample => Assert.Equal("Cancelled", sample.Outcome));
        Assert.Throws<ArgumentException>(() => history.Add(new MenuRequestTiming(new(0, 0, 0)).Sample));
    }
    [Fact]
    public void DiagnosticExportIncludesTimingsWithoutTargetData()
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterCM-timing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var timing = new MenuRequestTiming(new(123, 456, 789)); timing.Complete(true, false);
            string path = Path.Combine(root, "diagnostics.zip");
            DiagnosticExporter.Export(path, new(), 96, root, [timing.Sample]);
            using var archive = ZipFile.OpenRead(path);
            using var reader = new StreamReader(archive.GetEntry("menu-requests.json")!.Open());
            using var json = JsonDocument.Parse(reader.ReadToEnd());
            var sample = Assert.Single(json.RootElement.GetProperty("Samples").EnumerateArray());
            Assert.Equal("FocusUnavailable", sample.GetProperty("Outcome").GetString());
            Assert.False(sample.TryGetProperty("Window", out _)); Assert.False(sample.TryGetProperty("Path", out _));
        }
        finally { Directory.Delete(root, true); }
    }
}
