using BetterCM.Config;
namespace BetterCM.Services;

// A preparing preview owns the temporary global appearance even before a window exists.
internal sealed class AppearancePreview(Func<AppConfig> saved, Action<AppConfig> apply)
{
    private int? _owner;
    internal void Begin(int request, AppConfig config, bool preview)
    {
        if (!preview) { Restore(); return; }
        _owner = request; apply(config);
    }
    internal void Restore(int? request = null)
    {
        if (_owner == null || request != null && request != _owner) return;
        _owner = null; apply(saved());
    }
}
