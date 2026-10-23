namespace BetterCM.Services;

internal static class StartupRouter
{
    internal static bool IsSupported(IReadOnlyList<string> args) => args.Count == 0
        || args.Count == 1 && args[0] is "--shutdown" or "--background"
        || args.Count == 2 && args[0] is "--shell-host" or "--context-host" && !string.IsNullOrWhiteSpace(args[1]);
}
