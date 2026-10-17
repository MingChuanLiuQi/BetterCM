using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using BetterCM.Services;

namespace BetterCM;

public partial class App
{
    private bool _helperMode, _interceptionPaused;
    private long _lastErrorNotification = long.MinValue;
    public string? ConfigurationError => _store.CanSave ? null : _store.LastError;
    public bool HasConfigurationBackup => File.Exists(_store.BackupPath);
    internal bool ReloadConfiguration()
    {
        bool loaded = _store.Load();
        if (loaded) { ApplyInput(); ThemeService.Apply(Config.Theme, Config.Material); }
        _settings?.NotifyConfigurationChanged();
        return loaded;
    }
    public string? RestoreConfigurationBackup()
    {
        if (!_store.RestoreBackup()) return _store.LastError;
        ApplyInput(); ThemeService.Apply(Config.Theme, Config.Material);
        return null;
    }
    private void DispatcherFailure(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        AppLog.Write("dispatcher", args.Exception);
        if (_helperMode) { args.Handled = true; Shutdown(1); return; }
        // Unknown UI failures must not keep intercepting Explorer with a broken
        // menu. Leave settings/tray available so the user can inspect or restart.
        bool recoverable = args.Exception is IOException or COMException or Win32Exception or OperationCanceledException;
        if (!recoverable)
        {
            _interceptionPaused = true;
            try { ApplyInput(); } catch (Exception ex) { AppLog.Write("pause-input", ex); _input?.Dispose(); _input = null; }
        }
        try { _input?.WatchPreparation(null); _menu?.CloseMenu("界面异常", true); }
        catch (Exception ex) { AppLog.Write("error-dismiss", ex); }
        ReportError(recoverable ? "操作未完成，请重试。" : "界面遇到异常，已暂停右键接管。请重启 BetterCM；暂时可使用 Windows 右键菜单。");
        args.Handled = true;
    }
    private void UnobservedFailure(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        AppLog.Write("unobserved-task", args.Exception); args.SetObserved();
    }
    private void ReportError(string message)
    {
        long now = Environment.TickCount64;
        if (_lastErrorNotification != long.MinValue && now - _lastErrorNotification < 10000) return;
        _lastErrorNotification = now; Notify(message, true);
    }
}
