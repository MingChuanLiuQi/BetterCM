using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using BetterCM.Models;
namespace BetterCM.Views;

public sealed class MenuItemButton : Button
{
    protected override AutomationPeer OnCreateAutomationPeer() => new MenuItemPeer(this);
    private sealed class MenuItemPeer(MenuItemButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;
        protected override string GetClassNameCore() => "MenuItem";
        private ListBoxItem? Row
        {
            get
            {
                for (DependencyObject? current = owner; current != null; current = VisualTreeHelper.GetParent(current))
                    if (current is ListBoxItem item) return item;
                return null;
            }
        }
        protected override int GetPositionInSetCore() => Row is { } row && ItemsControl.ItemsControlFromItemContainer(row) is { } list
            ? list.Items.Cast<MenuEntry>().Where(x => !x.IsSeparator).ToList().IndexOf((MenuEntry)row.DataContext) + 1 : base.GetPositionInSetCore();
        protected override int GetSizeOfSetCore() => Row is { } row && ItemsControl.ItemsControlFromItemContainer(row) is { } list
            ? list.Items.Cast<MenuEntry>().Count(x => !x.IsSeparator) : base.GetSizeOfSetCore();
        protected override string GetItemStatusCore()
        {
            var entry = owner.Tag as MenuEntry;
            return string.Join("，", new[] { entry?.Checked == true ? "已勾选" : "", Row?.IsSelected == true ? "已选中" : "", !owner.IsEnabled ? "不可用" : "", entry?.IsGroup == true ? "子菜单" : "" }.Where(x => x.Length > 0));
        }
    }
}
