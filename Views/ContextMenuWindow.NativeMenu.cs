using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public partial class ContextMenuWindow
{
    private async Task CompleteSearchAsync()
    {
        int generation = _generation;
        try
        {
            var page = await (_loadingAll = _nativeLoad!(null));
            if (_generation != generation || !IsOpen) return;
            if (Search.Text.Length == 0)
            {
                // The user cleared search and returned to root browsing. Hydrate the
                // completed tree in place without replacing root row containers or closing an open submenu.
                _nativeState.AddCommands(page.Actions);
                _nativeState.SetMenu(page.Menu);
                var hydrated = _nativeState.Rows();
                var current = VisibleRows;
                for (int i = 0; i < Math.Min(current.Count, hydrated.Count); i++)
                    if (current[i].IsGroup && current[i].DeferredSubmenu != null)
                    {
                        current[i].Children = hydrated[i].Children;
                        current[i].DeferredSubmenu = null;
                    }
                return;
            }
            _nativeState.Reset(page.Menu, page.Actions); Rebuild(); Reposition(); RootMenu.PublishBounds();
        }
        catch (OperationCanceledException) { /* Superseded search completion leaves the visible menu alone. */ }
        catch (Exception ex) { if (_generation == generation && IsOpen && Search.Text.Length > 0) ShowLoadError(ex); }
        finally { if (_generation == generation) _loadingAll = null; }
    }
    private void ShowLoadError(Exception exception)
    {
        AppLog.Write("shell-expand", exception);
        ShellStatus.Text = "子菜单读取失败，请重试或使用 Shift+右键"; ShellStatus.Visibility = Visibility.Visible; Reposition();
    }
}
