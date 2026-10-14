# 1.0.0 审查处理记录

依据用户提供的 DeepSeek 0.9.0 审查，对照修改前代码核实后实施。审查意见是参考资料，未直接当成已证实缺陷或补丁指令。保持默认忠实映射经典菜单、WPF、隔离 ShellHost 和原生命令执行语义。

| 意见 | 核实与处理 |
| --- | --- |
| P0：每次 invoke 在回复前固定阻塞 3.6 秒 | 根因链不成立：ShellHost 在循环内 WriteLine/Flush 回复，finally 的等待在客户端关闭管道后执行；ShellBridge.InvokeAsync 还会把当前宿主移到独立 invocation 集合，下一次 open 使用备用连接。确有非新建背景命令在清理阶段空等 3 秒，已修：BeginInvocation 跟踪运行时新建服务请求或回调，仅相关动作等完成/1200ms。普通动作让出一次 dispatcher 后清理，保留 600ms 原生收尾。新增真实刷新往返和宿主退出检查。 |
| P1：高对比度永久污染色板 | 0.9.0 正常分支每次重写 MenuSurfaceBg 及其他资源，随后刷新所有窗口，未证实永久污染。重构为生产共用 ApplyResolved，补高对比度往返检查；纯色 HwndSource 底色也使用系统 WindowColor。没有修改用户系统辅助功能设置。 |
| P1：搜索深层命令不可直接看到和执行 | 确认体验问题。搜索直接输出叶子，显示完整父路径，例如 NanaZip · SHA-256；匹配祖先自然包含子叶，空组不进入结果。未搜索的原生树完全保留。ID 一直使用完整递归路径，父级禁用向下传播。新增深层路径、空组和 ID 回归。 |
| P1：设置初始禁用态 | 真实首次 ShowSettings 原本会 RefreshSettings，报告关于已可见窗口“从未初始化”的推断不完整。为消除事件顺序依赖，构造时完整加载配置，并显式 RefreshModeAvailability；增强按钮和更多延时同时禁用。已可见设置保留未保存编辑，外部配置更新显示提示，不无条件覆盖。 |
| P2：图标串味、整表清空 | 确认。IconHelper 对存在的文件/目录读取实际 Shell 图标，而旧缓存却按扩展名或统一 folder 键，可能混用自定义图标。改为每个路径独立键，256 项 LRU 逐项淘汰，成功缓存 120 秒、失败缓存 5 秒。 |
| P2：Ctrl+C/X/V 恒失效 | “恒失效”不成立：增强模式通过 ShellCommandMap.Bind 保留 copy/cut/paste ID，经典物理文件有规范动词。确有远程/第三方 handler 省略规范动词，已改为优先匹配原生打印 Shortcut，再对根级规范动词回退；禁用动作不执行。Ctrl+Z 等打印加速键也统一解析。经典 Enter 只选中/默认，不任意回退首项。 |
| P2：导航标记没传、有选区必定误认 | 0.9.0 钩子已在 SysTreeView32 分支传 PointerActivation=true / NavigationActivation=true；Capture 在读取文件选区之前解析导航项，因此必定误认的结论不成立。补强 UIA TreeItem 优先判断，防原生子窗口类变化。实测文件保持选中时右键下载仍绑定下载和 Home 固定状态。 |
| P2：native: 命令号与内置 ID 冲突 | 不成立：命令号为 int，native:7 不可能等于 open/copy 等字符串；命名空间已经隔离。保留现有协议和动作 ID，增加断言。 |
| P2：父可用、所有子禁用时应灰掉父组 | 未采纳：经典模式应保存 HMENU 的父项状态，不能按子项重算 Windows 已返回的启用状态。增加保真测试；搜索叶子仍继承父级禁用。 |
| P3：跨进程日志丢行 | 已改为按数据目录确定的具名 Mutex 串行追加并加入 PID。只放宽 FileShare.ReadWrite 不足以保证多个 append 不交错。压力检查用三个独立宿主写 36 条日志，核对全部保留。 |
| P3：设置和启动项回滚 | 已改配置成功后再写启动项。失败时分别尝试恢复原始 JSON 文本、内存配置及原启动项值/类型；一处回滚失败不阻止另一处，明确返回失败细节。测试用临时配置与模拟注册表写入，没有改真实启动项。 |
| P3：热键失败通知 | 主热键注册失败显示托盘警告，含实际主/备用/右键状态，相同状态不重复通知。启动时待托盘创建后报告。 |
| P3：宿主 15 秒强制退出 | 不采纳强制结束：原生属性对话框、进度窗及线程引用需要宿主继续泵消息。改为 15 秒后记录保留原因，继续等待原生任务完成。 |
| P3：死代码与未用声明 | 未进行无收益的批量公开 Win32 声明清理。F5 在经典背景走绑定视图刷新，增强模式仍需要 refresh ID；它不是普遍不可达。保留有用兼容分支，避免为清理改变行为。 |
| P3：无交互 CI 桌面检查 | verify.ps1 增加显式 -SkipDesktopChecks，GitHub Actions 仅构建/xUnit/发布；本地默认和 Build-Installer.ps1 仍运行完整 UI/Shell 门禁。远端工作流未运行，不能声称远端 CI 已通过。 |

实测额外发现：拼音状态下仅 WPF InputMethod 属性不足以阻止空材质父 HWND 吞字母。已对该空父 HWND 解除 IME 关联，独立搜索 HWND 保持中文输入。W 访问键、新建文字/文件夹的名称编辑、搜索中文候选均已实测。

宿主生命周期取舍依据 [Microsoft SHSetInstanceExplorer 文档](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shsetinstanceexplorer) 和 [线程引用管理文档](https://learn.microsoft.com/en-us/windows/win32/shell/managing-thread-references)；窗口输入法隔离依据 [ImmAssociateContext 文档](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immassociatecontext)。

最终检查、证据和限制见 [QA-1.0.0.md](QA-1.0.0.md)。
