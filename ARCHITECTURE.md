# 1.4.0 连续探测排队、搜索退回原位注入与多点视图命中（当前构建）

OptionalProbe 保存当前在飞 Task，ReadAsync 在调用方剩余预算（2 秒）与 CancellationToken 约束下等待前一次正常读取完成再启动新读取，消除快速连续右键因前一次 5–20ms 本地读取尚未结束而直接返回 null 丢失图标或大小的问题；若前一次读取真正卡死超时，后续请求在等待预算耗尽时直接返回 default 而不启动第二个底层同步 I/O。Start(read) 仍保持零等待即时非阻塞契约。

CompleteSearchAsync 在延迟补全返回时若用户已清空搜索框退回普通根列表（Search.Text.Length == 0），只向 NativeMenuState 合并新命令与完整树并原位填充当前根行的 DeferredSubmenu/Children，不重建 ItemsSource、不替换已悬停的容器，也不关闭用户已展开的侧边子菜单。

NativePasteMenu.Merge 查找并替换占位命令时显式要求 Group.Length == 0，防止增强模式递归读取子菜单时误删复用相同数字 Command 的子菜单项。ShellViewBinding.IsActiveView 通过 IsViewHit 先测视图中心点，未命中时回退检测下半区左右采样点，规避快捷键呼出时地址栏/搜索下拉浮层遮挡中心点导致活动标签误判，同时非活动标签在所有采样点仍被当前活动标签完全覆盖而拒绝。

# 1.3.0 活动视图与预览生命周期（前轮构建）

ExplorerContextService 与 ShellViewBinding 使用共同的原生视图选择路径。ShellWindows 项的 HWND/根框架只用于筛选候选；IShellBrowser.QueryActiveShellView 获得视图，按框架内部子窗口 Z 序命中其区域，排除仍有 WS_VISIBLE 的旧标签。由同一 IShellView.GetItemObject(SVGIO_BACKGROUND, IID_IDispatch) 获得 Folder/SelectedItems。执行再校验活动视图窗口、位置及精确选区，同目录标签也不混用。对桌面保留独立命名空间分支。

AppearancePreview 以请求号持有临时外观，准备取消与未显示就结束也恢复当前保存值，旧请求 finally 不能恢复新预览。搜索先过滤已加载树，再补全；初始化时先同步 _appliedSearch。NativePasteMenu 只匹配规范化根叶子，保留 native caption/mnemonic/hint，命令映射仍调用原 _pasteSession。

MenuDetails 独立等待图标和大小并发布部分结果；两项各自从启动计时最多 2 秒。OptionalProbe 每类只允许一项在飞，超时不伪装中止同步 I/O，新请求也不无限排队。菜单关闭/重开取消发布，SetDetails 仍核对代次。AppLog 在同一跨进程互斥内判断 2 MiB、轮转上一份及追加，条目限长。

# 1.2.0 读取状态与会话索引（前轮构建）

ShellResponse.Superseded 表达请求已不属于当前菜单，不等于真实传输错误。Expand 前后核对当前连接和 session；App 在 Success 处理前核对请求。分组、搜索单独忽略 OperationCanceledException，分组真实错误还必须持有当前 GroupLoad/请求号。有效同会话晚到成功仍更新缓存，不抢开其他子菜单。

SendAsync 原有 SemaphoreSlim 覆盖整个管道请求/响应；Invoke 预留并移交连接后，旧 Expand 返回取消，执行仍在同一队列中等待读取完成。没有引入 Expand 引用计数或读取移交。真正的读取断连使当前连接退出复用；下一次 Open 跳过已关闭连接而直接恢复。已开始的原生任务仍不被强制终止。

NativeMenuState 属于一次 ShowAt 代次，命令字典增量 TryAdd、先到者胜，树或新命令改变时使无过滤行快照失效。Rebuild 和快捷键匹配复用快照，搜索独立过滤；ShowAt Reset 清空旧委托，完全不跨原生会话缓存。窗口 HWND/固定度量的复用规则不变。

# 1.1.0 回调与测量补充（前轮构建）

分组回调在 root generation 校验之外核对 token 对应的 GroupLoad 请求对象，避免复用窗口失效请求提交。合法同会话晚到结果仍缓存，命令表合并保留已发现 ID。关闭悬停请求只禁止抢开窗口，不错误地撤销已有效的原生命令。

ExpansionAnimator 的 Play/Commit/Render 在同步回调之后再次核对 revision，防止回调启动新动画后被旧流程清理。透明 child 自己偏移时同样对齐并同步 HwndTarget，MenuWindowSize 明确 WM_SIZE 的 16-bit unsigned word 契约。保留同步渲染，不人为差一像素再恢复，也不把关键位置更新延后到空闲队列。

