# BetterCM 1.1.0 交接

2026-10-14 已完成源码、测试及根目录安装包。SHA256 `306A37C26897306EE04D3765BE075F418576B6E6C02DB8F09673D35C141A336E`；发布 DLL `A0314275C6F57F3322F1B6125B7C2BD2C50526AC024930D77B66800E2984292A`。QA-1.1.0.md 为当前验证，REVIEW-1.1.0.md 为本轮 DeepSeek 观察的核实与取舍。

162 xUnit、发布与隔离安装各 184 UI/Shell、62 压力、13 安装卸载、2 外部输入、3 侧边输入全部通过，build/publish 无警告错误。备份 Backups/BetterCM-1.0.1-before-1.1.0-20261014-003656.zip，17636 文件逐项校验，旧包单独保存且根目录也保留。旧通用报告移到副本 artifacts/qa/previous-before-1.1.0。

主要改变：

- GroupLoad 回调提交前核对 root generation 与当前 token 请求对象，ShowAt 明确清空 active 引用。合法同会话晚到结果仍缓存；命令表合并保留已发现 ID，不因较早响应删掉别的分组动作。不要把“悬停已取消”当成“原生会话失效”。
- ExpansionAnimator Play/Commit/Render 在同步回调后复查 revision，防首/终帧回调重开时旧清理覆盖新任务；视图在 ResizeNative 后也检查 revision 再更新行。Render 防重入、重复 timestamp、准备帧取消、固定 bitmap 及终点不重排保持不变。
- SyncContentPosition 的 parentMoved 或 drifted 都触发分层同步；MenuWindowSize.Pack 明确 unsigned WORD 合约。不要差 1px 再恢复尺寸，也不要把关键同步延后到空闲队列。
- MeasureContent 单行继续固定密度/DPI/模板缓存；文本明确换行时独立测量不污染缓存。Hint 限宽 120 DIP、省略和完整 ToolTip，底层快捷键仍原文。
- 新的虚拟化行测试覆盖展开中滚到末尾；原有 ID 快照和 Loaded/DataContextChanged 已能接入当前进度，完成后滚动不重播是预期。

ShellMenuSite 的一次性 TCS 保留：原生动作关闭菜单，InvokeAsync 原子预留并移交连接，旧 session 不能再 invoke。新压力检查命令还运行时也拒绝重复调用。editsInFlight=0 的轮询不能等待尚未到达的回调，不能用来替换宽限期。

用户日常安装仍为 1.0.1（05045065…），已恢复后台；配置 6484BEFA…、自启 null 未变。不要声称隔离安装就是用户已升级。运行根目录 1.1.0 包才能更新真实安装。项目没有 Git 仓库。

更早的原生后端、COM site/GIT、原生新建内联编辑、输入钩子及 UI 分层背景见 HANDOFF-1.0.1.md。版本唯一来源为 BetterCM.csproj，AppInfo 从 AssemblyInformationalVersion 读取。不要引入命令行代替 Windows 原生动作，不强退仍有窗口或原生引用的宿主。
