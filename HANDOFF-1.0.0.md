# BetterCM 1.0.0 项目交接

目录 C:\Users\zky89\Desktop\文件夹\BetterCM，无 Git 仓库。基线 HANDOFF-0.9.0.md；用户提供 DeepSeek 审查作为参考，要求修复后打包 1.0.0 正式版。代码、测试、打包和原环境恢复已完成。

## 交付

根正式包 BetterCM-1.0.0-Setup-x64.exe：61,169,360 字节，SHA256 `5BD7A91756CE8714A9A5859104A31CBD071799C9CC1A81E523728778F0CB2BCB`，附校验文件。修改前完整备份 Backups/BetterCM-0.9.0-before-1.0.0-20261013-135415.zip，9870 条逐一核对通过，3,538,436,185 字节，SHA256 `78B59A15225B620E8FE05BCC10F82B70E690D20693FC6F8C740A72313820F42A`。

140 xUnit，发布/安装各 123 UI/Shell，59 压力，13 安装卸载，2 外部输入，3 侧边输入全部通过，构建零警告错误。冻结代码最终 DLL SHA256 `94F4A50A0C7EA6DB4E5E0C32602FF627308F5C09FC763D3901D3B1C9AC180F81`，与 dev-ime 的真实新建和输入法测试构建相同。最终报告和截图在 artifacts/qa，详见 QA-1.0.0.md。

真实安装基线为 0.9.0.0，已恢复后台，配置哈希 6484BEFA20077EAC79805F453C9A10A6C21B7E4AB79494A2EAC37CE83CCD24C6、启动项空值保持不变，隔离安装卸载。不要声称自动安装 1.0.0。版本唯一来源 csproj 1.0.0；程序和包 1.0.0.0。

## 关键变更

- NativeMenuComposer 无过滤保持原 HMENU 树；搜索输出可执行叶子及完整祖先路径，父禁用向下传播，不显示空组，全路径 ID 不重置。匹配祖先时自然列出其叶子。默认继续 ClassicMenu=true，不注入原创动作。
- MenuKeyboard 解析原打印 Shortcut（Ctrl/Control/Alt/Shift、F 键、Enter/Delete 等），优先于根级规范 verb 回退；禁用动作不参与；经典 Enter 用选中或 IsDefault，无任意第一行回退。增强模式仍按现有内置 ID，不删除有效 refresh 路径。
- ShellMenuSite.BeginInvocation 区分枚举期与执行期的 New 服务请求；QueryService 在执行期置标记，SelectAndEditItem 计数。WaitForNewCallbackAsync 先让一次 Background dispatcher；只在有执行期服务请求/回调时等编辑完成或 1200ms，随后 DrainCallbacks。普通动作不固定空等 3 秒。
- ShellHost 仍在循环内立即 WriteLine/Flush 回复，然后客户端断管道后 finally 清理。ShellBridge 当前/备用/invocation 独立；审查关于“回复前阻塞”的链不成立。保持 ReleaseCommands → lifetime.Drain → DrainCallbacks → Dispose 顺序。保留 600ms 原生收尾，15 秒后仅记录 retained-work，不强制结束还存在 UI/线程引用的宿主。
- ContextMenuWindow.SourceInitialized 对空材质父 HWND 调用 ImmAssociateContext(hwnd,0)，独立 WPF 内容 HWND 不受影响。该父窗口短暂原生焦点曾让拼音吞 W；修正后真实 W 打开新建，Search 仍有中文候选。WPF 根/Surface=false、Search=true 的输入法作用域继续保留。
- ThemeService.ApplyResolved 是生产及 QA 共用的明确主题参数入口，不更改系统设置。原正常色板已经重写，不存在已证实的永久污染；补往返测试，HwndSource 纯色高对比底色用 SystemColors.WindowColor。
- MainWindow 构造完成即 RefreshSettings；RefreshModeAvailability 显式禁用经典下的增强网格/命令按钮/更多延时。ShowSettings 对已可见窗口刷新启用态但不覆盖未保存编辑。外部更新显示提示，RestoreSaved 加载最新配置。
- SettingsPersistence 配置先保存再写启动项；startup 失败分别恢复原始 JSON 文本/内存配置和启动项值/类型，单处回滚失败不阻止另一处并明确提示。StartupService.Snapshot 保存精确原值；不是只回滚 enabled 布尔值。
- IconCache 按路径区分文件和文件夹；BoundedCache 为 256 项 LRU，成功 120 秒、失败 5 秒，有效命中移到队尾，满时只删最旧。现有 STA 图标抽取机制保持。
- AppLog 使用按数据目录哈希的 Local\BetterCM.Log.* 具名 Mutex，跨进程串行追加并含 PID；压力夹具三个独立宿主写 36 行验证。不要用简单 FileShare.ReadWrite 代替跨进程串行追加。
- InputService 记录 PrimaryHotkeyAvailable，App 在托盘创建后和配置应用时报告主热键失败，同状态去重。ExplorerContextService 遇真实 UIA TreeItem 优先导航，兼容原生类名变化；原 InputService SysTreeView32 已传导航标记，未重写其正确链路。

详细审查真假及未采纳原因见 REVIEW-1.0.0.md。native:<int> 已是隔离命名空间，不会等于 open/copy；可用父组所有子禁用时仍保留 HMENU 父状态，不擅自灰掉。QQ 音乐等原注册项目保留。

## 验证与注意

verify.ps1 默认完整本地门禁；-SkipDesktopChecks 只用于 CI 明确跳过 UI/Shell，仍构建、xUnit、发布。Build-Installer.ps1 不跳过；GitHub Actions 调用 -SkipDesktopChecks，远端未执行。版本和编译排除继承 0.9.0，不改 UI 技术栈，不上 NativeAOT，不去 ShellHost。

普通背景刷新真实 invoke 6.83ms、宿主 737.09ms 退出。新增等待修正没有牺牲新建回调：文本和文件夹都实测名称编辑及 flags 0/3。原 GIT、绝对 PIDL、ILClone、IShellView vtable 及生命周期规则继续看 0.9.0 交接。

首次外部输入检查与安装端桌面检查重叠时断言失败，保留 input-1.0.0-overlap-attempt.json；之后独立复验 2/2、侧边 3/3 通过。以后安装自检、真实菜单和输入夹具按顺序运行，不能当作相互独立任务并行。压力 IPC 检查可以独立运行，但也使用独立数据目录。

实际 UI 仅专用夹具，无用户文件删除/回收站清空/固定状态改变。高对比与注册表失败使用受控入口/模拟注入。仍有经典文字菜单接口、自绘/现代扩展、混合 DPI、多标签、远程桌面覆盖边界。包未代码签名。历史失败诊断日志不可整份当作最终结果。

正式输出只用根安装包和 artifacts/publish/win-x64；artifacts/dev-ime-1.0.0 是 QA 开发输出。下一次修改先备份本轮；有源码变化需重新完整验证并打包。