普通单行保留固定模板/DPI 缓存；标题、提示、图标或来源包含明确换行时逐行测量且不写缓存。提示列限 120 DIP、省略并保留完整 ToolTip。展开期间新实现的虚拟行沿用全体新行 ID 快照及当前进度；完成后滚动不重新入场。

# 1.0.1 更多项目动画补充（前轮构建）

更多展开先按最终尺寸准备 layered child 的内容帧，再用一个 Stopwatch 同步父窗口高度、行透明度和位移。内容窗口尺寸在动画期间保持不变，结束时不重复测量或重排。复用行在 DataContextChanged 时恢复视觉状态；Render 回调防重入并忽略同一 rendering timestamp 的重复调用。

工作区边缘改变 parent 位置时，Windows 自动移动 child 并不足以更新 HwndTarget 的屏幕坐标。明确对齐后通知真实且未变化尺寸的 WM_SIZE，触发 layered OnResize 的同步渲染；没有每帧改位图尺寸或反射私有状态。取消和重开撤销准备帧，首帧及展开提交使用 Background 优先级，避免 ContextIdle 被活跃渲染持续推迟。

每个延迟分组令牌持有当前代次、请求和焦点意图；悬停加载中再点击只升级原请求。设置用快照检测外部更新并禁止旧值覆盖，重新加载已保存值解除冲突。剪贴板能力探测只凭文件载荷格式；增强文件定位直接使用原生 PIDL 选择。

# 1.0.1 布局与性能补充（前轮构建）

普通和紧凑菜单共用 MenuMetrics：一级和侧边菜单同宽，分别为 320/300 DIP，共用行字体、图标、留白及 4 DIP 侧边间隙。子菜单按实际首行的布局偏移对齐父级选中行；屏幕不足时仍向左展开或在工作区内上移。

经典模式初次打开只读取原生根项目。ShellMenuSession 保留 CMF_SYNCCASCADEMENU，保证系统终端等打包扩展能完整生成一级项目；不提前递归发送 WM_INITMENUPOPUP 和读取子菜单。侧边展开使用会话内令牌按需读取，搜索补全同一会话的原始树；过期会话和未知令牌拒绝读取，迟到结果不打开旧窗口。增强模式继续完整读取，以保留原有分组和固定功能。未缓存不同右键请求之间的菜单、命令或启用状态。

ShellClipboard 直接探测文件及虚拟文件传输格式，剪贴板没有这些格式时跳过额外粘贴能力菜单；需要时以 CMF_OPTIMIZEFORINVOKE 读取规范动词能力。启动时准备上下文/菜单管道和隐藏 HWND，实际目标仍在每次请求时重新读取。隐藏设置页不在菜单呈现时反复刷新诊断文本。

每层侧边菜单只复用一个窗口，关闭时清掉内容 RootVisual，避免隐藏的旧动作仍出现在可访问树中；重新打开前连接内容并恢复点击状态，按实际 DPI 测量。行模板的固定高度按 DPI、宽度、密度及行类型保存，最多 24 项，不保存动作数据。根窗口的首帧遮蔽、动画终点和取消规则保持。

新增 --performance-check 是有限只读基准，不执行菜单动作；对照使用本轮修改前源码快照加相同采样代码。冻结构建验证和范围见 QA-1.0.1-layout-perf.md。

# 前一轮 1.0.1 补充

后台宿主启动统一通过 BackgroundProcess 的 CreateProcessW，使用 STARTF_FORCEOFFFEEDBACK，不设置 STARTF_USESHOWWINDOW/SW_HIDE，避免把宿主随后创建的原生属性窗口隐藏。参数按 Windows 反斜杠/引号规则引用；命名管道及子进程继承原有隔离环境。

文件夹背景没有选区 IDataObject，静态动词的 %V 依赖调用目录。ShellMenuSession.Invoke 为真实存在的单个背景目录填写 CMINVOKECOMMANDINFOEX 的 lpDirectory/lpDirectoryW，并在同步 InvokeCommand 返回后释放字符串。项目菜单及虚拟背景不伪造物理目录。仍使用原菜单命令 ID 和 Windows 扩展，不重新解释注册表命令，也不对失败动作重试，以免执行重复操作。

