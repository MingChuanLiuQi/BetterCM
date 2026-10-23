# 1.9.0 功能与边界修正 — 2026-10-20

按用户提供的 DeepSeek 静态审查意见核对现有工作树后修复。版本号保持 1.9.0；保留任务开始时已有的全部未提交修改。本轮验证针对当前工作树，不复用历史安装包的验证结论。

| 审查问题 | 当前行为 |
| --- | --- |
| 应用内固定、托盘暂停被误报为外部冲突 | 应用内保存携带原配置基线；设置页按字段合并并保留未保存编辑，固定/隐藏集合按键合并。真实外部变更仍启用原有冲突保护。 |
| 文件监听重载自己的写入 | ConfigStore 比较实际磁盘内容与最近一次已加载/保存内容，仅在内容变化或恢复状态异常时重载。 |
| 异常暂停不能恢复 | 显式启用并成功保存后解除异常暂停；输入服务曾被销毁时重建服务与事件绑定。外部重载不会解除异常保护。托盘提示读取实际接管状态，设置页显示异常暂停与恢复方式。 |
| 自定义批量动作显示 SHA256 | 进度窗口支持标题及副标题；哈希保持 SHA256 文案，自定义动作用通用启动标题和动作名称。 |
| 原生动作缺乏选区条件 | 剪切/复制/删除要求选区，重命名要求单选；成功时绑定真实 Shell 能力，失败时移除全部无法绑定的原生动作。 |
| 经典模式 Shell 失败只剩报错 | 仅本次菜单回退到增强动作，展开可用内置/自定义动作并解释 Shift+右键入口；不修改保存的经典模式偏好。 |
| Clipboard.Flush 导致已成功命令误报失败 | Shell 命令成功后单独保护 Flush，并记录持久化失败。 |
| 展开子菜单改变设置页系统项目来源 | 按层展开保留根项目快照；完整根菜单重载才更新 LastShellItems。 |
| 菜单预览异常静默 | 观察异步任务，捕获并记录异常，同时通过错误通知反馈；被取消的菜单请求正常结束。 |
| 自定义动作 ID 冲突 | 拒绝空白、重复 ID，并拒绝与内置 ID、shell:/native: 命名空间冲突。 |
| 自定义程序检查阻塞 UI | 后台探测，同一路径共享进行中的检查；最多四个并发文件系统探测，超出上限时在 250 ms 预算内排队等待空闲槽位，超时/繁忙显示原因并禁用该动作；重新打开菜单重试。 |
| 子菜单首帧指标和文件打开失败 | 子菜单正确记录 FirstFrameHeld；打开配置/日志失败在设置页显示原因，不升级为全局异常。 |
| 搜索及空分组 | 搜索框 Up/Down 选择最后/第一个可用动作；Esc 先清空搜索，再次关闭。已加载的空原生分组禁用，尚待加载的分组保留展开能力。 |
| 管理员脚本工作目录与自定义动作体验 | 管理员运行 `.ps1`/`.bat`/`.cmd`/`.msi` 显式传入目标所在目录为工作目录；`StaWorker` 覆盖初始化竞态释放；设置页隐藏再呼出保留未保存草稿，自定义动作支持文件浏览自动回填默认标题及上下移动排序。 |
| 菜单云母/亚克力实时透色与遮罩调优 | 为 `mica` 与 `acrylic` 菜单统一启用 `DWMWA_USE_HOSTBACKDROPBRUSH` 实时后方画面采样与高斯模糊，显式调低深浅色 `TintOpacity` 与 `LuminosityOpacity`，修正 `ContextMenuWindow` 句柄样式与 DWM 边框扩展初始化时序。 |

新增 20 项参数化后计数的 xUnit 回归（共 309 项），以及 17 项 WPF/真实 Shell 检查（共 293 项），涵盖应用内配置合并、隐藏重开保留草稿、自定义动作浏览回填与排序、快速探测排队、管理员脚本工作目录、STA 线程立即释放、系统项目勾选同步、经典/增强模式跨目录选区失败、上下键与 Esc、进度标题以及异常暂停后的实际鼠标接管恢复。桌面回归在隔离配置目录执行，保存并恢复配置及备份，不修改用户开机启动项。

跨目录选区通过增强动作回退处理，本轮没有实现跨目录原生 Shell 菜单。暂停时保留鼠标观察钩子，因为通过热键打开菜单仍需要外部点击关闭；启动时的同步 hook 就绪等待仍保留。英文资源、ARM64、屏幕阅读器实测属于后续产品工作。

本轮验证结果和产物见项目内 `artifacts/review-fix/`。正式安装包签名、干净虚拟机安装及远端发布不在本轮验证范围。

完整验证命令：`verify.ps1 -PublishDirectory artifacts\review-fix\publish`（2026-10-20 交互桌面下 303 项 xUnit、67 项脚本、291 项 UI/Shell 全部通过；2026-10-23 补充迭代后 `verify.ps1 -SkipDesktopChecks` 下 Release/Debug/QA 零警告构建、QA/Release 各 309 项 xUnit、67 项脚本、正式二进制 QA 隔离以及沙盒内全部 17 项 `ReviewFix` UI 自检均通过）。首次新增桌面检查有两项因为未展开搜索框而不能获取焦点，修正测试前提后重新运行完整流程通过；本轮不声称连续两次无重试。

证据：`artifacts/review-fix/verify.log`、`published-tests.json`、`headless-tests.trx`、`headless-release-tests.trx`、`script-tests.json`、`dependency-audit.json`、`production-isolation.json`、`verified-publish.json`。已验证的自包含可执行文件为 `artifacts/review-fix/publish/BetterCM.exe`。

## 安装包补充

2026-10-20 从当前工作树生成逐文件校验的独立干净源码快照 `5f3ecaaeef8bbbda265f0c83997ec5844469205f`，再次通过完整 verify，再执行原有 `Build-Installer.ps1 -ReuseVerifiedPublish`，未绕过干净源码及载荷校验门禁，未提交主工作区。

正式身份的未签名 x64 候选安装包：`artifacts/releases/1.9.0/BetterCM-1.9.0-Setup-x64.exe`（同步更新至 `artifacts/candidates/1.9.0-functional-fix/BetterCM-1.9.0-Setup-x64.exe`），61,207,329 字节（58.37 MiB）。SHA256：`105A68CF722D6AA733AAA5068E057BDF2849C39C2415B08C9A12E2B20D5D411A`。目录内附校验文件、构建信息和源码快照 Git bundle，旧候选保留。

随后同快照编译 QA 独立身份安装包，13 项安装/启动/单实例/退出/卸载检查及安装目录内 291 项自检通过；用户配置及原有启动项保持不变。没有用正式身份 Setup 替换用户现有安装，也不声称完成干净 VM 验证。证据位于 `artifacts/review-fix/installer-evidence/`，编译与安装测试日志分别为 `build-installer.log` 和 `check-installer.log`。源码清单及实现一致性见 `installer-source-manifest.json`、`installer-result.json`。
