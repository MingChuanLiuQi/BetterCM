using Microsoft.Win32;
namespace BetterCM.Services;

public static class StartupService
{
    internal sealed record Snapshot(object? Value, RegistryValueKind Kind);
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
#if QA
    private static string ValueName => Environment.GetEnvironmentVariable("BETTERCM_STARTUP_NAME") == "BetterCM.QA" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BETTERCM_DATA_DIR")) ? "BetterCM.QA" : "BetterCM";
#else
    private const string ValueName = "BetterCM";
#endif
    internal static bool Matches(string? command, string executable)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        try
        {
            var args = ActionExecutor.ParseArguments(command);
            return args.Length == 2 && args[1] == "--background" && Path.GetFullPath(args[0]).Equals(Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return Matches(key?.GetValue(ValueName) as string, Environment.ProcessPath!); } }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue(ValueName, false);
    }
    internal static Snapshot Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(value, value == null ? RegistryValueKind.String : key!.GetValueKind(ValueName));
    }
    internal static void Restore(Snapshot snapshot)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (snapshot.Value == null) key.DeleteValue(ValueName, false);
        else key.SetValue(ValueName, snapshot.Value, snapshot.Kind);
    }
}