ComServiceReference 只共享进程范围的 GIT cookie；每次在当前 COM apartment 调用 CoCreateInstance 获取 IGIT 原生指针，Register/Get/Revoke 后 Release，避免复用 STA RCW 到 RPC MTA。原 site 按 IUnknown 保存，服务查询通过当前 apartment 的 IServiceProvider 指针。原生新建仍由 ShellMenuSite 接收 PIDL 回调，复制后派发绑定视图 STA；会话清理恢复完整原 site。

MenuFirstFrame 使用 DWMWA_CLOAK 阻止复用 HWND 的旧内容先出现；完成一次 WPF Rendering 后，在 ContextIdle 等待 DwmFlush，解除遮蔽并启动 SurfaceAnimator。120ms 有界回退，关闭/重开撤销订阅、定时器和旧代回调；无动画立即显示。侧边菜单同用此入口并延后键盘焦点，实际尺寸不随首帧改变。

验证：新增静态 verb 探针只注册 GUID 命名的临时 HKCU 子键，通过原生 IContextMenu 启动本程序的有限 --verb-probe 模式，检查中文、空格、& 的真实 argv。报告原子写入；finally 删除自己注册的子键。真实视图 RPC 服务另测，不依赖 Explorer 热注册缓存。详见 QA-1.0.1.md。

# 1.0.0 补充

经典菜单仍是默认模式。未搜索时保留原始 HMENU 树，搜索时返回可直接执行的叶子，标题包含完整祖先路径；路径 ID 不重置。打印快捷键优先于规范动词回退，Enter 不把第一项误当原生默认项。

ShellHost 已在 finally 清理前回复；BeginInvocation 开启新建服务请求跟踪，只有实际新建服务请求或回调才等编辑完成或 1200ms，普通动作只让出一次 dispatcher。600ms 原生收尾及活动窗口/线程引用保留机制继续存在。超过 15 秒记录诊断，不关闭仍有原生窗口或任务的宿主。

材质父 HWND 不承担文本输入，以 ImmAssociateContext(hwnd,0) 去掉默认输入法关联；独立 WPF 内容 HWND 仍由输入法属性控制，搜索启用、菜单行禁用。此措施针对空父窗口在激活和内容焦点转移之间吞访问键的实际问题。

设置持久化先保存配置，再写启动项；启动项失败分别还原原始配置文本及注册表值和类型。外部更新不抹掉未保存编辑，显示加载最新配置提示。图标缓存按路径区分，用有期限的 256 项 LRU；日志按数据目录建立跨进程具名互斥锁。

具体审查取舍见 REVIEW-1.0.0.md，验证见 QA-1.0.0.md。以下章节保留 0.9.0 和以前的架构背景；其中“搜索保留层级”在 1.0.0 已改为上述直接叶子结果，“最多 3 秒新建等待”已由上述条件等待替代。

# BetterCM 0.9.0 架构

## 0.9.0 默认原生呈现与新建回调

ClassicMenu 默认 true，旧配置缺字段也采用此默认。App 在这个分支不创建 ActionCatalog 或 CustomActions 的可见入口，直接将 ShellMenuNode 树与原会话执行委托交给 NativeMenuComposer。IncludeShellActions、隐藏、一级固定及更多折叠只作用于手动切换后的增强模式。下述旧 MenuComposer 说明对应保留的增强路径。

ShellMenuSession 同时保留平面 ShellItem 索引和原始 ShellMenuNode 树。树保留顺序、分隔线、子菜单、状态、默认、快捷提示和助记键；IPC 使用可序列化数据，执行委托只在主进程建立。数字 command 不是行唯一键，Home 等 Shell 菜单可复用编号；展示 ID 按树路径区分，调用仍保持同一原始数字身份，各别名的 Enabled 独立。

PinnedNavigation 来自 UIA 标记或 Home 项目的 System.Home.IsPinned 属性。真正固定的顶层导航目录从 Home IShellFolder 枚举到原始 child PIDL，直接 GetUIObjectOf，保留取消固定语义；不是对物理目录菜单手工添加取消固定命令。只读检测不改变用户固定列表。

背景视图的 IObjectWithSite 绑定 ShellMenuSite，提供 INewMenuClient，并将其余 QueryService 委托给旧 site 或 Explorer 服务。ComServiceReference 使用 GIT，为跨线程调用取得适合当前 COM apartment 的服务代理。新建扩展自己创建文件，回调提供准确绝对 PIDL；ILClone 复制借用内存，Dispatcher 将 SelectItem/UIActivate 带回原 STA，视图进行名称编辑。ILCloneFull 是 SDK 内联/宏，不是 shell32 导出。UIActivate 是 IShellView vtable 第 7 槽，SelectItem 是第 14 槽。

