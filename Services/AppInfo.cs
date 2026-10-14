using System.Reflection;
namespace BetterCM.Services;

public static class AppInfo
{
    public static string Build { get; } = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public static string Version { get; } = Build.Split('+')[0];
    public static string DisplayName => "BetterCM " + Version;
}
