# BetterCM 1.0.1 交接

本文是历史版本记录；当前版本为 1.1.0，见 HANDOFF-1.1.0.md、QA-1.1.0.md 和 REVIEW-1.1.0.md。

## 当前更多项目闪烁修复重打包

2026-10-14 完成，版本保持 1.0.1。最新根目录包 SHA256 `D358CEAB8E897C6968F337103E983D934BF4343707EB51A469B41DE6FF2586B8`；发布 DLL `05045065E47D1C3D53BAEB898BECC5BCF6CFDC6FE92787004E268B73AA77C6CB`。QA-1.0.1-more-fix.md 是当前交付验证，REVIEW-1.0.1-more-fix.md 逐条记录两份审查取舍；下面所有章节是旧构建背景。

冻结：157 xUnit、发布及隔离安装各 170 UI/Shell、61 压力、13 安装卸载、2 外部输入、3 侧边交互全部通过，构建发布无警告错误。完整修改前备份 Backups/BetterCM-1.0.1-before-more-fix-20261013-214105.zip，16981 文件逐项验证，另存旧安装包。

ExpansionAnimator 同一 Stopwatch 驱动高度、行透明度和位移，先 Rendering/Background/DwmFlush 等准备内容帧，准备取消也有 serial 保护；不能在终点 CancelExpansion + Reposition。取消时才恢复复用行，行 DataContextChanged 防止继承前一项的透明度。Rendering 更新必须防重入，因为 HwndTarget 的同步会泵嵌套帧；诊断采样跳过未完成的嵌套帧。

ResizeNative 只在物理尺寸/位置变化时 SetWindowPos。parent 在工作区边缘移动后，透明 child 明确对齐；通知真实未改变尺寸的 WM_SIZE 使 layered HwndTarget 更新坐标并 CompleteRender，不能仅检查 child 当前坐标或只通知 move。否则下一次 UpdateLayeredWindow 会还原旧 screen origin，实测可偏移 3–213 px。内容位图在展开期间保持最终尺寸，动画终点发布最终 bounds。

分组加载以 token->GroupLoad 记录代次、请求及焦点，点击正在加载的分组升级 Focus；过期返回只更新合法会话树而不能抢开。Ctrl+F 使用 FocusSearch 能力判断，紧凑/侧边禁用。MenuKeyboard 正确处理数字及 INS/PGUP/PGDN。

设置配置快照改变时保留编辑但禁止保存，Save_Click 再检查快照，加载已保存值解除冲突；菜单固定成功立即通知设置。剪贴板判定只凭载荷格式，不凭 Preferred DropEffect，保留 CF_HDROP 与 A/W 虚拟描述符。增强模式 reveal 文件使用 PIDL + SHOpenFolderAndSelectItems，目录仍正常打开。

用户实际安装仍为前轮布局性能的 1.0.1，4167C8D1…，已恢复后台。配置 hash 6484BEFA…和启动项 null 未变；隔离安装已卸载。不要声称测试安装等于用户已升级。不要盲目采纳宿主硬退出、视图校验失败后吞错执行等审查建议。

## 当前布局与性能重打包

用户最新要求统一普通/紧凑菜单间距，并加速打开，版本号明确保持 1.0.1。最新安装包仍为根目录同名文件；SHA256 为 `45D4DA6FAF6707B19F9F9290B387E0B124C0104A7C82ADF15F3963000AD1CD00`。完整记录见 QA-1.0.1-layout-perf.md；以下旧章节保留为前一轮背景，不代表最新安装包身份。

冻结验证：149 无界面、发布及隔离安装各 150 UI/Shell、61 压力、13 安装卸载、2 外部输入、3 多级侧边输入全部通过。配对热启动基准普通桌面 369.1→214.8 ms、紧凑桌面 363.0→221.8 ms、文件 230.4→133.1 ms，约降低 39%–42%；不保证全部冷类型访问更快。发布 DLL 为 4167C8D1…。

修改前备份 Backups/BetterCM-1.0.1-before-layout-perf-20261013-192047.zip，12584 文件逐一核对；同目录保存旧安装包。通用旧报告另存 artifacts/qa/previous-1.0.1-before-layout-perf，避免同版本报告混淆。

MenuMetrics 统一根与侧边宽度、密度及 4 DIP 间隙。MenuPlacement 按真实首行偏移对齐，边界不足仍夹在工作区。ShowCascade 必须恢复 Surface.IsHitTestVisible：复用窗口在 Hide 时关闭了输入，若不恢复会出现可见但点不动。隐藏子菜单同时移除 RootVisual，打开前重新接上以按 DPI 测量；否则其旧可访问动作仍能被枚举。每层最多一个复用窗口，根关闭时释放。