宿主在 Invoke 返回后保留新建回调的等待和 dispatcher 工作，再恢复旧 site、释放原生命令，继续既有 Shell 引用及窗口 drain；最后释放视图和 GIT。回调不以目录快照或文件名猜测新文件，也不模拟键盘。位置变化时拒绝旧视图操作。

SurfaceAnimator 的 opening Completed 保持 HoldEnd，Hide/复用时先设置基值再清理旧时钟。ThemeService 在 SourceInitialized 建立菜单材质，菜单不在 ContentRendered 重设 frame/resources；DWMWA_TRANSITIONS_FORCEDISABLED 让 DWM 不叠加第二套窗口动画。该属性主要为 Set 使用，QA 检查设置 HRESULT，不能假设 DwmGetWindowAttribute 可读取它。逐帧组件检查同时记录窗口尺寸和视觉终点。父窗口激活或取得 Win32 焦点时，只在它仍为前台的条件下恢复内容子 HWND 的焦点；菜单行关闭 IME，搜索框显式开启。远程包装器去掉 & 时保留打印的访问键后缀。

## 进程与数据流

主进程负责托盘、设置、输入调度、菜单呈现和内置动作。Shell 扩展在同一可执行文件的 --shell-host 辅助进程中运行，避免扩展读取阻塞主界面。

~~~text
右键 / 注册热键
       |
 InputService → App.TriggerAsync
       |
 ExplorerContextService（独立 STA：点击选区 / Shell.Application）
       |
 SelectedContext 不可变快照 + ActionCatalog + 用户配置
       |
 ShellBridge → 独立 ShellHost STA → IContextMenu 递归读取叶子
       |
 ShellResponse → 标准命令绑定 → MenuComposer 菜单树
       |
 ContextMenuWindow（最终尺寸首次显示 / 各层侧边子窗口）
       |
 ActionExecutor / 原会话 InvokeCommand
~~~

## 输入

InputService 将 WH_MOUSE_LL 放在有消息循环的专用线程。回调仅做窗口范围判断、手势状态、少量 Win32 查询及队列通知，不做 COM、文件读取、日志或 WPF 调度等待。

接管 CabinetWClass / Progman / WorkerW 下的 SHELLDLL_DefView 文件视图，以及 CabinetWClass 下的 SysTreeView32 导航树。普通点击同时接管右键按下和释放，避免 Explorer 开始原生菜单或捕获循环。在独立上下文辅助进程的 STA 上通过 UI Automation SelectionItemPattern 选择点击的未选中项；点击已经选中的项保留多选。背景点击清除选区，然后通过 Shell.Application 同时读取物理路径与 Shell parsing path。

RightClickGesture 保存整次手势归属和按下位置。接管后先处理松开，通知失败、焦点/指针/设置变化都不能把松开交回 Explorer。普通移动不再重放按下事件，消除轻微抖动被误认右拖而唤出原菜单的路径。缓存菜单 HWND，关闭动画期间仍吞掉其内部完整右键，避免按下落在菜单、松开落在 Explorer。Progman/WorkerW 背景直接识别；Explorer 文件视图/导航树沿用范围判断。开始手势时按住 Shift 才让整个原生菜单/右键拖动走系统路径。没有合成全局按键或注入 explorer.exe。

热键由 RegisterHotKey 注册；失败会显示真实状态。参考 [微软低级鼠标钩子文档](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) 和 [SelectionItemPattern 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.selectionitempattern)。

菜单关闭使用三条独立路径：WPF Deactivated、低级鼠标钩子的外部输入判断、OUTOFCONTEXT WinEventHook 的前台切换/移动/最小化通知。输入线程读取不可变 MenuObservation（原生 HWND、物理矩形、代次和显示时间），通过 WM_DISMISS 投递给 UI 线程，始终继续传递外部点击。关闭消息检查代次；窗口事件检查时间戳，避免旧事件误关新菜单。关闭右键接管时仍保留观察线程，以保证热键/预览菜单的关闭行为。

参考 [SetWinEventHook 官方文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)。没有引入前台窗口轮询或键盘全局钩子。

## 上下文与动作

SelectedContext 保存全部选中路径、目录、来源窗口和捕获时间。动作始终执行当前菜单的快照，界面打开后变化的资源管理器选区不会改变已显示动作的目标。

