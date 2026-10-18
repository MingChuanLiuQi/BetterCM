# BetterCM 1.5.1 交接

本轮用户明确否定 1.5.0 的二次菜单确认方式，允许移除无法可靠执行的固定功能。1.5.1 删除开始／任务栏固定与对应取消固定入口，不再将打开系统菜单包装成固定操作成功。不要恢复这一方案或继续通过伪装进程、修改数据库、模拟按键绕过限制。

## 修改点

`UnsupportedShellCommand.IsProtectedPin` 精确匹配四种规范动词，忽略大小写。`ShellMenuSession.ReadMenu` 在建立命令索引和树之前排除，覆盖完整／递归／延迟读取、搜索、经典和增强模式。剩余原生命令偏移不变，HMENU 本身不变。未知和已移除命令由现有 `Invoke` 索引检查拒绝。

旧 `NativeSelectionCommand`、`RequiresNativeConfirmation`、`ShellViewBinding.ShowSelectionContextMenu`、专用 `WM_CONTEXTMENU` 常量、系统确认详情与标签全部删除。ShellBridge 恢复普通 invoke 的 `invoked = true` 生命周期分支。BeginRename 的视图激活和原有 ShellMenuSite 新建回调不受影响。

Home／快捷访问 `pintohome`、`unpinfromhome` 与 BetterCM 自身一级菜单管理不是此次删除目标。不要按“固定”两个字全局过滤显示标题，不能误删其他合法功能。1.5.0 的三级材质、短菜单测量、ConfigJson、带身份的 OptionalProbe、发布哈希清单均保留。

## 身份与验证

源码 `e6b524c4ab46169c05599030121adf4e65cd84aa`；修改前标签 `backup-before-1.5.1` 指向 `8d6796c8d1aec418d4495de420ab689b3211391a`。正式安装包根目录 `BetterCM-1.5.1-Setup-x64.exe`；SHA256 `907CFBB32851B3CBE7245AD63B4E97DA289144BB97DDECCE831F4A63CBF5148A`。最终 QA、发布清单及环境记录见 QA-1.5.1.md 和 artifacts/qa。发布标记之后的说明／校验文件提交不改变本次编译源码。

日常安装开始时为用户已升级的 1.5.0，勿沿用上一份交接的“仍是 1.4.0”假设。此次只做隔离安装测试，保留实际安装、用户配置和启动项；用户运行新安装包后才切换到 1.5.1。历史备份与旧包不批量清理。
