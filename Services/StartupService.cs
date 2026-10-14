using Microsoft.Win32;
namespace BetterCM.Services;
public static class StartupService
{
    internal sealed record Snapshot(object? Value, RegistryValueKind Kind);
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("BetterCM") is string; } }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("BetterCM", $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue("BetterCM", false);
    }
    internal static Snapshot Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var value = key?.GetValue("BetterCM", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(value, value == null ? RegistryValueKind.String : key!.GetValueKind("BetterCM"));
    }
    internal static void Restore(Snapshot snapshot)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (snapshot.Value == null) key.DeleteValue("BetterCM", false);
        else key.SetValue("BetterCM", snapshot.Value, snapshot.Kind);
    }
}