ExplorerContextService 在独立 --context-host 进程内使用 Shell.Application，按 HWND 匹配 Explorer；桌面通过 FindWindowSW 获取。虚拟对象保留 Shell parsing path 与显示名，并禁用文件路径动作。无法可靠确认的点击显示无目标提示，不补发延迟的 WM_CONTEXTMENU。导航栏按 UIA 节点、固定标记规范化和真实 Shell 命名空间解析，不通过导航或文件名猜测目标。其他程序仅在热键呼出时使用剪贴板中的有效路径。

ActionCatalog 统一内置动作 ID、适用条件、快捷键和默认层级，避免菜单 XAML 和执行代码各自维护一份规则。自定义动作要求整个多选选区满足扩展名条件。

ActionExecutor 负责数据和进程启动。标准文件操作由 ShellCommandMap 绑定 canonical verb，ActionExecutor 通过 ShellBridge 调用同一会话的原生命令；复制/剪切不再自行生成 FileDrop 数据；长任务使用异步文件读取、固定大小缓冲区、进度和取消；程序参数先分词再替换，通过 ArgumentList 保持路径边界。Clipboard 写入对短暂占用有限重试。

## Shell 扩展

ShellMenuSession 通过 SHParseDisplayName、SHBindToParent、GetUIObjectOf 获取目标的 IContextMenu，不扫描注册表拼装伪命令。QueryContextMenu 后递归读取 HMENU，向 IContextMenu2/3 转发 WM_INITMENUPOPUP，展开延迟创建的子项。

背景菜单优先通过 ShellViewBinding 绑定原 HWND 的 Shell 浏览器及当前 IShellView，调用 GetItemObject(SVGIO_BACKGROUND, IContextMenu)。桌面通过 FindWindowSW 获取真正桌面命名空间视图，避免把物理 Desktop 文件夹的对象菜单当成桌面背景菜单。不可用时才回退 CreateViewObject/GetUIObjectOf。桌面真实背景提供刷新、查看和排序等命令；ActionCatalog 将 refresh 默认放到一级，F5 调用独立 ShellBridge 连接上的 IShellView.Refresh。Explorer 绑定通过 IFolderView/IPersistFolder2 读取当前位置，读取和执行前对比快照，避免切换标签/路径后执行旧视图命令。参考 [IShellView.GetItemObject](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellview-getitemobject) 和 [IShellView.Refresh](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellview-refresh)。

传输层只读取带文字的叶子动作，同时保留父组路径与 canonical verb；稳定键包含组名及 canonical verb，缺少 verb 时使用标题。固定和隐藏记录保存稳定键，不保存变化的临时命令编号。

MenuComposer 负责呈现分组，MenuEntry 区分组、分隔线与命令行。原始 Shell 层级保留，额外合并 Git/打开方式、不同格式路径，并将压缩工具的根层校验和邮件动作按用途分组。MenuEntry.Children 保存下级项目树。每个分组的子菜单只展示该层，搜索仍直接查找全部叶子；展开不改变 Shell 会话、命令编号或执行委托。搜索直接查找全部叶子，固定项从分组移出，避免重复；同名第三方命令不会因标题相同被删除。仅合并已启用内建动作的明确原生等价项。

ShellBridge 用仅当前用户可连接的命名管道交换 JSON，RequestId 逐请求关联，SessionId 限制命令身份。读取与排队均有 6 秒时限，调用有 30 秒时限。当前浏览连接保留 Shell COM 与 HMENU，并保留最多一个预热备用连接；invoke 独占原连接并把备用连接交给新 open。菜单重开不会排在旧 invoke 后面。读取取消可关闭只读宿主；invoke 发出后的超时、异常和 Dispose 只断开 IPC，绝不 Kill 执行宿主或其进程树。宿主在调用后写入失败/EOF 时释放菜单，并通过 ShellHostLifetime 保持 STA 消息循环，直到 Shell 线程/进程引用和可见窗口释放后退出。连接的 SemaphoreSlim 在所有在途请求 finally 释放后才 Dispose。原生标准按钮与原生叶子共用同一 Session/Command；IncludeShellActions 控制额外项目的显示，标准动作仍需读取 Shell 能力。

第三方扩展自身的行为仍受其实现影响。隔离保护主菜单的响应性，不构成所有扩展都能导入或执行的保证。

## 呈现与性能

菜单在本次系统能力、内置及用户动作就绪后首次显示。元数据和图标在固定单行区域异步补入，并检查菜单代次；系统项目不在打开后补入或触发高度变化。

