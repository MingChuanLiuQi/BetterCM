# BetterCM 1.4.0：二次复查的核实与处理

2026-10-14。针对 1.3.0 二次只读复查提出的 4 项细节隐患逐一核实、修复并补充回归验证。

## 四项问题核实与修改

| 项目 | 判断、修改与验证 |
| --- | --- |
| OptionalProbe 忙时直接返回 null 导致快速连续右键丢失图标/大小 | 确认为真实竞争。1.3.0 的 `Interlocked.CompareExchange` 在前一次数毫秒的正常 STA 图标提取或线程池 `FileInfo` 尚未返回时直接放弃本次读取。1.4.0 改为在 `OptionalProbe.ReadAsync` 中保存当前在飞 `Task`：后续请求在自身剩余预算（默认 2 秒）和 `CancellationToken` 内等待前一次探测结束，随后立即执行本次读取；若前一次探测因离线网络真正卡死超时，后续请求在等待到期时返回 `default` 而绝不启动第二个底层 I/O。新增两项可控单元测试覆盖快速连续读取等待成功与卡死探测超时不累加。 |
| CompleteSearchAsync 在清空搜索退回根菜单后强制 Rebuild 打断悬停与子菜单 | 确认为真实交互边界。当用户输入搜索词触发 `CompleteSearchAsync` 后又在补全返回前清空搜索框并打开某个根分组子菜单时，无条件 `Rebuild()` 会替换 `ActionsList.ItemsSource` 并关闭或脱离当前悬停/展开状态。1.4.0 在 `Search.Text.Length == 0` 时仅向 `_nativeState` 合并完整命令与节点，并对当前 `VisibleRows` 中仍带 `DeferredSubmenu` 的分组行原位填充 `Children`，保持根列表容器引用和已展开子菜单不变。新增 UI 回归检查验证容器引用保持、子菜单不关闭且延迟分组完成原位注入。 |
| NativePasteMenu.Merge 移除占位项未限定根层级 | 确认为真实边界。在增强模式（`deferSubmenus: false`）下子菜单项目先于后续根项目进入 `items`，若扩展子菜单复用了与根占位项相同的数字 `Command`，`items.FindIndex(x => x.Command == original.Command)` 会误删子菜单项。1.4.0 收紧为 `x.Group.Length == 0 && x.Command == original.Command`，并新增同名/同编号子菜单项保留测试。 |
| ShellViewBinding.IsActiveView 仅采样视图中心单点 | 采纳多点采样加固。提取 `ShellViewBinding.IsViewHit`，优先检测视图中心点 `(1/2, 1/2)`，未命中时回退检测下半区 `(3/4, 3/4)` 与 `(1/4, 3/4)`，规避快捷键呼出时顶部地址栏/搜索下拉浮层遮挡中心点导致活动标签误判，同时非活动标签在全区域被活动标签覆盖仍被拒绝。新增纯逻辑单元测试覆盖顶部遮挡恢复与全区域遮挡拒绝。 |

## 测试夹具加固

- `MenuUiChecks` 的逐帧展开循环原本使用固定 `Task.Delay(400)`，在安装包解压后系统防病毒扫描带来的瞬时 DWM 合成延迟下可能停在 `0.959` 而未采到终帧；现改为与 `NativeUiChecks` 一致的有界等待实际终帧（`while (menu.IsExpanding && expansionWait.ElapsedMilliseconds < 2000)`）。
- `NativeUiChecks` 的测试窗口在首次计时 `ShowAt` 前调用 `window.PreparePresentation()`，与生产环境 `App.OnStartup` 预创建菜单句柄的生命周期保持一致，保留首帧 `< 250ms` 断言不放宽。
