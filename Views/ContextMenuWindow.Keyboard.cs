using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BetterCM.Models;
using BetterCM.Services;
namespace BetterCM.Views;

public partial class ContextMenuWindow
{
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        try { await HandleKeyAsync(sender, e); }
        catch (Exception ex) { HandleActionFailure(ex); e.Handled = true; }
    }
    private async Task HandleKeyAsync(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed && !Search.IsKeyboardFocusWithin) key = e.ImeProcessedKey;
        var modifiers = Keyboard.Modifiers;
        if (Environment.GetEnvironmentVariable("BETTERCM_QA_REPORT") is { Length: > 0 } report)
            try { File.AppendAllText(report + ".keys.jsonl", System.Text.Json.JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Key = key.ToString(), Original = e.Key.ToString(), Modifiers = modifiers.ToString(), SearchFocus = Search.IsKeyboardFocusWithin }) + "\n"); } catch { }
        if (!IsOpen) { e.Handled = true; return; }
        if (key == Key.Escape) { if (_parentMenu != null) ReturnToParent(); else CloseMenu("Escape"); e.Handled = true; return; }
        if (key == Key.Left && modifiers == ModifierKeys.None && _parentMenu != null) { ReturnToParent(); e.Handled = true; return; }
        if (key == Key.F10 && modifiers == ModifierKeys.Shift) { Expand(); e.Handled = true; return; }
        if (key == Key.F && modifiers == ModifierKeys.Control) { FocusSearch(); e.Handled = true; return; }
        if (Search.IsKeyboardFocusWithin && key == Key.Down && modifiers == ModifierKeys.None)
        {
            ActionsList.Focus(); ActionsList.SelectedItem = VisibleRows.FirstOrDefault(x => x.Enabled && !x.IsSeparator); e.Handled = true; return;
        }
        if (Search.IsKeyboardFocusWithin && !(key == Key.Enter || modifiers == ModifierKeys.Alt)) return;
        var selected = ActionsList.SelectedItem as MenuEntry;
        if (RootMenu._nativeMenu != null && modifiers is ModifierKeys.None or ModifierKeys.Alt && key >= Key.A && key <= Key.Z)
        {
            string mnemonic = ((char)('A' + key - Key.A)).ToString();
            var matches = VisibleRows.Where(x => x.Enabled && x.Mnemonic == mnemonic).ToList();
            if (matches.Count > 0)
            {
                e.Handled = true;
                if (matches.Count > 1) { ActionsList.Focus(); ActionsList.SelectedItem = matches[(matches.FindIndex(x => x == selected) + 1) % matches.Count]; return; }
                var match = matches[0];
                if (match.IsGroup) OpenGroup(match, true);
                else if (match.Action?.Execute is { } execute) { RootMenu.CloseMenu("原生访问键执行", true); await execute(); }
                return;
            }
        }
        if (RootMenu._nativeMenu != null && key == Key.F5 && modifiers == ModifierKeys.None && RootMenu._nativeRefresh is { } refresh)
        { RootMenu.CloseMenu("原生视图刷新", true); e.Handled = true; await refresh(); return; }
        if (modifiers == ModifierKeys.None && selected is { IsGroup: true } && key is Key.Right or Key.Left or Key.Enter)
        {
            if (key != Key.Left) OpenGroup(selected, focus: true);
            e.Handled = true; return;
        }
        if (key == Key.Right && modifiers == ModifierKeys.None) { Expand(); e.Handled = true; return; }
        string? id = (key, modifiers) switch
        {
            (Key.F5, ModifierKeys.None) => "refresh",
            (Key.C, ModifierKeys.Control) => "copy", (Key.X, ModifierKeys.Control) => "cut",
            (Key.V, ModifierKeys.Control) => "paste", (Key.F2, ModifierKeys.None) => "rename", (Key.Delete, ModifierKeys.None) => "delete",
            (Key.Enter, ModifierKeys.Alt) => "properties",
            (Key.T, ModifierKeys.None) => "terminal", (Key.C, ModifierKeys.None) => "code",
            (Key.N, ModifierKeys.None) => "notepad", (Key.P, ModifierKeys.None) => "path",
            (Key.U, ModifierKeys.None) => "slash", (Key.H, ModifierKeys.None) => "hash", _ => null
        };
        var action = RootMenu._nativeMenu != null
            ? MenuKeyboard.NativeAction(RootMenu._nativeState.Rows(), key, modifiers)
            : RootMenu._primary.Concat(RootMenu._more).Concat(RootMenu._toolbar).FirstOrDefault(x => x.Id == id && x.Enabled);
        if (key == Key.Enter && modifiers == ModifierKeys.None) action = MenuKeyboard.EnterAction(VisibleRows, selected, RootMenu._nativeMenu != null);
        if (action is { Enabled: true, Execute: not null }) { RootMenu.CloseMenu("键盘执行动作", true); e.Handled = true; await action.Execute(); }
    }
}
