using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using BetterCM.Config;
namespace BetterCM.Services;

public static partial class DiagnosticExporter
{
    [GeneratedRegex(@"^\[(?<time>[^\]]+)\] pid=\d+ (?<scope>[a-zA-Z0-9-]+): (?<type>System\.[a-zA-Z0-9.]+)", RegexOptions.Multiline)]
    private static partial Regex LogHeader();
    public static void Export(string destination, AppConfig config, double dpi, string? dataDirectory = null, IReadOnlyList<MenuRequestSample>? menuRequests = null)
    {
        using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        void Add(string name, object value)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
        Add("environment.json", new { Version = AppInfo.Version, Build = AppInfo.Build, OS = Environment.OSVersion.VersionString, Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), Dpi = dpi, Time = DateTimeOffset.Now });
        Add("menu-requests.json", new { Clock = "Monotonic; cumulative milliseconds from accepted right-button release or hotkey message", Observation = "WPF rendering callback completed; keyboard focus sampled separately; not hardware display latency or animation completion", Samples = menuRequests ?? [] });
        // Allowlist: never copy arbitrary strings, exception messages, stack traces, paths or shell titles.
        Add("configuration.json", new
        {
            config.SchemaVersion,
            config.Theme,
            config.Material,
            config.ClassicMenu,
            config.ReplaceExplorerMenu,
            config.EnableAnimations,
            config.ShowFileDetails,
            config.IncludeShellActions,
            config.ShowMoreItems,
            config.CompactMenu,
            config.MoreHoverDelayMs,
            config.SubmenuHoverDelayMs,
            config.OpenAnimation,
            config.CloseAnimation,
            config.OpenAnimationMs,
            config.CloseAnimationMs,
            config.AnimationDistance,
            config.AnimationScalePercent,
            config.AnimationEasing,
            config.EnableExpansionAnimations,
            config.MoreExpansionAnimation,
            config.GroupExpansionAnimation,
            config.MoreExpansionMs,
            config.GroupExpansionMs,
            BuiltinActions = config.Actions.Where(x => Models.ActionCatalog.All.Any(a => a.Id == x.Id)).Select(x => new { x.Id, x.Enabled, x.IsPrimary, x.Order }),
            CustomActions = config.CustomActions.Select((x, i) => new { Index = i, x.Enabled, x.RunAsAdmin, x.IsPrimary, x.UseTargetWorkingDirectory, Mode = x.SelectionMode is "single" or "each" or "batch" ? x.SelectionMode : "invalid", ExtensionCount = x.MatchExtensions.Count }),
            PinnedShellCount = config.PinnedShellActions.Count,
            HiddenShellCount = config.HiddenShellActions.Count
        });
        foreach (var name in new[] { "BetterCM.log", "BetterCM.prev.log" })
        {
            var path = Path.Combine(dataDirectory ?? ConfigStore.DataDirectory, name);
            if (!File.Exists(path)) continue;
            // Read a bounded tail even when an external process has replaced the log with a large file.
            using var log = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            log.Seek(Math.Max(0, log.Length - AppLog.MaximumBytes), SeekOrigin.Begin);
            using var reader = new StreamReader(log);
            Add(name + ".summary.json", LogHeader().Matches(reader.ReadToEnd()).Select(m => new { Time = m.Groups["time"].Value, Scope = m.Groups["scope"].Value, ExceptionType = m.Groups["type"].Value }).ToArray());
        }
        Add("privacy.json", new { Redacted = true, Omitted = new[] { "usernames", "paths", "filenames", "custom titles", "arguments", "shell names", "exception messages", "stack traces" }, Logs = "Recent log metadata only; inspect before sharing." });
    }
}
