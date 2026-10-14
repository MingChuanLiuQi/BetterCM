using System.Diagnostics;
using System.Text.Json;
using BetterCM.Config;
using BetterCM.Models;
using BetterCM.Views;
namespace BetterCM.Services;

// Same finite benchmark is copied into the pre-change source snapshot. It executes no verbs.
internal static class PerformanceChecks
{
    internal static async Task RunAsync(string report)
    {
        report = Path.GetFullPath(report); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        Environment.SetEnvironmentVariable("BETTERCM_PERF", "1");
        var samples = new List<Sample>();
        var desktop = NativeMethods.FindWindow("Progman", null);
        var folder = Path.Combine(Path.GetDirectoryName(report)!, "performance-fixture"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "目标 & 文件.txt"); File.WriteAllText(file, "performance fixture");
        using var contexts = new ExplorerContextService();
        using var bridge = new ShellBridge();
        var menu = new ContextMenuWindow { ShowActivated = false };
        ThemeService.Apply("dark", "mica");
        var preparation = Stopwatch.StartNew();
        menu.PreparePresentation();
        await Task.WhenAll(contexts.PrepareAsync(), bridge.PrepareAsync());
        double preparationMs = preparation.Elapsed.TotalMilliseconds;
        try
        {
            foreach (var scenario in new[] { "DesktopNormal", "DesktopCompact", "FileNormal" })
            for (int index = 0; index < 10; index++)
            {
                var total = Stopwatch.StartNew(); var phase = Stopwatch.StartNew();
                await contexts.CaptureAsync(scenario == "FileNormal" ? 0 : desktop, scenario == "FileNormal" ? [file] : []);
                double captured = phase.Elapsed.TotalMilliseconds; phase.Restart();
                SelectedContext context = scenario == "FileNormal"
                    ? new() { CurrentDirectory = folder, Items = [new(file, false)] }
                    : new() { CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), SourceWindow = desktop, ShellBackground = true };
                var response = await bridge.OpenAsync(context, deferSubmenus: true);
                if (!response.Success) throw new InvalidOperationException(response.Error);
                double shell = phase.Elapsed.TotalMilliseconds; phase.Restart();
                var config = new AppConfig { CompactMenu = scenario == "DesktopCompact" };
                var actions = response.Items!.Select(x => new MenuAction { Id = "native:" + x.Command, Title = x.Title, ShellKey = x.Key, Enabled = x.Enabled }).ToList();
                menu.ShowAt(28, 36, context, config, [], [], [], false, actions, nativeMenu: response.Menu);
                double layout = phase.Elapsed.TotalMilliseconds;
                while (menu.PresentationPending) await Task.Delay(1);
                double ready = total.Elapsed.TotalMilliseconds;
                // Full native data is verified after the first presentation, outside its measured path.
                var full = await bridge.ExpandAsync(response.Session!, null);
                if (!full.Success) throw new InvalidOperationException(full.Error);
                samples.Add(new(scenario, index, captured, shell, layout, menu.FirstFrameDelayMs, ready, full.Items!.Count, response.BuildTimings));
                menu.CloseMenu("性能采样", true);
                await Task.Delay(30);
            }
            var summary = samples.GroupBy(x => x.Scenario).Select(group => new
            {
                Scenario = group.Key, ColdTotalMs = group.First().TotalMs,
                WarmMedianMs = Percentile(group.Skip(1).Select(x => x.TotalMs), .5), WarmP95Ms = Percentile(group.Skip(1).Select(x => x.TotalMs), .95),
                WarmContextMs = Percentile(group.Skip(1).Select(x => x.ContextMs), .5),
                WarmShellMs = Percentile(group.Skip(1).Select(x => x.ShellMs), .5), WarmUiMs = Percentile(group.Skip(1).Select(x => x.UiMs), .5),
                WarmFirstFrameMs = Percentile(group.Skip(1).Select(x => x.FirstFrameMs), .5)
            });
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Version = AppInfo.Version, Executable = Environment.ProcessPath, Time = DateTimeOffset.Now, StartupPreparationMs = preparationMs, Notes = "No verbs executed; capture reads view without pointer hit-test. Startup prepares helpers and hidden HWND, never caches menu data. First sample per scenario excluded from warm statistics.", Summary = summary, Samples = samples }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { menu.CloseMenu("性能采样结束", true); menu.Close(); }
    }
    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        var ordered = values.Order().ToArray(); return ordered[(int)Math.Ceiling((ordered.Length - 1) * fraction)];
    }
    private sealed record Sample(string Scenario, int Index, double ContextMs, double ShellMs, double UiMs, double FirstFrameMs, double TotalMs, int LeafCount, ShellBuildTimings? BuildTimings);
}
