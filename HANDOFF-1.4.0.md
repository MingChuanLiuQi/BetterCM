# BetterCM 1.4.0 交接

2026-10-14。版本唯一来源 `BetterCM.csproj`，`Version=1.4.0`。正式安装包在项目根目录 `BetterCM-1.4.0-Setup-x64.exe`；最终身份和验证见 `QA-1.4.0.md`、`artifacts/qa/release-1.4.0.json`。本轮审查判断与修复细节见 `REVIEW-1.4.0.md`。

修改前备份 `Backups/BetterCM-1.3.0-before-1.4.0-20261014-041632.zip`，17,958 文件逐项核对大小与 SHA256；旧 1.3.0 包另存为 `Backups/BetterCM-1.3.0-before-1.4.0-20261014-041632-Setup-x64.exe`，旧报告副本在 `artifacts/qa/previous-before-1.4.0`。

本轮关键实现：

- `OptionalProbe.ReadAsync` 保存当前在飞 `Task`，在调用方剩余超时预算（2 秒）与 `CancellationToken` 下串行等待前一次探测结束再启动新探测，避免快速连续右键因前一次几毫秒的本地读取尚未完成而直接返回 `null` 丢失图标或文件大小；若前一次读取真正卡死超时，后续请求在等待预算耗尽时返回 `default`，不启动第二个底层同步读取。
- `ContextMenuWindow.CompleteSearchAsync` 在延迟树补全完成时若搜索框已清空（`Search.Text.Length == 0`），仅将新命令与节点树合并入 `_nativeState` 并原位填充当前 `VisibleRows` 的延迟分组，不调用 `Rebuild()` 替换行容器或关闭已打开的侧边子菜单。
- `NativePasteMenu.Merge` 查找并替换占位项时显式要求 `Group.Length == 0`，避免误删复用相同数字 `Command` 的子菜单项。
- `ShellViewBinding.IsActiveView` 通过 `IsViewHit` 在中心点未命中时回退检测下半区采样点，防止地址栏/搜索下拉浮层遮挡中心点导致活动标签误判，同时继续拒绝被完全遮挡的非活动标签。
- `MenuUiChecks` 逐帧展开检查等待实际终帧（上限 2 秒），`NativeUiChecks` 测试窗口与生产启动一致调用 `PreparePresentation()`，保留首帧 `< 250ms` 断言。

测试与恢复：187 项 xUnit、发布与隔离安装各 191 项 UI/Shell、67 项压力、13 项安装冒烟、2 项外部输入、3 项多级侧边输入全部通过。日常安装保持原 1.1.0（DLL `A0314275…`），配置哈希 `6484BEFA…`、自启 `null` 与 `baseline-1.4.0.json` 一致，详见 `environment-restored-1.4.0.json`。