HoverExpansion 分别管理更多项目和每层分组的短暂悬停计时，默认均 180 ms，移出、关闭和重开取消旧计时。ShowMoreItems=false 直接呈现非常用动作与分组入口，首次显示不做高度展开。CompactMenu 使用 300 DIP 宽、12 DIP 字号及紧凑行高，关闭搜索/工具栏/更多，把工具栏命令放入文字列表；动作分组保留，悬停或点击在侧边打开下级项目。搜索直接匹配所有叶子。

ExpansionAnimator 仅在有限展开期间订阅 CompositionTarget.Rendering，平滑改变非分层父 HWND 尺寸；WPF 透明子窗口提前设置最终尺寸，父窗口裁切揭示内容，避免逐帧缩放分层位图导致旧 UpdateLayeredWindow 尺寸覆盖最终内容。新行可淡入/滑入，已有行不重做入场。更多与分组样式/时长独立，曲线遵从共享配置；关闭、搜索、重开取消帧订阅与旧行动画。动画帧不查询 Shell、不重组 MenuComposer。

网格按头部、工具栏、搜索、状态及底栏的实测尺寸分配列表，移除固定 210 DIP 预留。虚拟列表的 DesiredSize 代表当前视口，所以按行类型各测量一个模板计算自然总高，再约束外层 Border/工作区；避免动画中间视口被固定为最终高度。多行 Shell 提示仍占自己的 Auto 行。原生尺寸更新与 WPF Window.Height 异步更新分离，防旧值覆盖新值。

MenuEntry 保留 Origin/IsShell：具名分组来源用于叶子标记，未知原生来源只标 Shell。分组有背景/左侧标记，来源不改变命令身份。SearchPlaceholder 为非交互 Adorner，焦点及 IME 输入时隐藏，保留标准 TextBox 的组合输入；不占用布局或鼠标事件。

菜单宽 320 DIP、高度上限 640 DIP，仍受目标显示器工作区约束。列表采用 Recycling 虚拟化，Grid 保留头部和底部空间。未固定的星标只在行悬停/选中时显露，取消默认首行选中。

Themes/FluentControls.xaml 统一菜单和设置的按钮、输入框、提示框、复选框、下拉框、侧栏和滚动条。ThemeService 采用可冻结的中性色画刷，并响应 Windows 主题/高对比度变化。主窗口 HWND 保持非分层、AllowsTransparency=false，保留 WindowChrome、系统圆角和阴影。菜单的 MenuBackdrop 使用 Windows App SDK MicaController(BaseAlt)/DesktopAcrylicController，并明确设置 SystemBackdropConfiguration.IsInputActive=true，使短暂无法取得前台焦点的菜单仍保持材质。使用控制器时关闭重复 DWM backdrop；设置窗口继续用 DWM。接口不可用时保留 DWM/纯色回退。参考 [IsInputActive 官方说明](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.systembackdrops.systembackdropconfiguration.isinputactive?view=windows-app-sdk-1.8)。

材质由父 HWND 的 DesktopWindowTarget 合成，WPF Surface 放入一个透明子 HwndSource，避免两个渲染器在同一 HWND 上竞争导致文字被背景覆盖。子窗口提供逐像素 alpha，父窗口保留系统边框与阴影。Surface 显式继承资源和文字颜色，并转发 PreviewKeyDown。每次项目、分组、搜索变化先完成内部布局，再重新测量内容、同步父子原生尺寸；使用测量尺寸而非延后更新的 ActualHeight。系统关闭透明、开启高对比度或不支持材质时使用纯色；不截取屏幕、不运行常驻模糊或渲染循环。

ShowAt 在准备和显示期间抑制旧焦点/边界通知，显示后更新观察时间。检查 Activate 与 GetForegroundWindow 的实际结果；失败时仅当原 Explorer 或菜单仍为前台才在 Input 优先级重试一次，不从另一应用夺取焦点。菜单材质保持不依赖这次激活是否成功；外部点击和前台切换仍按原有关闭策略生效。

图标在独立 STA 读取并缓存最多 256 个键，普通文件按扩展名复用，EXE/LNK/ICO 按路径区分。哈希使用 1 MB 缓冲区，不把整个文件放进内存。

定位使用物理屏幕坐标、MonitorFromPoint 工作区和 PerMonitorV2 DPI。先将 HWND 移到目标屏幕，再测量 WPF 逻辑尺寸并约束到工作区。SurfaceAnimator 仅动画内容层的 Opacity/RenderTransform，不修改 HWND 透明度，也不动画布局尺寸；预设分别控制淡入/淡出、位移和缩放。新配置默认打开 160 ms、关闭 70 ms；升级保留显式时长，范围 60–400 ms，遵循系统动画和高对比度设置。CloseMenu 立即停止接收命令、清除输入观察并取消悬停，视觉结束后隐藏；代次和动画序号防止旧回调隐藏新菜单，关闭期间拒绝旧异步数据。命令无需等待动画。

