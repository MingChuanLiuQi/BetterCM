using BetterCM.Config;
using BetterCM.Services;
using Xunit;
namespace BetterCM.Tests;

public sealed class AppearancePreviewTests
{
    [Fact]
    public void CancellingBeforeAnyWindowExistsRestoresSavedAppearance()
    {
        var saved = new AppConfig { Theme = "dark", Material = "mica" }; AppConfig? applied = null;
        var preview = new AppearancePreview(() => saved, value => applied = value);
        preview.Begin(1, new() { Theme = "light", Material = "solid" }, true);
        Assert.Equal("light", applied!.Theme); preview.Restore(1); Assert.Same(saved, applied);
    }
    [Fact]
    public void LateCancelledRequestCannotRestoreOverNewPreview()
    {
        AppConfig? applied = null; var saved = new AppConfig();
        var preview = new AppearancePreview(() => saved, value => applied = value);
        preview.Begin(1, new() { Theme = "light" }, true); var latest = new AppConfig { Theme = "dark" };
        preview.Begin(2, latest, true); preview.Restore(1); Assert.Same(latest, applied);
        preview.Restore(2); Assert.Same(saved, applied);
    }
    [Fact]
    public void NormalMenuClearsPreparingPreviewAndLateCleanupDoesNothing()
    {
        int changes = 0; var saved = new AppConfig(); var preview = new AppearancePreview(() => saved, _ => changes++);
        preview.Begin(1, new(), true); preview.Begin(2, saved, false); preview.Restore(1); Assert.Equal(2, changes);
    }
    [Fact]
    public void RestorationUsesLatestSavedSettings()
    {
        var saved = new AppConfig { Material = "mica" }; AppConfig? applied = null;
        var preview = new AppearancePreview(() => saved, value => applied = value);
        preview.Begin(1, new(), true); saved = new() { Material = "acrylic" }; preview.Restore(); Assert.Same(saved, applied);
    }
}
