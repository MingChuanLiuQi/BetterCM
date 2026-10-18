using BetterCM.Config;
namespace BetterCM.Services;

public interface IMenuHost
{
    AppConfig Config { get; }
    void ToggleShellPin(string key);
    void ShowSettings();
    void Notify(string text, bool error = false);
}