没有采用 NativeAOT，也没有为冷启动、内存或呼出延迟做未经测量的数字承诺。下一步优化应以测量为依据，重点是上下文查询和慢扩展，而非先替换 UI 技术栈。

## 生命周期、设置和发布

App 负责单实例互斥量、唤醒/退出事件、托盘和窗口生命周期。关闭设置隐藏窗口；退出托盘才关闭程序。再次唤醒可见设置窗口保留未保存编辑。

显式设置 BETTERCM_DATA_DIR 时，单实例/唤醒/退出事件使用该目录路径的哈希作为独立标识，使测试实例不会唤醒或退出实际安装的实例。普通用户数据仍使用原有 BetterCM 标识，以兼容 0.1.0 升级。

ConfigStore 负责校验、原子替换、默认配置导入和错误恢复。用户数据放在 LocalAppData，不依赖程序目录的写权限。FileSystemWatcher 配合 350 ms 防抖合并保存事件。

StartupService 使用 HKCU Run，保存带引号的当前可执行路径与 --background。ThemeService 提供跟随系统、深色、浅色及高对比度资源，并根据 Material 设置与系统条件管理原生材质。未保存选项可预览，关闭预览恢复已保存外观，不写入自启或用户配置。

设置页 Hotkey_Changed 即时运行 Hotkey.Parse，空段、重复修饰键、无效键显示错误并禁用保存；占用只在保存时注册。RestoreSaved 取消表格编辑、预览并重载已保存快照，不写配置或启动项。

Inno Setup 使用当前用户安装、Windows 11 最低版本、x64 包和固定 AppId。升级及卸载先发送 --shutdown。卸载清除启动项，保留用户配置和日志。版本号唯一来源是 BetterCM.csproj 的 Version，AppInfo 读取 AssemblyInformationalVersion；程序集、manifest、UI、报告和安装包均派生版本。安装包自带 .NET/WPF 与 Windows App SDK InteractiveExperiences/Base 的必要材质组件及许可文件。

global.json 固定 SDK 10.0.401、rollForward=latestPatch。verify.ps1 把零警告构建、97 项无界面 xUnit 测试、自带运行时发布、真实 UI/Shell --self-test 连成失败即停止的验证链；Build-Installer.ps1 复用链后编译安装包。GitHub Actions 已配置同一流程，但本地工作区无 Git 远端，远端 CI 未运行。LogicChecks/RegressionFixtures 提供共享逻辑检查和临时文件夹具；Tests 项目不创建 Application、GUI 或 Shell 会话。--self-test 仅检查需 WPF/真实 Shell 的部分，失败返回非零退出码。MainWindow.xaml 已格式化为每个元素独立行，Win32 钩子、窗口样式、消息、DWM、Shell 标志使用命名常量。


## 0.4.0 捕获与诊断

输入线程合并队列里的旧触发，App 为每次捕获分配代次和 CancellationToken。新点击取消旧捕获并立即关闭旧菜单；CaptureAsync 的 1800 ms 时限会断开 IPC 并结束只进行上下文读取的进程，真正废弃卡住的 UIA/COM 工作。旧结果不能更新新菜单。输入钩子从按下到释放持有普通点击，不生成全局输入，也不先显示再关闭系统菜单。EVENT_SYSTEM_MENUPOPUPSTART 计数仅用于诊断，没有菜单隐藏逻辑。

BETTERCM_QA_REPORT 仅在显式测试环境中写入观察快照，包含接管计数、原生弹出计数、上下文、Shell 项目和耗时。--shell-test-host/--context-test-host 只供独立测试协议使用。

## 0.8.0 原生命令执行契约

ShellHostLifetime 显式 OleInitialize，注册 SHCreateThreadRef/SHSetThreadRef 和 SHSetInstanceExplorer；以 CMIC_MASK_NOASYNC 请求同步执行，客户端授予宿主前台权限，调用前激活来源窗口。返回后仍为保留引用/原生对话框持续泵送 STA，不在调用后立刻结束进程。复制/剪切用 Clipboard.Flush 保持 Shell IDataObject 跨宿主退出可用。传输超时不终止执行中的宿主，既有隔离原则不变。