经典菜单使用 DeferSubmenus，仅延后本程序的 WM_INITMENUPOPUP/递归读取，不能取消 CMF_SYNCCASCADEMENU：实机发现取消会漏掉系统终端及部分打包扩展的根动作。令牌只在当前 Shell 会话有效；hover/click 读取一个分组，搜索 CompleteMenu 补全，同会话保留命令 ID、启用状态及新建 site。过期结果不会打开旧窗口。增强模式仍完整读取。

ShellClipboard 的格式探测跳过空/文本剪贴板的额外粘贴能力菜单；需要时使用规范动词能力查询。PrepareAsync 只预备传输和上下文环境，目标仍每次重读；PreparePresentation 预创建隐藏窗口。行高度只保存固定模板度量，不缓存菜单或动作，最多 24 项；设置页隐藏时不重复更新诊断。

基准前版源码快照位于 artifacts/baseline-source-layout-perf，只有阶段计时和同一采样入口的附加代码，不含产品优化。最终基准会在完整子菜单核验之后的下一次新请求继续测量；无菜单动作执行，也不模拟鼠标 UIA 命中。原生树全量对比、延迟读取、取消/搜索及复用点击/可访问内容已加入自检。最终报告位置和实际范围见最新 QA 文件。

## 前一轮背景

用户本轮要求小版本修复：呼出菜单时光标与菜单闪烁、Pebrel 打开无效，并排查同类后端动作。本轮完成 1.0.1 源码、回归、安装包及环境恢复，细节见 QA-1.0.1.md。

正式包在根目录 BetterCM-1.0.1-Setup-x64.exe，SHA256 `EEEABB2C16C8EC7122708A029415E161E6EF26C19ED39D03431FDA9B99853C55`。用户当前实际安装仍为 1.0.0.0，已恢复后台；不把开发/QA 进程当成安装后的新版。安装新版沿用配置。未建立 Git 仓库。

修改前完整备份 Backups/BetterCM-1.0.0-before-1.0.1-20261013-145131.zip，10442 文件逐个校验，附 manifest 和 SHA256。不要删除历史交付和备份。

## 核心变化

- Services/BackgroundProcess.cs：CreateProcessW + STARTF_FORCEOFFFEEDBACK，完整 Windows 参数引用；ShellBridge/ExplorerContextService 使用统一启动入口，继承隔离环境，不隐藏宿主将来创建的原生窗口。
- Services/MenuFirstFrame.cs、SurfaceAnimator、ContextMenuWindow 及 Submenus：准备首帧、DWM cloak、Rendering/ContextIdle/DwmFlush 后播放动画；120ms 回退，取消清理，侧边焦点延后。不要重新引入动画后材质重设或让首次可见帧显示缓存。
- Services/ShellMenuSession.cs：仅真实物理背景目录填写 lpDirectory/lpDirectoryW，修复 %V。保持原始命令 ID 及原扩展调用，字符串释放在同步调用结束后；不重试失败动作，防止重复执行。
- Services/ComServiceReference.cs：GIT cookie 可跨线程，IGIT 指针从当前 COM apartment 获取，避免共享 STA RCW。ShellMenuSite 原 site 以 IUnknown 保存，QueryService 查询当前 apartment 的代理。曾试验调用前恢复 site，未解决目录问题，最终已移除此试验；仍在清理时恢复完整原 site。
- 新建仍由 INewMenuClient 提供确切 PIDL，RPC 线程复制，派发绑定 STA 进入名称编辑；生命周期、条件等待、属性宿主保留规则沿用 1.0.0。
- Tests 两个新测试文件以及 Services/ShellLaunchChecks/ComServiceChecks 增加参数及真实 COM/启动覆盖。App 的 --verb-probe 为有限探针：原子写报告后退出，不进入主实例和菜单。

## 最终验证

146 xUnit、发布与隔离安装各 130 UI/Shell、61 压力、13 安装卸载、2 外部输入、3 侧边输入全部通过。发布 DLL SHA256 `3277247BF56C3DC1BA5A2AC8A20227252EE65A78F93CF2B5E087008257685CF4`。不要并行桌面自检/交互夹具/人工菜单操作。

背景启动探针注册 GUID 临时静态菜单，文件/文件夹 %1 和背景 %V 都实际启动并检查 argv，中文、空格和 & 路径正确，finally 清理注册键。真实 Shell 复制/剪切粘贴通过；回收站只读菜单，不清空。实机新建编辑、属性显示、Pebrel 背景调用通过。旧诊断日志有失败记录，结合时间和构建阶段判读。

当前恢复证据 artifacts/qa/environment-restored-1.0.1.json：真实 1.0.0.0 在后台，配置哈希 6484BEFA…，启动项 null，QA 安装已经卸载。有限启动测试及实机操作均使用 artifacts 下自建夹具，没有改用户文件、固定状态或辅助功能设置。Pebrel 未输入命令、未控制工作区。

未逐项执行所有外部扩展；不要声称每个第三方菜单均获验证。保持默认经典换皮设计、Shell 宿主隔离、Shift+右键原生入口，后续先用完整上下文/原生接口排查再考虑替代动作。
