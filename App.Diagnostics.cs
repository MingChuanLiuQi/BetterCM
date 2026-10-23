using System.IO;
using BetterCM.Services;
namespace BetterCM;

public partial class App
{
    private async Task<bool> TryRunDiagnosticsAsync(string[] args)
    {
            if (args.Length >= 3 && args[0] == "--verb-probe")
            {
                await File.WriteAllTextAsync(args[1] + ".tmp", System.Text.Json.JsonSerializer.Serialize(new { Version = AppInfo.Version, Arguments = args.Skip(2).ToArray(), WorkingDirectory = Environment.CurrentDirectory }));
                File.Move(args[1] + ".tmp", args[1], true);
                Shutdown(); return true;
            }
            if (args.Length >= 2 && args[0] == "--shell-test-host") { await ShellHost.RunAsync(args[1], Dispatcher, true); Shutdown(); return true; }
            if (args.Length >= 2 && args[0] == "--context-test-host") { await ExplorerContextService.RunHostAsync(args[1], Dispatcher, true); Shutdown(); return true; }
            if (args.Length >= 2 && args[0] == "--performance-check") { await PerformanceChecks.RunAsync(args[1]); Shutdown(); return true; }
            if (args.Length >= 4 && args[0] == "--explorer-view-check") { Shutdown(await ExplorerViewChecks.RunAsync(args[1], args[2], args[3], args.Contains("--hold")) ? 0 : 1); return true; }
            if (args.Length >= 2 && args[0] == "--stress-test") { await RobustnessChecks.RunAsync(args[1]); Shutdown(); return true; }
            if (args.Contains("--self-test")) { var report = args.ElementAtOrDefault(Array.IndexOf(args, "--self-test") + 1); Shutdown(await SelfTests.RunAsync(report) ? 0 : 1); return true; }
            if (args.Contains("--input-check")) { await InteractionChecks.RunAsync(args.ElementAtOrDefault(Array.IndexOf(args, "--input-check") + 1)); Shutdown(); return true; }
            if (args.Length >= 2 && args[0] == "--cascade-input-check") { await CascadeInteractionChecks.RunAsync(args[1]); Shutdown(); return true; }
        return false;
    }
}
