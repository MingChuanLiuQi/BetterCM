using System.Windows;
namespace BetterCM.Services;

internal sealed record MenuMetrics(double Width, Thickness ActionPadding, double FontSize)
{
    internal const double CascadeGap = 4;
    internal static MenuMetrics For(bool compact) => compact ? new(300, new(7, 3, 7, 3), 12) : new(320, new(10, 7, 10, 7), 14);
}
