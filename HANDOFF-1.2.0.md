# BetterCM 1.2.0 交接

2026-10-14。当前版本来源 BetterCM.csproj 的 Version=1.2.0。发布包在项目根目录 BetterCM-1.2.0-Setup-x64.exe，身份和最终检查见 QA-1.2.0.md、artifacts/qa/release-1.2.0.json；审查判断见 REVIEW-1.2.0.md。

修改前备份 Backups/BetterCM-1.1.0-before-1.2.0-20261014-010822.zip，17714 文件逐项核对大小和 SHA256。旧 1.1.0 包另存，历史包和备份保留。旧通用报告副本在 artifacts/qa/previous-before-1.2.0。

本轮新增：

- ShellResponse.Superseded 表示读取所属会话或当前连接已被取代，App 先查请求归属再处理 Success。分组/搜索的 OperationCanceledException 静默结束；真实分组异常必须同时符合根代次、可见性、active GroupLoad、请求号及 token 对象身份。合法同会话晚到成功仍可缓存，不能一律丢掉。
- ShellConnection 已有全程 SemaphoreSlim 串行化及 InvocationReserved 宿主保护；没有添加 Expand 引用计数或读取移交。并发夹具证明旧读取取消但执行仍成功；已开始原生任务继续保留。真实断连会清理当前连接，Open 跳过 Closed，下一次右键直接恢复。
- NativeMenuState 属于当前根菜单代次。命令按 ID TryAdd，先到者胜；无搜索行快照在树/新命令变化时失效，搜索不覆盖完整快照。ShowAt Reset 清掉旧委托；即使命令 ID 相同，下一会话也使用新的动作。不要跨右键缓存原生命令、选区或启用状态。
- 产品操作说明为“选择动作后关闭菜单，再次右键继续”；一次调用的新建 TCS、COM site/GIT、原生名称编辑仍保持，不添加命令替代原生能力。
- UI 自检等待实际终帧，保留首帧 250ms 断言。焦点意图在正常优先级加载提交后检查，持续前台行为由实际点击夹具验证，避免后台启动器无前台权限产生正常失焦关闭时误判。

测试入口：verify.ps1 / Build-Installer.ps1；安装测试仅用 TestBuild 包和 Installer/Test-Installer.ps1 -Isolated。测试报告在 artifacts/qa，源文件和报告哈希汇总见 release-1.2.0.json。自检菜单读取会接触实际 Shell；复制/移动仅使用独立夹具，回收站不执行清空。

本轮起始用户实际安装为 1.1.0（DLL A0314275…），测试不替换日常安装。恢复状态见 environment-restored-1.2.0.json；运行根目录新包才能升级真实安装。配置哈希 6484BEFA…、自启值 null 应与 baseline-1.2.0.json 一致。

用户明确要求交付后关机，最终交付阶段另记录 shutdown-1.2.0.json；隐藏 helper 等待约 60 秒后发出普通关机请求，不强制关闭应用。不要在后续接手时自动重复关机。

既有动画/透明内容位置同步、每层 HWND 复用、CMF_SYNCCASCADEMENU、原生新建/属性/剪切粘贴及 Pebrel 背景路径机制见 ARCHITECTURE.md 和之前交接。项目无 Git 仓库；全部第三方扩展、混合 DPI 和远程桌面未全面实测，本轮没有发布新的性能百分比。
