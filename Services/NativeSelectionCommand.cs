namespace BetterCM.Services;

internal static class NativeSelectionCommand
{
    // These user-selected Shell verbs need the real Windows menu confirmation.
    // Local COM handlers and remote proxies can reject or ignore programmatic pins.
    internal static bool RequiresExplorer(ShellItem item) => item.Group.Length == 0 &&
        (item.Verb.Equals("PinToStartScreen", StringComparison.OrdinalIgnoreCase) ||
         item.Verb.Equals("UnpinFromStartScreen", StringComparison.OrdinalIgnoreCase) ||
         item.Verb.Equals("taskbarpin", StringComparison.OrdinalIgnoreCase) ||
         item.Verb.Equals("taskbarunpin", StringComparison.OrdinalIgnoreCase));
}