在本机 Explorer 跨进程 GetItemObject(SVGIO_SELECTION) 菜单包装器不返回 canonical verb。文件/命名空间对象使用本地 SHBindToParent/GetUIObjectOf 构建 IContextMenu；保留原视图绑定，读取/执行时核对完整选区及位置。只有单选且有视图时声明 CMF_CANRENAME，重命名用 IShellView.SelectItem(SVSI_EDIT) 进入 Windows 内联编辑，避免命令行和模拟按键。

背景菜单继续从实际 IShellView 获取查看/排序等动作。如果背景包装器未返回 paste canonical verb，额外获取同一目标文件夹的原生对象菜单，映射其 paste/pastelink 命令和当前 Enabled 状态。FolderCommandBase 只作为会话内路由偏移，不依赖资源管理器固定编号；原生命令仍由原 IContextMenu.InvokeCommand 执行。两份同目标原生菜单的相同粘贴标题只用于展示去重，不用于决定执行目标或构造命令。

ShellItem 保留 command offset、verb、group、key 和 Enabled。重复 stable key 使用出现顺序后缀，UI Id 包含 Command 与 Key，避免某些扩展复用 ID 的占位行隐藏有效动作。只按根层规范 verb 绑定系统标准按钮，绝不按本地化的“删除”等标题猜测执行命令。读取失败时标准按钮保持禁用，显示错误；隐藏菜单后立即执行，避免关闭动效夺走原生编辑/对话框的焦点。

参考 Microsoft [QueryContextMenu](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-icontextmenu-querycontextmenu)、[线程引用管理](https://learn.microsoft.com/en-us/windows/win32/shell/managing-thread-references)、[SHSetInstanceExplorer](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shsetinstanceexplorer)。

## 0.8.0 首次显示与侧边子菜单

App.ShowMenuAsync 先捕获配置与上下文，等待本次 ShellResponse，绑定标准动作并合成完整菜单后才调用 ShowAt。菜单和透明内容源均在 Show 前完成最终尺寸、位置与打开动画初态；原生项目加载不再触发高度动画。SetShell 仅为组件验证保留即时重排路径。此选择会把慢扩展的读取时间放在首次显示之前，沿用 Shell 的有限读取超时。

InputService.WatchPreparation 使用独立的负代次观察准备中的请求。新鼠标按下、滚轮或前台变化会取消旧请求，并继续传递原输入；旧响应不能覆盖新菜单。预览使用当前设置窗口为来源。窗口关闭和取消预览也会撤销准备。

ContextMenuWindow.Submenus.cs 复用非分层父 HWND、透明 WPF 内容源与独立材质控制器。每层只创建一个侧边子窗口；切换同层分组关闭旧子窗口及其后代。鼠标移出分组取消待开启计时，进入子菜单不会关闭父菜单；返回父层可用 Left/Escape。叶子执行前立即关闭整个链，原 Shell 会话和命令委托保持不变。

MenuObservation 保存主窗口和各子窗口的实际矩形及 HWND；命中逐个矩形检查，不把矩形间空隙算成菜单内部。前台切换到任意子菜单不会误关主菜单。MenuPlacement 使用物理像素和显示器工作区决定侧边方向；右侧不足时向左，后续层优先沿同一方向。长列表限高并保留滚动。

只有显示更多项目使用 ExpansionAnimator 改变主窗口高度。侧边子菜单由 SurfaceAnimator 按 GroupExpansionAnimation 做淡入或水平滑动，不改变主菜单高度。SubmenuHoverDelayMs 独立于 MoreHoverDelayMs，均默认 180 ms、范围 80–600 ms。小尺寸关闭搜索和更多折叠，保留完整菜单树。

行模板在实际 HwndSource 的 DPI 下测量，按设备像素上取整。ActionsList 使用没有隐含边距的 ScrollViewer/ItemsPresenter 模板，避免默认列表模板额外占用两像素而让短子菜单出现滚动条。

## 0.8.0 导航栏解析

UIA 节点名移除快速访问开始/结束、固定标记和格式控制字符。顶层固定项每次从主文件夹的 Items 读取实际 Path，不缓存固定顺序或旧节点。下载等命名空间使用 ::{GUID} 解析路径，而不把资源管理器 shell: 地址当作 NameSpace 参数。名字存在歧义时不猜测；嵌套物理节点不冒用同名顶层固定目标。

本机验证 NameSpace("shell:Downloads") 返回 null，NameSpace("::{374DE290-123F-4565-9164-39C4925E467B}") 返回实际下载路径；主文件夹枚举包含当前固定下载目录。参考 [Shell.NameSpace 参数说明](https://learn.microsoft.com/en-us/windows/win32/shell/shell-namespace) 和 [KNOWNFOLDERID](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid)。
