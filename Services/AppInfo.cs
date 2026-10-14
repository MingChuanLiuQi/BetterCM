using System.Reflection;
namespace BetterCM.Services;

public static class AppInfo
{
    public static string Version { get; } = (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0];
    public static string DisplayName => "BetterCM " + Version;
}
