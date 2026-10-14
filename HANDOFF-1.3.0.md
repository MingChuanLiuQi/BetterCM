# BetterCM 1.3.0 交接

2026-10-14。版本唯一来源 BetterCM.csproj，Version=1.3.0。正式安装包在项目根目录 BetterCM-1.3.0-Setup-x64.exe；最终身份和验证见 QA-1.3.0.md、artifacts/qa/release-1.3.0.json。Gemini 反馈的全部取舍见 REVIEW-1.3.0.md。

修改前备份 Backups/BetterCM-1.2.0-before-1.3.0-20261014-032538.zip，17857 文件逐项核对大小与 SHA256；旧 1.2.0 包另存，旧报告副本在 artifacts/qa/previous-before-1.3.0。保留历史包、文档与备份，本轮未初始化 Git 或移动旧产物。

关键实现：

- ShellViewBinding.OpenCore 共用于上下文捕获与执行。ShellWindows 条目的 HWND/GA_ROOT 只筛选候选。对 QueryActiveShellView 返回的原生视图，检查框架内按 Z 序递归命中的实际文件区域；不要退回仅 IsWindowVisible 或第一个 browser.Document。本机非活动标签保留 WS_VISIBLE，初版实际读取错 A/B 的失败证据在 tab-b-development-1.3.0.json。最终从同一视图 GetItemObject(SVGIO_BACKGROUND, IID_IDispatch) 读取 Folder/SelectedItems，ValidateActiveView/ValidateLocation 每次执行再检查。桌面保留独立分支。
- 同目录双标签实际选择 A-one/A-two，第二标签捕获精确目标；持有旧 ShellViewBinding，切换标签后拒绝，即使位置字符串一样。只读探测入口 --explorer-view-check <report> <folder> <file> [--hold]；hold 写 .ready.json，外部实际点击切换后创建 .continue 触发校验，等待上限 120 秒。探测不会注入输入或执行文件动作。
- AppearancePreview 请求持有全局临时外观，准备取消、打开设置、新右键、未 ShowAt 完成的 finally 都恢复；旧 finally 只能恢复自己的请求，恢复时读取最新保存配置。
- Search_Changed 先对已加载节点 Rebuild/Reposition，再补全一次原生树；ShowAt 先设置 _appliedSearch。取消后的空列表是当前搜索的结果，不能要求旧未过滤列表仍留在屏幕上。
- NativePasteMenu 只匹配规范化的根叶子，原位保留 caption/mnemonic/hint，使用 _pasteSession 命令基址映射；已有规范动词不重复。无占位时编辑区优先，否则最后分隔线/属性前；无文本原生项不编造标签。
- MenuDetails 图标和大小独立发布，2 秒等待及取消迟到结果；OptionalProbe 每类只在飞一项，底层同步 I/O 不可安全中断，超时后不无限排队。SetDetails 保留代次守卫。
- AppLog 轮转与追加在同一跨进程 Mutex 中，2 MiB 阈值，只保留当前和上一份；异常条目限长，日志失败仍不干扰操作。

新增逻辑检查包括预览所有权、元数据独立超时/取消/不积累、粘贴规范化/原位/不重复及日志轮转。新增 UI 检查在延迟树补全未完成时即时过滤、继续输入不重复查询。既有复制/剪切粘贴、背景 verb、原生新建 site、动画、设置及宿主保留检查持续运行；测试不清空用户回收站。

每个 GUI/实际输入/压力/安装自检串行运行，避免互相造成失焦。verify.ps1、Build-Installer.ps1 不跳过本地桌面门禁，安装验证用 TestBuild/-Isolated。最终 xUnit、UI/Shell、压力、输入、活动视图及安装报告的完整计数以 QA-1.3.0.md 为准。

本轮起始实际安装为 1.1.0（A0314275…），与项目 1.2.0 不是同一版本；恢复见 baseline-1.3.0.json 和 environment-restored-1.3.0.json。配置 6484BEFA…、自启 null 未修改。不要把隔离安装当成用户已升级；运行新包可升级。

本轮没有关机请求，不重复上轮的一次性关机操作。全部第三方扩展、不同 Windows 构建、混合 DPI、多屏、远程桌面、长期离线网络未全面验证；没有重新发布性能百分比。图标序列化、增强延迟规则、宿主排空和跨目录原生能力暂缓，理由在 REVIEW。既有同步内容定位、动画终点、命令会话身份与原生任务生命周期不要削弱。
