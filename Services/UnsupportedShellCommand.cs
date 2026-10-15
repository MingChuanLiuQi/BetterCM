namespace BetterCM.Services;

internal static class UnsupportedShellCommand
{
    // These protected verbs cannot be executed reliably by BetterCM. Omit them
    // from both command indices and menu trees; leave Explorer's own menu intact.
    // Match canonical verbs only, never localized captions or Home pin commands.
    internal static bool IsProtectedPin(string verb) =>
        verb.Equals("PinToStartScreen", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("UnpinFromStartScreen", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("taskbarpin", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("taskbarunpin", StringComparison.OrdinalIgnoreCase);
}
