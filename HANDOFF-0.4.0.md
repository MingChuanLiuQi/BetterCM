# BetterCM 0.4.0 项目交接

日期：2026-10-12；工作区：C:\Users\zky89\Desktop\文件夹\BetterCM；基线：HANDOFF-0.3.0.md。

## 本轮完成

已按用户四张截图完成 0.4.0：搜索聚焦隐藏提示、移除延迟原生菜单补发、最新请求取消/代次保护、虚拟 Shell/导航树覆盖、Mica Alt 加深，以及最后一张截图的七项健壮性优化。普通右键仍在 WH_MOUSE_LL 中接管完整按下与释放，不先显示再隐藏系统菜单。

上下文读取从常驻 STA 移入可取消 --context-host 进程，1800 ms 时限真正废弃卡住捕获。SelectedContext 分离物理目标和 ShellParsingPaths，避免把此电脑等虚拟对象传给路径动作。SourceWindow 不参与 JSON，在主进程恢复窗口句柄。

ShellBridge 保留当前浏览连接及最多一个预热备用；invoke 将原会话连接移入独立执行集合，open 使用备用。RequestId 关联响应，SessionId 保证命令身份。所有调用与排队有时限；invoke 发出后不 Kill 宿主或子进程。通信断开后宿主在命令完成时自行退出。SemaphoreSlim 在所有在途请求释放后销毁。属性复用菜单的加载任务与原始 Items/Session。

搜索提示判断 Text、Visibility、IsKeyboardFocusWithin。Mica DWM 值改为 4（Mica Alt），表面色层更轻，Acrylic/solid 回退保持。新默认关闭 70 ms，旧显式设置保留。batch 配置要求 ParseArguments 后有独立 {paths}。MenuComposer 启用显式比较和 CA1307/CA1310。HoverExpansion 用单调时钟保护实际延时。

## 备份与交付

修改前完整备份：Backups/BetterCM-0.3.0-before-0.4.0-20261012-092913.zip；1125 文件逐一 SHA256 验证。归档 SHA256：05B241625726A9E83DDB94678B017A8E3341450A77DE099DA8CFACA855B248D0；同目录有 .manifest.json 与 .zip.sha256。

正式包：BetterCM-0.4.0-Setup-x64.exe；53,537,002 字节，自带运行时，文件版本 0.4.0.0。
SHA256：27D10BE98DB354B293A4E946FE8B7EC43FA8E06269A2C1804AEC9DA45ED51668。

当前正式安装仍为 0.3.0，已恢复后台运行；用户需要双击新包升级。实际用户配置、自启项未修改。旧包和历史备份保留。工作区仍不在 Git 中。

## 验证

最终发布与隔离安装各 138/138 组件检查，隔离安装 13/13，真实输入夹具 2/2。Shell/捕获压力 58/58（40 秒假 verb、30 秒超时宿主存活、第二次 open 33.79 ms、50 次关联响应、卡住捕获约 1806 ms、下次捕获约 249 ms）。压力负载早于最终仅涉及悬停的修复，所测 Shell/捕获实现相同。详见 QA.md，报告在 artifacts/qa。

实机观察文件右键、此电脑导航、固定下载节点及此电脑背景；后两份观察 JSON 的 NativePopups=0。不要把通信压力检查等同于实体高频桌面连击。多显示器/混合 DPI/远程桌面/所有第三方扩展仍未全面覆盖。

## 关键源码

新增：Services/RobustnessChecks.cs、.editorconfig。
主要变化：App.xaml.cs、Services/InputService.cs、ExplorerContextService.cs、ShellBridge.cs、ShellMenuSession.cs、ThemeService.cs、ActionExecutor.cs、MenuComposer.cs、HoverExpansion.cs、SelfTests.cs、MenuUiChecks.cs、InteractionChecks.cs；Models/SelectedContext.cs、ActionCatalog.cs；Config/AppConfig.cs、config.json；Views/ContextMenuWindow.xaml；版本/安装/文档。

Views/CustomActionWindow.cs 沿用 ConfigStore.Validate，batch 错误会在对话框内显示，因此无需重复维护校验逻辑。

## 后续边界

背景优先 CreateViewObject(IContextMenu)，接口不支持时使用文件夹对象菜单；此电脑只读到其自身导出的两个项目，未完整重建查看/排序菜单。导航路径不明确时拒绝执行，不猜路径。未来可补更广的 namespace/多标签/右键拖动实机矩阵。保持命令执行超时不 Kill 的原则，避免中断用户任务。
